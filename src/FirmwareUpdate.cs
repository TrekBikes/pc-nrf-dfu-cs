/*
 * Copyright 2019 - BCycle, LLC
 * Licensed under the MIT license
 * 
 */
namespace Nordic.nRF.DFU
{
    public class FirmwareUpdate
    {
        public byte[] InitPacket { get; set; }
        public byte[] FirmwareImage { get; set; }
    }
}
