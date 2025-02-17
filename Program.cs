using CommandLine;
using System.IO.Ports;
using Topshelf;
using System.ServiceProcess;
using System.Reflection;
using System.Configuration.Install;
using System.Diagnostics;
using Serilog;


namespace ConsoleApp9
{
   
    internal class Program
    {

        static int Main(string[] args)
        {
            string name = null;
            // Domyślne ustawienia
            bool throwOnStart = false;
            string throwOnStartValue = null;
            bool throwOnStop = false;
            bool throwUnhandled = false;

            return (int)HostFactory.Run(x =>
            {
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .CreateLogger();



                // Używamy definicji, by pobrać wartość z linii poleceń
                x.AddCommandLineDefinition("name", f => { name = f; });
                x.ApplyCommandLine();
                x.SetServiceName(name);
                x.SetDisplayName(name);
                x.Service(settings => new SampleService(throwOnStart, throwOnStartValue, throwOnStop, throwUnhandled), s =>
                {
                    s.BeforeStartingService(_ => Console.WriteLine("BeforeStart"));
                    s.BeforeStoppingService(_ => Console.WriteLine("BeforeStop"));
                });

                x.SetStartTimeout(TimeSpan.FromSeconds(10));
                x.SetStopTimeout(TimeSpan.FromSeconds(10));

                x.EnableServiceRecovery(r =>
                {
                    r.RestartService(3);
                    r.RunProgram(7, "ping google.com");
                    r.RestartComputer(5, "message");

                    r.OnCrashOnly();
                    r.SetResetPeriod(2);
                });

                x.OnException((exception) =>
                {
                    Console.WriteLine("Exception thrown - " + exception.Message);
                });
            });
        }



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




        //static void Main(string[] args)
        //{
        //    //z konsoli
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

        //var exitCode = HostFactory.Run(x =>
        //{
        //    x.Service<ModbusService>(s =>
        //    {
        //        s.ConstructUsing(modbusService => new ModbusService());
        //        s.WhenStarted(modbusService => modbusService.Start());
        //        s.WhenStopped(modbusService => modbusService.Stop());
        //    });

        //    x.RunAsLocalSystem();
        //    x.SetServiceName(instanceName);
        //    x.SetDisplayName(instanceName);
        //    x.SetDescription("TCP <=> RTU Converter");
        //});


        //int exitCodeValue = (int)Convert.ChangeType(exitCode, exitCode.GetTypeCode());
        //Environment.ExitCode = exitCodeValue;






    }

}
