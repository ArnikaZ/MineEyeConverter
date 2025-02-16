﻿using System.IO.Ports;

namespace ConsoleApp9
{
    internal class Program
    {
        static void Main(string[] args)
        {

            //var bridge = new ModbusGateway("Szyb1Poziom950Przenosnik2");
            //bridge.Start();

            //Console.WriteLine("Naciśnij dowolny klawisz, aby zakończyć...");
            //Console.ReadKey();

            //bridge.Stop();

            LearningModeHandler lm = new LearningModeHandler("Szyb1Poziom950Przenosnik2");
            List<SlaveConfiguration> discoveredConfigs = lm.DiscoverSlaves();
            lm.SaveConfigurationToXml(discoveredConfigs);
            Console.ReadKey();



        }
    }
}
