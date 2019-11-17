/*
 * Copyright 2019 - BCycle, LLC
 * Licensed under the MIT license
 * 
 */
using Newtonsoft.Json;

namespace Nordic.nRF.DFU
{
    public class FirmwareManifest
    {
        public FirmwareManifestEntries Manifest { get; set; }
    }

    public class FirmwareManifestEntries
    {
        public FirmwareManifestEntry Bootloader { get; set; }
        public FirmwareManifestEntry Softdevice { get; set; }
        public FirmwareManifestEntry Application { get; set; }
        [JsonProperty(PropertyName = "softdevice_bootloader")]
        public FirmwareManifestEntry SoftdeviceBootloader { get; set; }
    }

    public class FirmwareManifestEntry
    {
        [JsonProperty(PropertyName = "bin_file")]
        public string BinFilePath { get; set; }

        [JsonProperty(PropertyName = "dat_file")]
        public string DatFilePath { get; set; }
    }
}
