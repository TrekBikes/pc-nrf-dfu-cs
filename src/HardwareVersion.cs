/*
 * Copyright 2019 - BCycle, LLC
 * Licensed under the MIT license
 * 
 */
namespace Nordic.nRF.DFU
{
    public class HardwareVersion
    {
        public int Part { get; set; }
        public int Variant { get; set; }
        public HardwareMemoryConfig Memory { get; set; }
    }
}