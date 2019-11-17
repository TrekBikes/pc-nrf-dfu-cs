/**
 * Copyright 2019 - BCycle, LLC
 * Adapted from: DfuError.js
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

using System;
using System.Collections.Generic;

namespace Nordic.nRF.DFU
{
    public enum ErrorCode
    {
        // Error message types
        ERROR_MESSAGE = 0x00,
        ERROR_MESSAGE_RSP = 0x01,
        ERROR_MESSAGE_EXT = 0x02,

        // Error code for DfuAbstractTransport
        ERROR_CAN_NOT_INIT_ABSTRACT_TRANSPORT = 0x0000,
        ERROR_PRE_DFU_INTERRUPTED = 0x0001,
        ERROR_UNEXPECTED_BYTES = 0x0002,
        ERROR_CRC_MISMATCH = 0x0003,
        ERROR_TOO_MANY_WRITE_FAILURES = 0x0004,

        // Error code for DfuTransportPrn
        ERROR_CAN_NOT_INIT_PRN_TRANSPORT = 0x0010,
        ERROR_CAN_NOT_USE_HIGHER_PRN = 0x0011,
        ERROR_READ_CONFLICT = 0x0012,
        ERROR_TIMEOUT_READING_SERIAL = 0x0013,
        ERROR_RECEIVE_TWO_MESSAGES = 0x0014,
        ERROR_RESPONSE_NOT_START_WITH_0x60 = 0x0015,
        ERROR_ASSERT_EMPTY_RESPONSE = 0x0016,
        ERROR_UNEXPECTED_RESPONSE_OPCODE = 0x0017,
        ERROR_UNEXPECTED_RESPONSE_BYTES = 0x0018,

        // Error code for DfuTransportSink
        ERROR_MUST_HAVE_PAYLOAD = 0x0031,
        ERROR_INVOKED_MISMATCHED_CRC32 = 0x0032,
        ERROR_MORE_BYTES_THAN_CHUNK_SIZE = 0x0033,
        ERROR_INVALID_PAYLOAD_TYPE = 0x0034,

        // Error code for DfuTransportNoble
        ERROR_CAN_NOT_DISCOVER_DFU_CONTROL = 0x0051,
        ERROR_TIMEOUT_FETCHING_CHARACTERISTICS = 0x0052,
        ERROR_CAN_NOT_SUBSCRIBE_CHANGES = 0x0053,

        // Error code for DfuTransportSerial(including slow and usb)
        ERROR_UNKNOWN_FIRMWARE_TYPE = 0x0071,
        ERROR_UNABLE_FIND_PORT = 0x0072,
        ERROR_NO_PORT_SPECIFIED = 0x0073,

        // Error code for response error messages
        ERROR_RSP_INVALID = 0x0100,
        ERROR_RSP_SUCCESS = 0x0101,
        ERROR_RSP_OP_CODE_NOT_SUPPORTED = 0x0102,
        ERROR_RSP_INVALID_PARAMETER = 0x0103,
        ERROR_RSP_INSUFFICIENT_RESOURCES = 0x0104,
        ERROR_RSP_INVALID_OBJECT = 0x0105,
        ERROR_RSP_UNSUPPORTED_TYPE = 0x0107,
        ERROR_RSP_OPERATION_NOT_PERMITTED = 0x0108,
        ERROR_RSP_OPERATION_FAILED = 0x010A,
        ERROR_RSP_EXT_ERROR = 0x010B,

        // Error code for extended error messages
        ERROR_EXT_NO_ERROR = 0x0200,
        ERROR_EXT_INVALID_ERROR_CODE = 0x0201,
        ERROR_EXT_WRONG_COMMAND_FORMAT = 0x0202,
        ERROR_EXT_UNKNOWN_COMMAND = 0x0203,
        ERROR_EXT_INIT_COMMAND_INVALID = 0x0204,
        ERROR_EXT_FW_VERSION_FAILURE = 0x0205,
        ERROR_EXT_HW_VERSION_FAILURE = 0x0206,
        ERROR_EXT_SD_VERSION_FAILURE = 0x0207,
        ERROR_EXT_SIGNATURE_MISSING = 0x0208,
        ERROR_EXT_WRONG_HASH_TYPE = 0x0209,
        ERROR_EXT_HASH_FAILED = 0x020A,
        ERROR_EXT_WRONG_SIGNATURE_TYPE = 0x020B,
        ERROR_EXT_VERIFICATION_FAILED = 0x020C,
        ERROR_EXT_INSUFFICIENT_SPACE = 0x020D,
        ERROR_EXT_FW_ALREADY_PRESENT = 0x020E,
    }

    public class ErrorTypes
    {
        private ErrorTypes() { }

        private static Dictionary<ErrorCode, string> ErrorCodeMap = new Dictionary<ErrorCode, string>
        {
            {ErrorCode.ERROR_MESSAGE, "Error message"},
            {ErrorCode.ERROR_MESSAGE_RSP, "Error message for known response code from DFU target"},
            {ErrorCode.ERROR_MESSAGE_EXT, "Error message for known extended error code from DFU target"},
        };
        public static ErrorTypes Messages { get; } = new ErrorTypes();
        public string this[ErrorCode code]
        {
            get
            {
                if (ErrorCodeMap.ContainsKey(code)) { return ErrorCodeMap[code]; }
                return null;
            }
        }
    }

    public class ErrorMessages
    {
        private ErrorMessages() { }

        public static Dictionary<ErrorCode, string> ErrorCodeMap = new Dictionary<ErrorCode, string>
        {
            {ErrorCode.ERROR_CAN_NOT_INIT_ABSTRACT_TRANSPORT, "Cannot instantiate DfuAbstractTransport, use a concrete subclass instead."},
            {ErrorCode.ERROR_PRE_DFU_INTERRUPTED, "A previous DFU process was interrupted, and it was left in such a state that cannot be continued. Please perform a DFU procedure disabling continuation."},
            {ErrorCode.ERROR_UNEXPECTED_BYTES, "Unexpected bytes to be sent."},
            {ErrorCode.ERROR_CRC_MISMATCH, "CRC mismatches."},
            {ErrorCode.ERROR_TOO_MANY_WRITE_FAILURES, "Too many write failures."},
            {ErrorCode.ERROR_CAN_NOT_INIT_PRN_TRANSPORT, "Cannot instantiate DfuTransportPrn, use a concrete subclass instead."},
            {ErrorCode.ERROR_CAN_NOT_USE_HIGHER_PRN, "DFU procotol cannot use a PRN higher than 0xFFFF."},
            {ErrorCode.ERROR_READ_CONFLICT, "DFU transport tried to read() while another read() was still waiting"},
            {ErrorCode.ERROR_TIMEOUT_READING_SERIAL, "Timeout while reading from serial transport. See https =//github.com/NordicSemiconductor/pc-nrfconnect-core/blob/master/doc/serial-timeout-troubleshoot.md"},
            {ErrorCode.ERROR_RECEIVE_TWO_MESSAGES, "DFU transport received two messages at once"},
            {ErrorCode.ERROR_RESPONSE_NOT_START_WITH_0x60, "Response from DFU target did not start with 0x60"},
            {ErrorCode.ERROR_ASSERT_EMPTY_RESPONSE, "Tried to assert an empty parsed response"},
            {ErrorCode.ERROR_UNEXPECTED_RESPONSE_OPCODE, "Unexpected opcode in response"},
            {ErrorCode.ERROR_UNEXPECTED_RESPONSE_BYTES, "Unexpected bytes in response"},
            {ErrorCode.ERROR_MUST_HAVE_PAYLOAD, "Must create/select a payload type first."},
            {ErrorCode.ERROR_INVOKED_MISMATCHED_CRC32, "Invoked with a mismatched CRC32 checksum."},
            {ErrorCode.ERROR_MORE_BYTES_THAN_CHUNK_SIZE, "Tried to push more bytes to a chunk than the chunk size."},
            {ErrorCode.ERROR_INVALID_PAYLOAD_TYPE, "Tried to select invalid payload type. Valid types are 0x01 and 0x02."},
            {ErrorCode.ERROR_CAN_NOT_DISCOVER_DFU_CONTROL, "Could not discover DFU control and packet characteristics"},
            {ErrorCode.ERROR_TIMEOUT_FETCHING_CHARACTERISTICS, "Timeout while fetching characteristics from BLE peripheral"},
            {ErrorCode.ERROR_CAN_NOT_SUBSCRIBE_CHANGES, "Could not subscribe to changes of the control characteristics"},
            {ErrorCode.ERROR_UNKNOWN_FIRMWARE_TYPE, "Unkown firmware image type"},
            {ErrorCode.ERROR_UNABLE_FIND_PORT, "Unable to find port."},
            {ErrorCode.ERROR_NO_PORT_SPECIFIED, "No serial port name specified." }
        };
        public static ErrorMessages Messages { get; } = new ErrorMessages();
        public string this[ErrorCode code]
        {
            get
            {
                if (ErrorCodeMap.ContainsKey(code)) { return ErrorCodeMap[code]; }
                return null;
            }
        }

    }


    /// <summary>
    /// Error messages for the known response codes.
    /// See http =//infocenter.nordicsemi.com/index.jsp?topic=%2Fcom.nordic.infocenter.sdk5.v14.2.0%2Fgroup__nrf__dfu__rescodes.html
    /// as well as the response codes at
    /// http =//infocenter.nordicsemi.com/index.jsp?topic=%2Fcom.nordic.infocenter.sdk5.v14.2.0%2Flib_dfu_transport_serial.html
    /// </summary>
    public class ResponseErrorMessages
    {
        private ResponseErrorMessages() { }

        private static Dictionary<ErrorCode, string> ErrorCodeMap = new Dictionary<ErrorCode, string>
        {
            {ErrorCode.ERROR_RSP_INVALID, "Missing or malformed opcode."},
                //  0x01 = success
            {ErrorCode.ERROR_RSP_OP_CODE_NOT_SUPPORTED, "Opcode unknown or not supported."},
            {ErrorCode.ERROR_RSP_INVALID_PARAMETER, "A parameter for the opcode was missing."},
            {ErrorCode.ERROR_RSP_INSUFFICIENT_RESOURCES, "Not enough memory for the data object."},
                // 0x05 should not happen. Bootloaders starting from late 2017 and later will
                // use extended error codes instead.
            {ErrorCode.ERROR_RSP_INVALID_OBJECT, "The data object didn\'t match firmware/hardware, or missing crypto signature, or malformed protocol buffer, or command parse failed."},
                //  0x06 = missing from the spec
            {ErrorCode.ERROR_RSP_UNSUPPORTED_TYPE, "Unsupported object type for create/read operation."},
            {ErrorCode.ERROR_RSP_OPERATION_NOT_PERMITTED, "Cannot allow this operation in the current DFU state."},
                //  0x09 = missing from the spec
            {ErrorCode.ERROR_RSP_OPERATION_FAILED, "Operation failed."},
            //  0x0B = extended error, will read next byte from the response and use it as extended error code
        };
        public static ResponseErrorMessages Messages { get; } = new ResponseErrorMessages();
        public string this[ErrorCode code]
        {
            get
            {
                if (ErrorCodeMap.ContainsKey(code)) { return ErrorCodeMap[code]; }
                return null;
            }
        }

    }

    /// <summary>
    /// Error messages for the known extended error codes.
    /// See http =//infocenter.nordicsemi.com/index.jsp?topic=%2Fcom.nordic.infocenter.sdk5.v14.2.0%2Fgroup__sdk__nrf__dfu__transport.html
    /// </summary>
    public class ExtendedErrorMessages
    {
        private ExtendedErrorMessages() { }

        private static Dictionary<ErrorCode, string> ErrorCodeMap = new Dictionary<ErrorCode, string>
        {
            {ErrorCode.ERROR_EXT_NO_ERROR, "An error happened, but its extended error code hasn\'t been set."},
            {ErrorCode.ERROR_EXT_INVALID_ERROR_CODE, "An error happened, but its extended error code is incorrect."},
                // Extended 0x02 should never happen, because responses 0x02 and 0x03
                // should cover all possible incorrect inputs
            {ErrorCode.ERROR_EXT_WRONG_COMMAND_FORMAT, "The format of the command was incorrect."},
            {ErrorCode.ERROR_EXT_UNKNOWN_COMMAND, "Command successfully parsed, but it is not supported or unknown."},
            {ErrorCode.ERROR_EXT_INIT_COMMAND_INVALID, "The init command is invalid. The init packet either has an invalid update type or it is missing required fields for the update type (for example, the init packet for a SoftDevice update is missing the SoftDevice size field)."},
            {ErrorCode.ERROR_EXT_FW_VERSION_FAILURE, "The firmware version is too low. For an application, the version must be greater than the current application. For a bootloader, it must be greater than or equal to the current version. This requirement prevents downgrade attacks."},
            {ErrorCode.ERROR_EXT_HW_VERSION_FAILURE, "The hardware version of the device does not match the required hardware version for the update."},
            {ErrorCode.ERROR_EXT_SD_VERSION_FAILURE, "The array of supported SoftDevices for the update does not contain the FWID of the current SoftDevice."},
            {ErrorCode.ERROR_EXT_SIGNATURE_MISSING, "The init packet does not contain a signature. This bootloader requires DFU updates to be signed."},
            {ErrorCode.ERROR_EXT_WRONG_HASH_TYPE, "The hash type that is specified by the init packet is not supported by the DFU bootloader."},
            {ErrorCode.ERROR_EXT_HASH_FAILED, "The hash of the firmware image cannot be calculated."},
            {ErrorCode.ERROR_EXT_WRONG_SIGNATURE_TYPE, "The type of the signature is unknown or not supported by the DFU bootloader."},
            {ErrorCode.ERROR_EXT_VERIFICATION_FAILED, "The hash of the received firmware image does not match the hash in the init packet."},
            {ErrorCode.ERROR_EXT_INSUFFICIENT_SPACE, "The available space on the device is insufficient to hold the firmware."},
            {ErrorCode.ERROR_EXT_FW_ALREADY_PRESENT, "The requested firmware to update was already present on the system."}
        };
        public static ExtendedErrorMessages Messages { get; } = new ExtendedErrorMessages();
        public string this[ErrorCode code]
        {
            get
            {
                if (ErrorCodeMap.ContainsKey(code)) { return ErrorCodeMap[code]; }
                return null;
            }
        }

    }

    /**
     * Error class for DFU
     */
    public class DfuException : Exception
    {
        public DfuException(ErrorCode code, string message = null)
            : base(DfuException.GetErrorMessage(code) + (String.IsNullOrWhiteSpace(message) ? String.Empty : $" {message}"))
        {
            this.Code = code;
        }

        public ErrorCode Code { get; private set; }

        public static string GetErrorMessage(ErrorCode code)
        {
            var errorMsg = String.Empty;
            ErrorCode errorType = (ErrorCode)((int)code >> 8);

            System.Diagnostics.Debug.WriteLine($"Error type is {errorType}.");

            errorMsg = ErrorTypes.Messages[errorType];
            if (errorMsg == null)
            {
                throw new Exception("Error type is unknown.");
            }

            errorMsg += " = ";
            switch (errorType)
            {
                case ErrorCode.ERROR_MESSAGE:
                    System.Diagnostics.Debug.WriteLine($"This is an error message.");
                    errorMsg += ErrorMessages.Messages[code];
                    break;
                case ErrorCode.ERROR_MESSAGE_RSP:
                    System.Diagnostics.Debug.WriteLine($"This is a response error message.");
                    errorMsg += ResponseErrorMessages.Messages[code];
                    break;
                case ErrorCode.ERROR_MESSAGE_EXT:
                    System.Diagnostics.Debug.WriteLine($"This is an extended error message.");
                    errorMsg += ExtendedErrorMessages.Messages[code];
                    break;
                default:
                    System.Diagnostics.Debug.WriteLine($"This is an unknown error message.");
                    break;
            }

            return errorMsg;
        }
    }
}
