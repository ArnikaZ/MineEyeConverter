using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Topshelf;
using Topshelf.Logging;

namespace MineEyeConverter
{
    internal class SampleService : ServiceControl
    {
        ModbusGateway gateway;
        string instanceName;
       
    

        public SampleService(string name)
        {
            instanceName = name;
        }

        public bool Start(HostControl hostControl)
        {
            try
            {
                gateway = new ModbusGateway(instanceName);
                gateway.Start();
                return true;
            }
            catch (Exception ex)
            {
                // Zaloguj szczegółowe informacje o wyjątku
                Console.WriteLine("Exception in Start: " + ex);
                return false; // lub podejmij inne działanie naprawcze
            }
        }

        public bool Stop(HostControl hostControl)
        {
            gateway.Stop();
            return true;
        }

       
    }
    
}
