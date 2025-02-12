using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp9
{
    public class TcpProvider :IProvider
    {
        public string Ip { get; set; }
        public int Port { get; set; }
       
    }
}
