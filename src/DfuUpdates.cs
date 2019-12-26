/**
 * Copyright 2019 - BCycle, LLC
* Adapted From: DfuUpdates.js
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

using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace Nordic.nRF.DFU
{
    /**
    * Represents a set of DFU updates.
    *
    * A DFU update is an update of either:
    * - The bootloader
    * - The SoftDevice
    * - The user application
    * - The bootloader plus the SoftDevice
    *
    * From the technical side, a DFU update is a tuple of an init packet and
    * a binary blob. Typically, the init packet is a protocol buffer ("protobuf"
    * or "pbf" for short) indicating the kind of memory region to update,
    * the size to update, whether to reset the device, extra checksums, which
    * kind of nRF chip family this update is meant for, and maybe a crypto signature.
    *
    * Nordic provides a default pbf definition, and *usually* a DFU update will use
    * that. However, there are use cases for using custom pbf definitions (which
    * imply using a custom DFU bootloader). Thus, this code does NOT perform any
    * checks on the init packet (nor on the binary blob, for that matter).
    *
    * An instance of DfuUpdates might be shared by several operations using different
    * transports at the same time.
    *
*/
    public class DfuUpdates
    {
        private static JsonSerializerSettings _serializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
        };

        public FirmwareUpdate[] Updates { get; private set; }

        public DfuUpdates(IEnumerable<FirmwareUpdate> updates)
        {
            // TODO: Sanity checks on incoming updates array
            Updates = updates?.ToArray();
        }

        /**
         * Instantiates a set of DfuUpdates given the *path* of a .zip file.
         * That .zip file is expected to have been created by nrfutil, having
         * a valid manifest.
         *
         * This requires your environment to have access to the local filesystem.
         * (i.e. works in nodejs, not in a web browser)
         *
         * @param {String} path The full file path to the .zip file
         * @return {Promise} A Promise to an instance of DfuUpdates
         */
        public static async Task<DfuUpdates> FromZipFile(string zipFilePath)
        {
            #if NETCORE
            var fileBytes = await System.IO.File.ReadAllBytesAsync(zipFilePath);
            #else
            var fileBytes = System.IO.File.ReadAllBytes(zipFilePath);
            #endif

            return await FromZipFile(fileBytes);
        }

        /**
         * Instantiates a set of DfuUpdates given the *contents* of a .zip file,
         * as an ArrayBuffer, a Uint8Array, Buffer, Blob, or other data type accepted by
         * [JSZip](https://stuk.github.io/jszip/documentation/api_jszip/load_async.html).
         * That .zip file is expected to have been created by nrfutil, having
         * a valid manifest.
         *
         * @param {String} zipBytes The full file path to the .zip file
         * @return {Promise} A Promise to an instance of DfuUpdates
         */
        public static async Task<DfuUpdates> FromZipFile(byte[] fileData)
        {
            var updates = new List<FirmwareUpdate>();
            using (var byteStream = new MemoryStream(fileData))
            {
                using (var zipStream = new ZipArchive(byteStream, System.IO.Compression.ZipArchiveMode.Read, true))
                {
                    var manifestEntry = zipStream.GetEntry("manifest.json");
                    FirmwareManifest manifest = null;
                    using (var manifestStream = manifestEntry.Open())
                    {
                        var manifestData = new byte[manifestEntry.Length];
                        await manifestStream.ReadAsync(manifestData, 0, manifestData.Length);

                        manifest = JsonConvert.DeserializeObject<FirmwareManifest>(System.Text.Encoding.UTF8.GetString(manifestData), _serializerSettings);
                    }

                    if (manifest.Manifest.Application != null)
                    {
                        updates.Add(await GetFirmwareUpdate(manifest.Manifest.Application, zipStream));
                    }

                    if (manifest.Manifest.Bootloader != null)
                    {
                        updates.Add(await GetFirmwareUpdate(manifest.Manifest.Bootloader, zipStream));
                    }

                    if (manifest.Manifest.Softdevice != null)
                    {
                        updates.Add(await GetFirmwareUpdate(manifest.Manifest.Softdevice, zipStream));
                    }

                    if (manifest.Manifest.SoftdeviceBootloader != null)
                    {
                        updates.Add(await GetFirmwareUpdate(manifest.Manifest.SoftdeviceBootloader, zipStream));
                    }

                }
            }

            return new DfuUpdates(updates);
        }

        private static async Task<FirmwareUpdate> GetFirmwareUpdate(FirmwareManifestEntry manifestEntry, ZipArchive zipStream)
        {
            var firmwareUpdate = new FirmwareUpdate();

            var initEntry = zipStream.GetEntry(manifestEntry.DatFilePath);
            if (initEntry != null)
            {
                firmwareUpdate.InitPacket = new byte[initEntry.Length];
                using (var initStream = initEntry.Open())
                {
                    await initStream.ReadAsync(firmwareUpdate.InitPacket, 0, firmwareUpdate.InitPacket.Length);
                }
            }

            var imageEntry = zipStream.GetEntry(manifestEntry.BinFilePath);
            if (imageEntry != null)
            {
                firmwareUpdate.FirmwareImage = new byte[imageEntry.Length];
                using (var imageStream = imageEntry.Open())
                {
                    await imageStream.ReadAsync(firmwareUpdate.FirmwareImage, 0, firmwareUpdate.FirmwareImage.Length);
                }
            }

            return firmwareUpdate;
        }
    }
}
