using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Topshelf;

namespace ConsoleApp9
{
    public class ModbusService : ServiceControl
    {
        ModbusGateway gateway;
        private string instanceName;

        public ModbusService()
        {
            // this.instanceName = instanceName;
        }
        //public void Start()
        //{

        //    gateway = new ModbusGateway("Szyb1");
        //    gateway.Start();
        //}

        public bool Start(HostControl hostControl)
        {
            gateway = new ModbusGateway("Szyb1");
            gateway.Start();
            return true;
        }

        //public void Stop()
        //{
        //    gateway.Stop();
        //}

        public bool Stop(HostControl hostControl)
        {
            gateway.Stop();
            return true;
        }
    }
}
