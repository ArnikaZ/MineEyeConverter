using EasyModbus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp9
{
    public class ModbusTcpServer
    {
        private readonly log4net.ILog _log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private ModbusServer _modbusServer;
        private List<ModbusClientAccount> WhiteList { get; set; }
        public bool IsChanged { get; private set; }
        public int Port { get; set; }
        


        public ModbusTcpServer(bool useWhiteList, int port = 502)
        {
            Port = port;
            if (useWhiteList)
            {
                WhiteList = new List<ModbusClientAccount>();
            }
            IsChanged = false;
            _modbusServer = new ModbusServer
            {
                LocalIPAddress = IPAddress.Any,
                Port = port,
                FunctionCode1Disabled = false,
                FunctionCode2Disabled = false,
                FunctionCode3Disabled = false,
                FunctionCode4Disabled = false,
                FunctionCode5Disabled = false,
                FunctionCode6Disabled = false,
                FunctionCode15Disabled = false,
                FunctionCode16Disabled = false
            };

        }

        public void Start()
        {
            _modbusServer.Listen();
            Console.WriteLine($"Server nasłuchuje na porcie {Port}");
            _modbusServer.NumberOfConnectedClientsChanged += _modbusServer_NumberOfConnectedClientsChanged;
        }

        private void _modbusServer_NumberOfConnectedClientsChanged()
        {
            Console.WriteLine("klient połączony!");
        }
    }
}
