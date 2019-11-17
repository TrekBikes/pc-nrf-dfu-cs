/*
 * Copyright 2019 - BCycle, LLC
 * Licensed under the MIT license
 * 
 */
namespace Nordic.nRF.DFU
{
    public class FirmwareVersion
    {
        public uint Version { get; set; }
        public uint Addr { get; set; }
        public uint Length { get; set; }
        public string ImageType { get; set; }
    }
}
