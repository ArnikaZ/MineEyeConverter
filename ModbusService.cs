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
        ModbusTcpServer server;
        private readonly string instanceName;

        public ModbusService(string instanceName)
        {
            this.instanceName = instanceName;
        }
        public void Start()
        {
            Configuration _config = ConfigLoader.LoadConfiguration("config.xml");
            var instanceConfig = _config.Instances.FirstOrDefault(i => string.Equals(i.Name, instanceName, StringComparison.OrdinalIgnoreCase));
            string operationMode = instanceConfig.OperationMode.ToLower();
            if (operationMode=="auto" || operationMode == "manual")
            {
                server = new ModbusTcpServer(instanceName, true);
                server.Start();
            }
            else if (operationMode == "learning")
            {
                LearningModeHandler lm = new LearningModeHandler(instanceName);
                List<SlaveConfiguration> discoveredConfigs = lm.DiscoverSlaves();
                lm.SaveConfigurationToXml(discoveredConfigs);
            }

        }

        
        public void Stop()
        {
            server.Stop();
        }

       
    }
}
