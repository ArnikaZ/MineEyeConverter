using CommandLine;
using System.IO.Ports;
using Topshelf;
using System.ServiceProcess;
using System.Reflection;
using System.Configuration.Install;
using System.Diagnostics;
using Serilog;
using System.Xml.Linq;


namespace ConsoleApp9
{
   
    internal class Program
    {

        //static int Main(string[] args)
        //{
        //    string name = null;

        //    return (int)HostFactory.Run(x =>
        //    {
        //        // Używamy definicji, by pobrać wartość z linii poleceń
        //        x.AddCommandLineDefinition("name", f => { name = f; });
        //        x.ApplyCommandLine();

        //        if (string.IsNullOrEmpty(name))
        //        {
        //            Console.WriteLine("Service name must be provided.");
        //            name = "sth";
        //        }

        //        x.SetServiceName(name);
        //        x.SetDisplayName(name);
        //        x.Service(settings => new SampleService(name));

        //        x.OnException((exception) =>
        //        {
        //            Console.WriteLine("Exception thrown - " + exception.Message);
        //        });
        //    });
        //}



        //string instanceName = "defaultservicename";
        //return (int)HostFactory.Run(x =>
        //{
        //    Log.Logger = new LoggerConfiguration()
        //    .MinimumLevel.Debug()
        //    .CreateLogger();


        //    x.Service(settings => new ModbusService(), s =>
        //    {
        //        s.BeforeStartingService(_ => instanceName = args.Length > 0 ? args[0] : "DefaultInstance");
        //    });
        //    x.SetStartTimeout(TimeSpan.FromSeconds(10));
        //    x.SetStopTimeout(TimeSpan.FromSeconds(10));
        //    x.AddCommandLineDefinition("name", v => instanceName = v);
        //    x.SetServiceName(instanceName);
        //});




        static void Main(string[] args)
        {
            
            string instanceName = null;

            var exitCode = HostFactory.Run(x =>
            {
                x.AddCommandLineDefinition("name", f => { instanceName = f; });
                x.ApplyCommandLine();
                x.Service<ModbusService>(s =>
                {
                    s.ConstructUsing(modbusService => new ModbusService(instanceName));
                    s.WhenStarted(modbusService => modbusService.Start());
                    s.WhenStopped(modbusService => modbusService.Stop());
                });

                x.RunAsLocalSystem();
                x.SetServiceName(instanceName);
                x.SetDisplayName(instanceName);
                x.SetDescription("TCP <=> RTU Converter");
                x.StartAutomatically();
            });


            int exitCodeValue = (int)Convert.ChangeType(exitCode, exitCode.GetTypeCode());
            Environment.ExitCode = exitCodeValue;



        }


    }

}

//z konsoli
//    var bridge = new ModbusGateway("Przenosnik1");
//    bridge.Start();

//    Console.WriteLine("Naciśnij dowolny klawisz, aby zakończyć...");
//    Console.ReadKey();

//    bridge.Stop();

//    //LearningModeHandler lm = new LearningModeHandler("Szyb1Poziom950Przenosnik2");
//    //List<SlaveConfiguration> discoveredConfigs = lm.DiscoverSlaves();
//    //lm.SaveConfigurationToXml(discoveredConfigs);
//    //Console.ReadKey();
//}