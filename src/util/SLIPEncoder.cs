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

using System.Linq;

namespace Nordic.nRF.DFU.Util
{
    public static class SLIPEncoder
    {
        /**
         * SLIP encodes a byte array.
         *
         * @param {Array-like} bytes a Uint8Array, Node.js Buffer, ArrayBuffer, or [] containing raw bytes
         * @param {Object} options encoder options
         * @return {Uint8Array} the encoded copy of the data
         */
        public static byte[] Encode(byte[] bytes/*, options*/)
        {
            var bufferPadding = 4; // Will be rounded to the nearest 4 bytes
            var bufLen = (bytes.Length + bufferPadding) & ~0x03;
            var encoded = new byte[bufLen];
            var j = 1;

            encoded[0] = SLIPConstants.END;
            for (var i = 0; i < bytes.Length; i++)
            {
                // We always need enough space for two value bytes plus a trailing END.
                if (j > encoded.Length - 3)
                {
                    var tmp = new byte[encoded.Length * 2];
                    encoded.CopyTo(tmp, 0);
                    encoded = tmp;
                }

                var val = bytes[i];
                if (val == SLIPConstants.END)
                {
                    encoded[j] = SLIPConstants.ESC;
                    j += 1;
                    val = SLIPConstants.ESC_END;
                }
                else if (val == SLIPConstants.ESC)
                {
                    encoded[j] = SLIPConstants.ESC;
                    j += 1;
                    val = SLIPConstants.ESC_ESC;
                }

                encoded[j] = val;
                j += 1;
            }

            encoded[j] = SLIPConstants.END;
            return encoded.Take(j + 1).ToArray();
        }
    }
}

