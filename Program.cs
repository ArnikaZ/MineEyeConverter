using CommandLine;
using System.IO.Ports;
using Topshelf;
using System.ServiceProcess;
using System.Reflection;
using System.Configuration.Install;
using System.Diagnostics;
using Serilog;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Configuration.CommandLine;
using MineEyeConverter;

[assembly: log4net.Config.XmlConfigurator(Watch =true)]
namespace MineEyeConverter
{
   
    internal class Program
    {
        private static readonly log4net.ILog _log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        
        static void Main(string[] args)
        {
            // _log.Info("Application starting");
            // var config = new ConfigurationBuilder()
            //.SetBasePath(AppContext.BaseDirectory)
            //.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            //.AddCommandLine(args)
            //.Build();

            // _log.Info($"Configuration loaded succesfully");

            // var instanceName = config["Service:Name"] ?? config["name"];
            // var serviceDescription = config["Service:Description"] ?? "TCP <=> RTU Converter";

            // var exitCode = HostFactory.Run(x =>
            // {
            //     x.AddCommandLineDefinition("name", f => { instanceName = f; });
            //     x.ApplyCommandLine();
            //     x.Service<ModbusService>(s =>
            //     {
            //         s.ConstructUsing(modbusService => new ModbusService(instanceName));
            //         s.WhenStarted(modbusService => modbusService.Start());
            //         s.WhenStopped(modbusService => modbusService.Stop());
            //     });

            //     x.RunAsLocalSystem();
            //     x.SetServiceName(instanceName);
            //     x.SetDisplayName(instanceName);
            //     x.SetDescription(serviceDescription);
            //     x.StartAutomatically();
            // });


            // int exitCodeValue = (int)Convert.ChangeType(exitCode, exitCode.GetTypeCode());
            // Environment.ExitCode = exitCodeValue;



            //z konsoli
            var server = new ModbusTcpServer("Przenosnik15", false);
            server.Start();

            Console.WriteLine("Naciśnij dowolny klawisz, aby zakończyć...");
            Console.ReadKey();

            server.Stop();

        }


    }

}




//    //LearningModeHandler lm = new LearningModeHandler("Szyb1Poziom950Przenosnik2");
//    //List<SlaveConfiguration> discoveredConfigs = lm.DiscoverSlaves();
//    //lm.SaveConfigurationToXml(discoveredConfigs);
//    //Console.ReadKey();


//}