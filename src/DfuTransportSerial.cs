/**
 * Copyright 2019 - BCycle, LLC
 * Adapted From: DfuTransportSerial.js
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO.Ports;
using Nordic.nRF.DFU.Util;

namespace Nordic.nRF.DFU
{
    /**
     * Adapted From: DfuTransportSerial.js w/ additions from DfuTransportUsbSerial.js
     * 
     * Serial DFU transport. Supports serial DFU for devices connected
     * through a SEGGER J-Link debugger. See DfuTransportUsbSerial for
     * support for Nordic USB devices without the J-Link debugger.
     *
     * This needs to be given a `serialport` instance when instantiating.
     * Will encode actual requests with SLIP
     */
    public class DfuTransportSerial : DfuTransportPrn
    {
        private string _serialPortName;
        private SerialPort _serialPort;
        private SLIPDecoder _decoder;
        private ManualResetEvent _readyFlag;
        private bool _isReady;
        private bool _isGettingReady;

        private DfuTransportSerial(short packetReceiveNotification)
            : base(packetReceiveNotification)
        {
            _decoder = new SLIPDecoder(this.OnData);
            _readyFlag = new ManualResetEvent(false);
            _isReady = false;
            _isGettingReady = false;
        }

        public DfuTransportSerial(SerialPort serialPort, short packetReceiveNotification = 16)
            : this(packetReceiveNotification)
        {
            _serialPort = serialPort;
        }

        public DfuTransportSerial(string serialPortName, short packetReceiveNotification = 16)
            : this(packetReceiveNotification)
        {
            _serialPortName = serialPortName;
        }

        // Given a command (including opcode), perform SLIP encoding and send it
        // through the wire.
        // This ensures that the serial port is open by calling this.open() - the first
        // call to writeCommand will actually open the port.
        public override async Task WriteCommand(byte[] bytes)
        {
            var encoded = SLIPEncoder.Encode(bytes);

            // Strip the heading 0xC0 character, as to avoid a bug in the nRF SDK implementation
            // of the SLIP encoding/decoding protocol
            encoded = encoded.Skip(1).ToArray();

            await Open();
            _serialPort.Write(encoded, 0, encoded.Length);
        }

        // Given some payload bytes, pack them into a 0x08 command.
        // The length of the bytes is guaranteed to be under this.mtu thanks
        // to the DfuTransportPrn functionality.
        public override Task WriteData(byte[] bytes)
        {
            var commandBytes = new byte[bytes.Length + 1];
            commandBytes[0] = 0x08; // "Write" opcode
            bytes.CopyTo(commandBytes, 1);

            return this.WriteCommand(commandBytes);
        }

        // Opens the port, sets up the event handlers and logging.
        // Returns a Promise when opening is done.
        private async Task Open()
        {
            if (this._serialPort != null && this._serialPort.IsOpen)
            {
                // Good to go, already open
                return;
            }

            if (this._serialPort == null)
            {
                await FindSerialPort();
            }

            System.Diagnostics.Debug.WriteLine($"Opening serial port.");
            _serialPort.DataReceived += SerialPort_DataReceived;
            _serialPort.ErrorReceived += SerialPort_ErrorReceived;
            _serialPort.Open();
        }

        private async Task FindSerialPort()
        {
            if (String.IsNullOrWhiteSpace(_serialPortName))
            {
                throw new DfuException(ErrorCode.ERROR_NO_PORT_SPECIFIED);
            }

            var retryCount = 0;
            while (retryCount < 50 && _serialPort == null)
            {
                var currentPortNames = SerialPort.GetPortNames();
                if (currentPortNames.Contains(_serialPortName, StringComparer.OrdinalIgnoreCase))
                {
                    _serialPort = new SerialPort(_serialPortName, 115200, Parity.None, 8, StopBits.One)
                    {
                        DtrEnable = true,
                        Handshake = Handshake.None,
                        ReadBufferSize = 4096,
                        ReadTimeout = 5000,
                        WriteBufferSize = 2048,
                        WriteTimeout = 5000
                    };
                }
                else
                {
                    await Task.Delay(200);
                    retryCount++;
                }
            }

            if (_serialPort == null)
            {
                throw new DfuException(ErrorCode.ERROR_UNABLE_FIND_PORT, $"Port Name: {_serialPortName}");
            }
        }

        private void SerialPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            // TODO: What should we do about this?
            System.Diagnostics.Debug.WriteLine($"SerialPort Error Received: {e.EventType}");
        }

        // Callback when raw (yet undecoded by SLIP) data is being read from the serial port instance.
        // Called only internally.
        private async void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (e.EventType != SerialData.Eof)
            {
                if (_serialPort.BytesToRead > 0)
                {
                    var bytes = new byte[_serialPort.BytesToRead];
                    if (_serialPort.Read(bytes, 0, bytes.Length) > 0)
                    {
                        await _decoder.DecodeBytes(bytes);
                    }
                }
            }
        }

        // Initializes DFU procedure: sets the PRN and requests the MTU.
        // The serial port is implicitly opened during the first call to writeCommand().
        // Returns a Promise when initialization is done.
        public override async Task Ready()
        {
            if (this._isReady) { return; }
            if (this._isGettingReady)
            {
                _readyFlag.WaitOne();
                return;
            }

            _isGettingReady = true;
            await WriteCommand(new byte[] {
                0x02, // "Set PRN" opcode
                (byte)(_prn & 0xFF), // PRN LSB
                (byte)((_prn >> 8) & 0xFF), // PRN MSB
            });
            var readResult = await Read();
            var readBytes = AssertPacket(0x02, 0)(readResult);

            // Request MTU
            await WriteCommand(new byte[] { 0x07 });
            readResult = await Read();
            readBytes = AssertPacket(0x07, 2)(readResult);

            var mtu = (readBytes[1] * 256) + readBytes[0];

            // Convert wire MTU into max size of data before SLIP encoding:
            // This takes into account:
            // - SLIP encoding ( /2 )
            // - SLIP end separator ( -1 )
            // - Serial DFU write command ( -1 )
            Mtu = Convert.ToInt32(Math.Floor((double)(mtu / 2) - 2));

            // Round down to multiples of 4.
            // This is done to avoid errors while writing to flash memory:
            // writing an unaligned number of bytes will result in an
            // error in most chips.
            Mtu -= Mtu % 4;

            System.Diagnostics.Debug.WriteLine($"Serial wire MTU: {mtu}; un-encoded data max size: {this.Mtu}");
            _isGettingReady = false;
            _isReady = true;
            _readyFlag.Set();
        }

        // Returns a Promise to the version of the DFU protocol that the target implements, as
        // a single integer between 0 to 255.
        // Only bootloaders from 2018 (SDK >= v15) for development boards implement this command.
        public async Task<byte> GetProtocolVersion()
        {
            System.Diagnostics.Debug.WriteLine($"GetProtocolVersion");
            await WriteCommand(new byte[] { 0x00 });
            var readResult = await Read();
            var readBytes = AssertPacket(0x00, 1)(readResult);

            return readBytes[0];
        }

        // Returns a Promise to the version of the DFU protocol that the target implements, as
        // an object with descriptive property names.
        // Only bootloaders from 2018 (SDK >= v15) for development boards implement this command.
        public async Task<HardwareVersion> GetHardwareVersion()
        {
            System.Diagnostics.Debug.WriteLine($"GetHardwareVersion");

            await WriteCommand(new byte[] { 0x0A });
            var readResult = await Read();
            var readBytes = AssertPacket(0x0A, 20)(readResult);

            return new HardwareVersion
            {
                Part = BitConverter.ToInt32(readBytes, 0),
                Variant = BitConverter.ToInt32(readBytes, 4),
                Memory = new HardwareMemoryConfig
                {
                    ROMSize = BitConverter.ToInt32(readBytes, 8),
                    RAMSize = BitConverter.ToInt32(readBytes, 12),
                    ROMPageSize = BitConverter.ToInt32(readBytes, 16)
                }
            };
        }

        // Given an image number (0-indexed), returns a Promise to a plain object describing
        // that firmware image, or boolean false if there is no image at that index.
        // Only bootloaders from 2018 (SDK >= v15) for development boards implement this command.
        public async Task<FirmwareVersion> GetFirmwareVersion(byte imageCount = 0)
        {
            System.Diagnostics.Debug.WriteLine($"GetFirmwareVersion");

            await WriteCommand(new byte[] { 0x0B, imageCount });
            var readResult = await Read();
            var readBytes = AssertPacket(0x0B, 13)(readResult);

            var imgType = "Unknown";
            switch (readBytes[0])
            {
                case 0xFF: return null;
                case 0: imgType = "SoftDevice"; break;
                case 1: imgType = "Application"; break;
                case 2: imgType = "Bootloader"; break;
                default: throw new DfuException(ErrorCode.ERROR_RSP_UNSUPPORTED_TYPE);
            }

            return new FirmwareVersion
            {
                Version = BitConverter.ToUInt32(readBytes, 1),
                Addr = BitConverter.ToUInt32(readBytes, 5),
                Length = BitConverter.ToUInt32(readBytes, 9),
                ImageType = imgType
            };
        }

        // Returns an array containing information about all available firmware images, by
        // sending several GetFirmwareVersion commands.
        public async Task<List<FirmwareVersion>> GetAllFirmwareVersions(byte index = 0, List<FirmwareVersion> accum = null)
        {
            var retVal = accum ?? new List<FirmwareVersion>();
            var version = await GetFirmwareVersion(index);
            if (version != null)
            {
                retVal.Add(version);
                return await GetAllFirmwareVersions((byte)(index + 1), retVal);
            }

            return retVal;
        }
    }

}