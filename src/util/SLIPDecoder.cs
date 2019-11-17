/*
 * Copyright 2019 - BCycle, LLC
 * Licensed under the MIT license
 * 
 * Adapted From:
 *
 * slip.js: A plain JavaScript SLIP implementation that works in both the browser and Node.js
 *
 * Copyright 2017, Colin Clark
 * Licensed under the MIT and GPL 3 licenses.
 */
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Nordic.nRF.DFU.Util
{
    /**
     * Creates a new SLIP Decoder.
     * @constructor
     *
     * @param {Function} onMessage a callback function that will be invoked
     *                             when a message has been fully decoded
     * @param {Number} maxBufferSize the maximum size of a incoming message
     *                               larger messages will throw an error
     */
    public class SLIPDecoder
    {
        private readonly Func<byte[], Task> _onMessage;
        private readonly int _maxMessageSize;
        private readonly int _bufferSize;
        private readonly Action<byte[], string> _onError;

        private byte[] _msgBuffer;
        private int _msgBufferIdx;
        private bool _escape = false;

        public SLIPDecoder(
            Func<byte[], Task> onMessage,
            int maxMessageSize = 10485760,
            int bufferSize = 1024,
            Action<byte[], string> onError = null
            )
        {
            _maxMessageSize = maxMessageSize; // Defaults to 10 MB.
            _bufferSize = bufferSize; // Message buffer defaults to 1 KB.
            _msgBuffer = new byte[bufferSize];
            _msgBufferIdx = 0;
            _onMessage = onMessage;
            _onError = onError;
            _escape = false;
        }

        /**
         * Decodes a SLIP data packet.
         * The onMessage callback will be invoked when a complete message has been decoded.
         *
         * @param {Array-like} bytes an incoming stream of bytes
         * @returns {Uint8Array} decoded msg
         */
        public async Task<byte[]> DecodeBytes(byte[] bytes)
        {
            var msg = new byte[0];

            for (var i = 0; i < bytes.Length; i += 1)
            {
                var val = bytes[i];

                if (this._escape)
                {
                    if (val == SLIPConstants.ESC_ESC)
                    {
                        val = SLIPConstants.ESC;
                    }
                    else if (val == SLIPConstants.ESC_END)
                    {
                        val = SLIPConstants.END;
                    }
                }
                else
                {
                    if (val == SLIPConstants.ESC)
                    {
                        _escape = true;
                        // eslint-disable-next-line no-continue
                        continue;
                    }
                    if (val == SLIPConstants.END)
                    {
                        msg = await HandleEnd();
                        // eslint-disable-next-line no-continue
                        continue;
                    }
                }

                var more = AddByte(val);
                if (!more)
                {
                    HandleMessageMaxError();
                }
            }

            return msg;
        }

        private void HandleMessageMaxError()
        {
            _onError?.Invoke(
                _msgBuffer,
                $"The message is too large; the maximum message size is {_maxMessageSize / 1024}KB. Use a larger maxMessageSize if necessary."
            );

            // Reset everything and carry on.
            _msgBufferIdx = 0;
            _escape = false;
        }

        // Unsupported, non-API method.
        private bool AddByte(byte val)
        {
            if (_msgBufferIdx > _msgBuffer.Length - 1)
            {
                var tmp = new byte[_msgBuffer.Length * 2];
                _msgBuffer.CopyTo(tmp, 0);
                _msgBuffer = tmp;
            }

            _msgBuffer[_msgBufferIdx++] = val;
            _escape = false;

            return _msgBuffer.Length < _maxMessageSize;
        }

        // Unsupported, non-API method.
        private async Task<byte[]> HandleEnd()
        {
            if (_msgBufferIdx == 0)
            {
                return null; // Toss opening END byte and carry on.
            }

            var msg = _msgBuffer.Take(_msgBufferIdx).ToArray();
            await _onMessage?.Invoke(msg);

            // Clear our pointer into the message buffer.
            _msgBufferIdx = 0;

            return msg;
        }
    }

}
