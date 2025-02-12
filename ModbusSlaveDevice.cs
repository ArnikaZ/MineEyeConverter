using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp9
{
    public class ModbusSlaveDevice
    {
        public byte UnitId { get; set; }
        public ushort[] HoldingRegisters { get; set; }
        public ushort[] InputRegisters { get; set; }

        public bool[] Coils { get; set; }
        public Slave SlaveConfiguration { get; set; }

        public ModbusSlaveDevice()
        {
            HoldingRegisters = new ushort[65536];
            InputRegisters = new ushort[65536];
            Coils = new bool[65536];
        }

        public ModbusSlaveDevice(byte unitId)
        {
            UnitId = unitId;
            HoldingRegisters = new ushort[65536];
            InputRegisters = new ushort[65536];
            Coils = new bool[65536];
        }
        public void InitializeFromConfig(Slave config)
        {
            if (config == null) Console.WriteLine("Konfiguracja slave jest null");
            SlaveConfiguration = config;
            UnitId = (byte)config.UnitId;
        }
    }
}
