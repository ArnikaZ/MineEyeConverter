using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp9
{
    public class ModbusRegister
    {
        public int SlaveId { get; set; }
        public ushort StartAddress { get; set; }
        public ushort Quantity { get; set; }
        public bool IsActive { get; set; }
        public ushort functionCode { get; set; }
        public string AccessMode { get; set; }
    }
}
