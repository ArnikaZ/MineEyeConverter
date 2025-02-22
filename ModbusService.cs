using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Topshelf;

namespace MineEyeConverter
{
    public class ModbusService 
    {
        ModbusTcpServer gateway;
        private string instanceName;

        public ModbusService(string instanceName)
        {
            this.instanceName = instanceName;
        }
        public void Start()
        {
            Configuration _config = ConfigLoader.LoadConfiguration("config.xml");
            var instanceConfig = _config.Instances.FirstOrDefault(i => string.Equals(i.Name, instanceName, StringComparison.OrdinalIgnoreCase));
            string operationMode = instanceConfig.OperationMode;
            if (operationMode=="Auto" || operationMode == "Manual")
            {
                gateway = new ModbusTcpServer(instanceName);
                gateway.Start();
            }
            else if (operationMode == "Learning")
            {
                LearningModeHandler lm = new LearningModeHandler(instanceName);
                List<SlaveConfiguration> discoveredConfigs = lm.DiscoverSlaves();
                lm.SaveConfigurationToXml(discoveredConfigs);
            }

        }

        
        public void Stop()
        {
            gateway.Stop();
        }

       
    }
}
