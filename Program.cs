using System.IO.Ports;

namespace ConsoleApp9
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var bridge = new ModbusGateway("Szyb1Poziom950Przenosnik2");

            

            // Uruchom bridge
            bridge.Start();
            
            Console.WriteLine("Naciśnij dowolny klawisz, aby zakończyć...");
            Console.ReadKey();

            bridge.Stop();




        }
    }
}
