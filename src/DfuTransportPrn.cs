/**
 * Copyright 2019 - BCycle, LLC
 * Adapted From: DfuTransportPrn.js
 * 
 * copyright (c) 2015 - 2018, Nordic Semiconductor ASA
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
using System.Threading;
using System.Threading.Tasks;

namespace Nordic.nRF.DFU
{
    /**
     * PRN-capable abstract DFU transport.
     *
     * This abstract class inherits from DfuAbstractTransport, and implements
     * PRN (Packet Receive Notification) and the splitting of a page of data
     * into smaller chunks.
     *
     * Both the Serial DFU and the BLE DFU protocols implement these common bits of
     * logic, but they do so in a lower level than the abstract 5-commands DFU protocol.
     */
    public abstract class DfuTransportPrn : DfuAbstractTransport
    {
        protected readonly int _prn;
        private Tuple<byte, byte[]> _lastReceivedPacket;
        private ManualResetEvent _waitingForPacket;

        public int? Mtu { get; protected set; }

        // The constructor takes the value for the PRN interval. It should be
        // provided by the concrete subclasses.
        protected DfuTransportPrn(short packetReceiveNotification = 16) : base()
        {
            _prn = packetReceiveNotification;

            // Store *one* message waitig to be read()
            _lastReceivedPacket = null;

            // Allow one waiting event -- was: Store *one* reference to a read() callback function
            _waitingForPacket = null;

            // Maximum Transmission Unit. The maximum amount of bytes that can be sent to a
            // writeData() call. Its value **must** be filled in by the concrete subclasses
            // before any data is sent.
            Mtu = null;
        }

        // The following are meant as abstract methods, meaning they do nothing and subclasses
        // must provide an implementation.

        // Abstract method. Concrete subclasses shall implement sending the bytes
        // into the wire/air.
        // The bytes shall include an opcode and payload.
        public abstract Task WriteCommand(byte[] bytes);

        // Abstract method. Concrete subclasses shall implement sending the bytes
        // into the wire/air.
        // The bytes are all data bytes. Subclasses are responsible for packing
        // this into a command (serial DFU) or sending them through the wire/air
        // through an alternate channel (BLE DFU)
        public abstract Task WriteData(byte[] bytes);

        // Abstract method, called before any operation that would send bytes.
        // Concrete subclasses **must**:
        // - Check validity of the connection,
        // - Re-initialize connection if needed, including
        //   - Set up PRN
        //   - Request MTU (only if the transport has a variable MTU)
        // - Return a Promise whenever the connection is ready.
        public abstract Task Ready();

        // Requests a (decoded and) parsed packet/message, either a response
        // to a previous command or a PRN notification.
        // Returns a Promise to [opcode, Uint8Array].
        // Cannot have more than one pending request at any time.
        protected virtual Task<Tuple<byte, byte[]>> Read()
        {
            if (this._waitingForPacket != null)
            {
                throw new DfuException(ErrorCode.ERROR_READ_CONFLICT);
            }

            if (this._lastReceivedPacket != null)
            {
                var packet = this._lastReceivedPacket;
                this._lastReceivedPacket = null;
                return Task.FromResult(packet);
            }

            // Store the callback so it can be called as soon as the wire packet is
            // ready. Add a 5sec timeout while we're at it; remove that timeout
            // when data is actually received.
            try
            {
                this._waitingForPacket = new ManualResetEvent(false);
                if (!this._waitingForPacket.WaitOne(5000))
                {
                    throw new DfuException(ErrorCode.ERROR_TIMEOUT_READING_SERIAL);
                }
            }
            finally
            {
                _waitingForPacket.Dispose();
                _waitingForPacket = null;
            }

            if (this._lastReceivedPacket != null)
            {
                var packet = this._lastReceivedPacket;
                this._lastReceivedPacket = null;
                return Task.FromResult(packet);
            }

            return Task.FromResult(default(Tuple<byte, byte[]>));
        }

        // Must be called when a (complete) packet/message is received, with the
        // (decoded) bytes of the entire packet/message. Either stores the packet
        // just received, or calls the pending read() callback to unlock it
        protected virtual async Task OnData(byte[] bytes)
        {
            if (this._lastReceivedPacket != null)
            {
                throw new DfuException(ErrorCode.ERROR_RECEIVE_TWO_MESSAGES);
            }

            this._lastReceivedPacket = await Parse(bytes);
            if (this._waitingForPacket != null)
            {
                this._waitingForPacket.Set();
            }
        }

        // Parses a received DFU response packet/message, does a couple of checks,
        // then returns an array of the form [opcode, payload] if the
        // operation was sucessful.
        // If there were any errors, returns a rejected Promise with an error message.
        protected virtual async Task<Tuple<byte, byte[]>> Parse(byte[] bytes)
        { // eslint-disable-line class-methods-use-this
            if (bytes == null || bytes.Length <= 0 || bytes[0] != 0x60)
            {
                throw new DfuException(ErrorCode.ERROR_RESPONSE_NOT_START_WITH_0x60);
            }

            var opcode = bytes[1];
            var resultCode = (ErrorCode)bytes[2];
            if (resultCode == ErrorCode.ERROR_MESSAGE_RSP)
            {
                System.Diagnostics.Debug.WriteLine($"Parsed DFU response packet: opcode {opcode} payload: {BitConverter.ToString(bytes.Take(3).ToArray())}");
                return Tuple.Create(opcode, bytes.Skip(3).ToArray());
            }

            var errorCode = ErrorCode.ERROR_MESSAGE;
            var errorStr = String.Empty;
            var extCode = (ErrorCode)((int)ErrorCode.ERROR_RSP_EXT_ERROR - ((int)ErrorCode.ERROR_MESSAGE_RSP << 8));
            var resultCodeRsp = (ErrorCode)(((int)ErrorCode.ERROR_MESSAGE_RSP << 8) + (int)resultCode);
            if (ResponseErrorMessages.Messages[resultCodeRsp] != null)
            {
                errorCode = resultCodeRsp;
            }
            else if (resultCode == extCode)
            {
                var extendedErrorCode = (ErrorCode)bytes[3];
                var resultCodeExt = (ErrorCode)(((int)ErrorCode.ERROR_MESSAGE_EXT << 8) + (int)extendedErrorCode);
                if (ExtendedErrorMessages.Messages[resultCodeExt] != null)
                {
                    errorCode = resultCodeExt;
                }
                else
                {
                    errorStr = $"0x0B 0x{((int)extendedErrorCode):X2}";
                }
            }
            else
            {
                errorStr = $"0x{((int)resultCode):X2}";
                errorCode = ErrorCode.ERROR_RSP_OP_CODE_NOT_SUPPORTED;
            }

            System.Diagnostics.Debug.WriteLine($"Parse Error: {errorCode}, {errorStr}");
            throw new DfuException(errorCode, errorStr);
        }

        // Returns a *function* that checks a [opcode, bytes] parameter against the given
        // opcode and byte length, and returns only the bytes.
        // If the opcode is different, or the payload length is different, an error is thrown.
        protected virtual Func<Tuple<byte, byte[]>, byte[]> AssertPacket(byte expectedOpcode, int expectedLength)
        {
            return (Tuple<byte, byte[]> response) =>
            {
                if (response == null || response.Item2 == null || (response.Item2.Length <= 0 && expectedLength > 0))
                {
                    System.Diagnostics.Debug.WriteLine($"Tried to assert an empty parsed response!");
                    System.Diagnostics.Debug.WriteLine($"response: {response}");
                    throw new DfuException(ErrorCode.ERROR_ASSERT_EMPTY_RESPONSE);
                }

                byte opcode = response.Item1;
                byte[] bytes = response.Item2;
                if (opcode != expectedOpcode)
                {
                    throw new DfuException(ErrorCode.ERROR_UNEXPECTED_RESPONSE_OPCODE, $"Expected opcode {expectedOpcode}, got {opcode} instead.");
                }

                if (bytes.Length != expectedLength)
                {
                    throw new DfuException(ErrorCode.ERROR_UNEXPECTED_RESPONSE_BYTES, $"Expected {expectedLength} bytes in response to opcode {expectedOpcode}, got {bytes.Length} bytes instead.");
                }

                return bytes;
            };
        }

        protected override async Task<byte[]> CreateObject(byte type, uint size)
        {
            await Ready();
            System.Diagnostics.Debug.WriteLine($"CreateObject type {type}, size {size}");
            await WriteCommand(new byte[]
            {
                0x01, // "Create object" opcode
                type,
                (byte)(size & 0xFF),
                (byte)((size >> 8) & 0xFF),
                (byte)((size >> 16) & 0xFF),
                (byte)((size >> 24) & 0xFF)
            });

            var result = await Read();
            return AssertPacket(0x01, 0)(result);
        }

        protected override async Task<Tuple<uint, uint>> WriteObject(byte[] bytes, uint? crcSoFar, uint offsetSoFar)
        {
            System.Diagnostics.Debug.WriteLine($"WriteObject");
            await Ready();
            return await WriteObjectPiece(bytes, crcSoFar, offsetSoFar, 0);
        }

        // Sends *one* write operation (with up to this.mtu bytes of un-encoded data)
        // Triggers a counter-based PRN confirmation
        protected virtual async Task<Tuple<uint, uint>> WriteObjectPiece(byte[] bytes, uint? crcSoFar, uint offsetSoFar, int prnCount)
        {
            await Ready();
            var sendLength = Math.Min(Mtu ?? 1, bytes.Length);

            var bytesToSend = bytes.Take(sendLength).ToArray();

            var newOffsetSoFar = offsetSoFar + (uint)sendLength;
            var newCrcSoFar = CRC32.ComputeHash(bytesToSend, crcSoFar);
            var newPrnCount = prnCount + 1;

            await WriteData(bytesToSend);
            if (this._prn > 0 && newPrnCount >= this._prn)
            {
                System.Diagnostics.Debug.WriteLine($"PRN hit, expecting CRC");
                // Expect a CRC due to PRN
                newPrnCount = 0;
                var crcResult = await this.ReadCrc();
                var offset = crcResult.Item1;
                var crc = crcResult.Item2;
                if (newOffsetSoFar != offset || newCrcSoFar != crc)
                {
                    System.Diagnostics.Debug.WriteLine($"PRN checksum OK at offset {offset} (0x{offset:X}) (0x{crc:X})");
                    throw new DfuException(ErrorCode.ERROR_CRC_MISMATCH, $"CRC mismatch during PRN at byte {offset}/{newOffsetSoFar}, expected 0x{newCrcSoFar:X2} but got 0x{crc:X2} instead");
                }
            }

            if (sendLength < bytes.Length)
            {
                // Send more stuff
                return await WriteObjectPiece(
                    bytes.Skip(sendLength).ToArray(),
                    newCrcSoFar, newOffsetSoFar, newPrnCount
                );
            }

            return Tuple.Create(newOffsetSoFar, newCrcSoFar);
        }

        // Reads a PRN CRC response and returns the offset/CRC pair
        protected virtual async Task<Tuple<uint, uint>> ReadCrc()
        {
            await Ready();
            var readResult = await Read();
            var bytes = AssertPacket(0x03, 8)(readResult);

            // Decode little-endian fields, by using a DataView with the
            // same buffer *and* offset than the Uint8Array for the packet payload
            var offset = BitConverter.ToUInt32(bytes);
            var crc = BitConverter.ToUInt32(bytes, 4);

            return Tuple.Create(offset, crc);
        }

        protected override async Task<Tuple<uint, uint>> CrcObject(uint offset, uint? crcSoFar)
        {
            System.Diagnostics.Debug.WriteLine($"Request CRC explicitly");
            await Ready();
            await WriteCommand(new byte[] { 0x03 });

            return await ReadCrc();
        }

        protected override async Task<byte[]> ExecuteObject()
        {
            System.Diagnostics.Debug.WriteLine($"Execute (mark payload chunk as ready)");
            await Ready();
            await WriteCommand(new byte[] { 0x04 });
            var readResult = await Read();
            return AssertPacket(0x04, 0)(readResult);
        }

        protected override async Task<Tuple<uint, uint, uint>> SelectObject(byte type)
        {
            System.Diagnostics.Debug.WriteLine($"Select (report max size and current offset/crc)");
            await Ready();
            await WriteCommand(new byte[] { 0x06, type });
            var readResult = await Read();
            var bytes = AssertPacket(0x06, 12)(readResult);
            var chunkSize = BitConverter.ToUInt32(bytes, 0);
            var offset = BitConverter.ToUInt32(bytes, 4);
            var crc = BitConverter.ToUInt32(bytes, 8);

            System.Diagnostics.Debug.WriteLine($"selected {type}: offset {offset}, crc {crc}, max size {chunkSize}");
            return Tuple.Create(offset, crc, chunkSize);
        }

        protected override async Task AbortObject()
        {
            System.Diagnostics.Debug.WriteLine($"Abort (mark payload chunk as ready)");
            await Ready();
            await WriteCommand(new byte[] { 0x0C });
        }
    }
}
