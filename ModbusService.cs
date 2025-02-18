using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Topshelf;

namespace ConsoleApp9
{
    public class ModbusService 
    {
        ModbusGateway gateway;
        private string instanceName;

        public ModbusService(string instanceName)
        {
            this.instanceName = instanceName;
        }
        public void Start()
        {

            gateway = new ModbusGateway(instanceName);
            gateway.Start();
        }

        
        public void Stop()
        {
            gateway.Stop();
        }

       
    }
}
