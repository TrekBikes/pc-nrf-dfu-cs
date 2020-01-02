/**
 * Copyright 2019 - BCycle, LLC
 * Adapted From: DfuAbstractTransport.js
 * 
 * Copyright (c) 2015 - 2018, Nordic Semiconductor ASA
 *
 * all rights reserved.
 *
 * redistribution and use in source and binary forms, with or without modification,
 * are permitted provided that the following conditions are met:
 *
 * 1. redistributions of source code must retain the above copyright notice, this
 *    list of conditions and the following disclaimer.
 *
 * 2. redistributions in binary form, except as embedded into a nordic
 *    semiconductor asa integrated circuit in a product or a software update for
 *    such product, must reproduce the above copyright notice, this list of
 *    conditions and the following disclaimer in the documentation and/or other
 *    materials provided with the distribution.
 *
 * 3. neither the name of Nordic Semiconductor ASA nor the names of its
 *    contributors may be used to endorse or promote products derived from this
 *    software without specific prior written permission.
 *
 * 4. this software, with or without modification, must only be used with a
 *    Nordic Semiconductor ASA integrated circuit.
 *
 * 5. any software provided in binary form under this license must not be reverse
 *    engineered, decompiled, modified and/or disassembled.
 *
 * this software is provided by Nordic Semiconductor ASA "as is" and any express
 * or implied warranties, including, but not limited to, the implied warranties
 * of merchantability, noninfringement, and fitness for a particular purpose are
 * disclaimed. in no event shall Nordic Semiconductor ASA or contributors be
 * liable for any direct, indirect, incidental, special, exemplary, or
 * consequential damages (including, but not limited to, procurement of substitute
 * goods or services; loss of use, data, or profits; or business interruption)
 * however caused and on any theory of liability, whether in contract, strict
 * liability, or tort (including negligence or otherwise) arising in any way out
 * of the use of this software, even if advised of the possibility of such damage.
 *
 */

using Nordic.nRF.DFU.Util;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Nordic.nRF.DFU
{
    public abstract class DfuAbstractTransport
    {

        // Restarts the DFU procedure by sending a create command of
        // type 1 (init payload / "command object").
        // By default, CRC checks are done in order to continue an interrupted
        // transfer. Calling this before a sendInitPacket() will forcefully
        // re-send everything.
        public virtual Task Restart()
        {
            System.Diagnostics.Debug.WriteLine("Forcefully restarting DFU procedure");
            return this.CreateObject(1, 0x10);
        }

        // Abort the DFU procedure, which means exiting the bootloader mode
        // and trying to switch back to the app mode
        public virtual Task Abort()
        {
            System.Diagnostics.Debug.WriteLine("Exit Bootloader Mode");
            return this.AbortObject();
        }

        // Given a Uint8Array, sends it as an init payload / "command object".
        // Returns a Promise.
        public virtual Task SendInitPacket(byte[] bytes)
        {
            return this.SendPayload(0x01, bytes);
        }

        // Given a Uint8Array, sends it as the main payload / "data object".
        // Returns a Promise.
        public virtual Task SendFirmwareImage(byte[] bytes)
        {
            return this.SendPayload(0x02, bytes);
        }

        // Sends either a init payload ("init packet"/"command object") or a data payload
        // ("firmware image"/"data objects")
        protected virtual async Task SendPayload(byte type, byte[] bytes, bool resumeAtChunkBoundary = false)
        {
            System.Diagnostics.Debug.WriteLine($"Sending payload of type {type}");
            var selectedObject = await this.AwaitAndCheckException(SelectObject(type));
            var offset = selectedObject.Item1;
            var crcSoFar = selectedObject.Item2;
            var chunkSize = selectedObject.Item3;
            if (offset != 0)
            {
                System.Diagnostics.Debug.WriteLine($"Offset is not zero ({offset}). Checking if graceful continuation is possible.");
                var crc = CRC32.ComputeHash(bytes.Take((int)offset).ToArray(), null);

                if (crc == crcSoFar)
                {
                    System.Diagnostics.Debug.WriteLine($"CRC match");
                    if (offset == bytes.Length)
                    {
                        System.Diagnostics.Debug.WriteLine($"Payload already transferred sucessfully, sending execute command just in case.");

                        // Send an exec command, just in case the previous connection broke
                        // just before the exec command. An extra exec command will have no
                        // effect.
                        await this.AwaitAndCheckException(this.ExecuteObject());
                        return;
                    }

                    if ((offset % chunkSize) == 0 && !resumeAtChunkBoundary)
                    {
                        // Edge case: when an exact multiple of the chunk size has
                        // been transferred, the host side cannot be sure if the last
                        // chunk has been marked as ready ("executed") or not.
                        // Fortunately, if an "execute" command is sent right after
                        // another "execute" command, the second one will do nothing
                        // and yet receive an "OK" response code.
                        System.Diagnostics.Debug.WriteLine($"Edge case: payload transferred up to page boundary; previous execute command might have been lost, re-sending.");

                        await this.AwaitAndCheckException(this.ExecuteObject());
                        await this.AwaitAndCheckException(this.SendPayload(type, bytes, true));
                        return;
                    }

                    System.Diagnostics.Debug.WriteLine($"Payload partially transferred sucessfully, continuing from offset {offset}.");

                    // Send the remainder of a half-finished chunk
                    var chunkEnd = (uint)Math.Min(bytes.Length, (offset + chunkSize) - (offset % chunkSize));

                    await this.AwaitAndCheckException(this.SendAndExecutePayloadChunk(
                        type, bytes, offset,
                        chunkEnd, chunkSize, crc
                    ));

                    return;
                }

                // Note that these are CRC mismatches at a chunk level, not at a
                // transport level. Individual transports might decide to roll back
                // parts of a chunk (re-creating it) on PRN CRC failures.
                // But here it means that there is a CRC mismatch while trying to
                // continue an interrupted DFU, and the behaviour in this case is to panic.
                System.Diagnostics.Debug.WriteLine($"CRC mismatch: expected/actual 0x{crc:X}/0x{crcSoFar:X}");

                throw new DfuException(ErrorCode.ERROR_PRE_DFU_INTERRUPTED);
            }

            var end = (uint)Math.Min(bytes.Length, chunkSize);

            await this.AwaitAndCheckException(this.CreateObject(type, end));
            await this.AwaitAndCheckException(this.SendAndExecutePayloadChunk(type, bytes, 0, end, chunkSize));
        }


        // Sends *one* chunk.
        // Sending a chunk involves:
        // - (Creating a payload chunk) (this is done *before* sending the current chunk)
        // - Writing the payload chunk (wire implementation might perform fragmentation)
        // - Check CRC32 and offset of payload so far
        // - Execute the payload chunk (target might write a flash page)
        protected virtual async Task SendAndExecutePayloadChunk(byte type, byte[] bytes, uint start, uint end, uint chunkSize, uint? crcSoFar = null)
        {
            await this.SendPayloadChunk(type, bytes, start, end, chunkSize, crcSoFar);
            await this.ExecuteObject();

            if (end >= bytes.Length)
            {
                System.Diagnostics.Debug.WriteLine($"Sent {end} bytes, this payload type is finished");
                return;
            }

            // Send next chunk
            System.Diagnostics.Debug.WriteLine($"Sent {end} bytes, not finished yet (until {bytes.Length})");
            var nextEnd = (uint)Math.Min(bytes.Length, end + chunkSize);
            await this.CreateObject(type, nextEnd - end);
            await this.SendAndExecutePayloadChunk(
                    type, bytes, end, nextEnd, chunkSize,
                    CRC32.ComputeHash(bytes.Take((int)end).ToArray(), null)
                );
        }

        // Sends one payload chunk, retrying if necessary.
        // This is done without checksums nor sending the "execute" command. The reason
        // for splitting this code apart is that retrying a chunk is easier when abstracting away
        // the "execute" and "next chunk" logic
        protected virtual async Task SendPayloadChunk(byte type, byte[] bytes, uint start, uint end, uint chunkSize, uint? crcSoFar = null, uint retries = 0)
        {
            byte[] subarray = bytes.Skip((int)start).Take((int)(end - start)).ToArray();
            var crcAtChunkEnd = CRC32.ComputeHash(subarray, crcSoFar);

            var writeResult = await this.WriteObject(subarray, crcSoFar, start);

            System.Diagnostics.Debug.WriteLine($"Payload type fully transferred, requesting explicit checksum");
            var crcResult = await this.CrcObject(end, crcAtChunkEnd);
            var offset = crcResult.Item1;
            var crc = crcResult.Item2;

            try
            {
                if (offset != end)
                {
                    throw new DfuException(ErrorCode.ERROR_UNEXPECTED_BYTES, $"Expected {end} bytes to have been sent, actual is {offset} bytes.");
                }

                if (crcAtChunkEnd != crc)
                {
                    throw new DfuException(ErrorCode.ERROR_CRC_MISMATCH, $"CRC mismatch after {end} bytes have been sent: expected {crcAtChunkEnd}, got {crc}.");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Explicit checksum OK at {end} bytes");
                }
            }
            catch (DfuException err)
            {
                if (retries >= 5)
                {
                    throw new DfuException(ErrorCode.ERROR_TOO_MANY_WRITE_FAILURES, $"Last failure: {err}");
                }

                System.Diagnostics.Debug.WriteLine($"Chunk write failed ({err}) Re-sending the whole chunk starting at {start}. Times retried: {retries}");

                // FIXME: Instead of re-creating the whole chunk, select the payload
                // type again and check the CRC so far.

                var newStart = start - (start % chunkSize);
                // Rewind to the start of the block
                var rewoundCrc = newStart == 0 ? (uint?)null : CRC32.ComputeHash(bytes.Take((int)newStart).ToArray(), null);

                await this.CreateObject(type, end - start);
                await this.SendPayloadChunk(
                        type, bytes, newStart, end,
                        chunkSize, rewoundCrc, retries + 1
                    );
            }
        }


        // The following 5 methods have a 1-to-1 mapping to the 5 DFU requests
        // documented at http://infocenter.nordicsemi.com/index.jsp?topic=%2Fcom.nordic.infocenter.sdk5.v14.0.0%2Flib_dfu_transport.html
        // These are meant as abstract methods, meaning they do nothing and subclasses
        // must provide an implementation.

        // Allocate space for a new payload chunk. Resets the progress
        // since the last Execute command, and selects the newly created object.
        // Must return a Promise
        // Actual implementation must be provided by concrete subclasses of DfuAbstractTransport.
        protected abstract Task<byte[]> CreateObject(byte type, uint size);

        // Fill the space previously allocated with createObject() with the given bytes.
        // Also receives the absolute offset and CRC32 so far, as some wire
        // implementations perform extra CRC32 checks as the fragmented data is being
        // checksummed (and the return value for those checks involves both the absolute
        // offset and the CRC32 value). Note that the CRC and offset are not part of the
        // SDK implementation.
        // Must return a Promise to an array of [offset, crc]
        // Actual implementation must be provided by concrete subclasses of DfuAbstractTransport.
        protected abstract Task<Tuple<uint, uint>> WriteObject(byte[] bytes, uint? crcSoFar, uint offsetSoFar);

        // Trigger a CRC calculation of the data sent so far.
        // Must return a Promise to an array of [offset, crc]
        // Actual implementation must be provided by concrete subclasses of DfuAbstractTransport.
        protected abstract Task<Tuple<uint, uint>> CrcObject(uint offset, uint? crcSoFar);

        // Marks payload chunk as fully sent. The target may write a page of flash memory and
        // prepare to receive the next chunk (if not all pages have been sent), or start
        // firmware postvalidation (if all pages have been sent).
        // Must return a Promise
        // Actual implementation must be provided by concrete subclasses of DfuAbstractTransport.
        protected abstract Task<byte[]> ExecuteObject();

        // Marks the last payload type as "active".
        // Returns a Promise to an array of [offset, crc, max chunk size].
        // The offset is *absolute* - it includes all chunks sent so far, and so can be several
        // times larger than the max chunk size.
        // Typically the chunk size will be the size of a page of flash memory.
        // Actual implementation must be provided by concrete subclasses of DfuAbstractTransport.
        protected abstract Task<Tuple<uint, uint, uint>> SelectObject(byte type);

        // Abort bootloader mode and try to switch back to app mode
        protected abstract Task AbortObject();

        // Records the last exception to occur during the current operation
        protected internal DfuException LastException { get; set; }
    }
}