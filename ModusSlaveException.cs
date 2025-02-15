using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp9
{
    public class ModbusSlaveException : Exception
    {
        public byte ExceptionCode { get; }
        public ModbusSlaveException(byte exceptionCode)
            : base($"Modbus Slave Exception, code: {exceptionCode}")
        {
            ExceptionCode = exceptionCode;
        }
    }
}
