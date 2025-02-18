using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MineEyeConverter
{
    public class SlaveConfiguration
    {
        public byte SlaveId { get; set; }
        public List<ModbusRegister> HoldingRegisters { get; set; } = new List<ModbusRegister>();
        public List<ModbusRegister> InputRegisters { get; set; } = new List<ModbusRegister>();
        public List<ModbusRegister> Coils { get; set; } = new List<ModbusRegister>();
    }
}
