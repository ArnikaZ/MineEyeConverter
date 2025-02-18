using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MineEyeConverter
{
    public class SerialProvider :IProvider
    {
        public string SerialName { get; set; }
        public int BaudRate { get; set; }
        public Parity PortParity { get; set; }
        public int DataBits { get; set; }
        public StopBits StopBits { get; set; }
      
    }
}
