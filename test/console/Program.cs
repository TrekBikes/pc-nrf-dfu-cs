/*
 * Copyright 2019 - BCycle, LLC
 * Licensed under the MIT license
 * 
 */
using System;
using System.Threading.Tasks;

namespace Nordic.nRF.DFU.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                System.Console.WriteLine(@"
Usage:
DFUConsole.exe <serialport> <zip filename>       
");
                System.Console.WriteLine("Hit 'Enter' to exit...");
                System.Console.ReadLine();
                return;
            }

            if (!System.IO.File.Exists(args[1]))
            {
                System.Console.WriteLine($"Unable to locate specified DFU update file: {args[1]}");
                System.Console.WriteLine("Hit 'Enter' to exit...");
                System.Console.ReadLine();
                return;
            }

            try
            {
                DoUpdate(args[0], args[1]).Wait();
            }
            catch(Exception ex)
            {
                System.Console.WriteLine($"Oops, something went horribly wrong... {ex.Message} - {ex.StackTrace}");
            }

            System.Console.WriteLine("Hit 'Enter' to exit...");
            System.Console.ReadLine();
        }
        
        private static async Task DoUpdate(string serialPort, string updateFile)
        {
            System.Console.WriteLine($"Setting up serial transport on {serialPort}...");
            var transport = new DfuTransportSerial(serialPort);

            System.Console.WriteLine($"Reading firmware update file at {updateFile}...");
            var updates = await DfuUpdates.FromZipFile(updateFile);

            System.Console.WriteLine("Starting DFU operation, please ensure the device is plugged in and in DFU mode, then hit 'Enter' to start...");
            var operation = new DfuOperation(updates, transport, false);

            System.Console.ReadLine();
            await operation.Start(true);

            System.Console.WriteLine("DFU operation complete...");
        }
    }
}
