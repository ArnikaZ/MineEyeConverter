using EasyModbus;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp9
{
    public class ModbusGateway
    {
        private readonly log4net.ILog _log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private ModbusServer _tcpServer; //przez serwer przychodzą żądania od klientów TCP/IP
        private ClientHandler _rtuClient; //obiekt odpowiedzialny za komunikację z urządzeniami Modbus RTU
        private readonly Dictionary<byte, ModbusSlaveDevice> _slaveDevices; //klucz - adres urządzenia, wartość - obiekt slave
        private bool _isRunning; //czy gateway jest aktualnie uruchomiony
        private readonly object _syncLock = new object();
        private readonly Configuration _config;
       

        public ModbusGateway(string instanceName)
        {
            
            _config = ConfigLoader.LoadConfiguration("config.xml");
            var instanceConfig = _config.Instances.FirstOrDefault(i => string.Equals(i.Name, instanceName, StringComparison.OrdinalIgnoreCase));
            if (instanceConfig == null)
            {
                Console.WriteLine($"Nie znaleziono instancji '{instanceName}' w konfiguracji.");
            }
            int listeningPort = instanceConfig.ListeningPort;
            string connectionType = instanceConfig.ConnectionType;
            RtuSettings rtuSettings = instanceConfig.RtuSettings;
            ClientWhiteList whiteList = instanceConfig.ClientWhiteList;
            _slaveDevices = new Dictionary<byte, ModbusSlaveDevice>();


            // Konfiguracja klienta RTU
            if (connectionType.Equals("COM", StringComparison.OrdinalIgnoreCase))
            {
                _rtuClient = new ClientHandler
                {
                    SerialDataProvider = new SerialProvider
                    {
                        SerialName = rtuSettings.PortName,
                        BaudRate = rtuSettings.BaudRate.HasValue ? rtuSettings.BaudRate.Value : 9600,
                        PortParity = ParseParity(rtuSettings.Parity),
                        DataBits = rtuSettings.DataBits.HasValue ? rtuSettings.DataBits.Value : 8,
                        StopBits = rtuSettings.StopBits.HasValue ? ParseStopBits(rtuSettings.StopBits.Value) : StopBits.One
                    }
                };
            }
            else if (connectionType.Equals("RtuOverTcp", StringComparison.OrdinalIgnoreCase))
            {
                _rtuClient = new ClientHandler
                {
                    TcpDataProvider = new TcpProvider
                    {
                        Ip = rtuSettings.IpAddress,
                        Port = rtuSettings.Port.HasValue ? rtuSettings.Port.Value : 503
                    }
                };
            }
            else
            {
                Console.WriteLine($"Nieobsługiwany typ połączenia: {connectionType}");
            }
          

            //wczytanie urządzeń slave dla danej instancji
            if (instanceConfig.SlaveDeviceList != null && instanceConfig.SlaveDeviceList.Slaves != null)
            {
                foreach (var slaveConfig in instanceConfig.SlaveDeviceList.Slaves)
                {
                    byte unitId = (byte)slaveConfig.UnitId;
                    AddSlaveDevice(unitId);
                }
            }
            Console.WriteLine($"dostępne slave devices: ");
            foreach (var slave in _slaveDevices)
            {
                Console.WriteLine($"{slave.Key}: {slave.Value.ToString}");
            }

           
            


            // Konfiguracja serwera TCP
            _tcpServer = new ModbusServer
            {
                LocalIPAddress = IPAddress.Any,
                Port = listeningPort
            };

            // Podpięcie handlerów zdarzeń dla serwera TCP
            _tcpServer.CoilsChanged += HandleCoilsChanged;
            _tcpServer.HoldingRegistersChanged += HandleHoldingRegistersChanged;
            _tcpServer.NumberOfConnectedClientsChanged += HandleClientConnectionChanged;

        }

        private Parity ParseParity(string parity)
        {
            return parity switch
            {
                "None" => Parity.None,
                "Even" => Parity.Even,
                "Odd" => Parity.Odd,
                "Mark" => Parity.Mark,
                "Space" => Parity.Space,
                _ => Parity.None,
            };
        }
        private StopBits ParseStopBits(int stopBits)
        {
            return stopBits switch
            {
                1 => StopBits.One,
                2 => StopBits.Two,
                _ => StopBits.One,
            };
        }

        public void AddSlaveDevice(byte unitId)
        {
            var slave = new ModbusSlaveDevice(unitId);
            _slaveDevices[unitId] = slave;
            _rtuClient.SlaveList.Add(slave);
        }

        public void Start()
        {
            if (_isRunning) //jeśli już uruchomiona, to kończy działanie
                return;

            _isRunning = true;

            // Uruchomienie serwera TCP
            _tcpServer.Listen();
            Console.WriteLine($"Serwer TCP nasłuchuje na porcie {_tcpServer.Port}");
            _log.Info($"Serwer TCP nasłuchuje na porcie {_tcpServer.Port}");



            // Uruchomienie klienta RTU w osobnym wątku
            Task.Run(() => _rtuClient.Start());
            // Uruchomienie synchronizacji w osobnym wątku
            Task.Run(SynchronizeRegisters);
        }

        public void Stop()
        {
            if (!_isRunning)
                return;

            _isRunning = false;
            _tcpServer.StopListening();
            _rtuClient.Stop();
            _log.Info("Zatrzymano bridge TCP/RTU");
        }
        //Metoda jest wywoływana gdy zmienią się stany cewek w serwerze TCP
        //coil: adres pierwszej zmienionej cewki
        //numberOfPoints: liczba kolejnych cewek, które uległy zmianie
        private void HandleCoilsChanged(byte slaveId, int coil, int numberOfPoints)
        {
            try
            {
                lock (_syncLock)
                {
                    
                    // Pobierz dane z serwera TCP
                    bool[] values = new bool[numberOfPoints];
                    for (int i = 0; i < numberOfPoints; i++)
                    {
                        values[i] = _tcpServer.coils[coil];
                    }

                   
                    if (_slaveDevices.ContainsKey(slaveId))
                    {
                        // Zapisz do urządzenia RTU
                        _rtuClient.WriteMultipleCoils(slaveId, (ushort)(coil - 1), values);
                        _log.Debug($"Przesłano zmianę coils do urządzenia {slaveId}, adres początkowy: {coil}, liczba punktów: {numberOfPoints}");
                        Console.WriteLine($"Przesłano zmianę coils do urządzenia {slaveId}, adres początkowy: {coil}, liczba punktów: {numberOfPoints}");
                    }
                }

            }
            catch (Exception ex)
            {
                _log.Error($"Błąd podczas obsługi zmiany coils: {ex.Message}");
                Console.WriteLine($"Błąd podczas obsługi zmiany coils: {ex.Message}");
            }
        }

        private void HandleHoldingRegistersChanged(byte slaveId, int register, int numberOfPoints)
        {
            try
            {
                lock (_syncLock)
                {
                    // Pobierz dane z serwera TCP
                    ushort[] values = new ushort[numberOfPoints];
                    for (int i = 0; i < numberOfPoints; i++)
                    {
                        values[i] = (ushort)_tcpServer.holdingRegisters[register];
                    }

                    if (_slaveDevices.ContainsKey(slaveId))
                    {
                        // Zapisz do urządzenia RTU
                        _rtuClient.WriteMultipleRegisters(slaveId, (ushort)(register - 1), values);
                        _log.Debug($"Przesłano zmianę rejestrów do urządzenia {slaveId}, adres początkowy: {register}, liczba rejestrów: {numberOfPoints}");
                        Console.WriteLine($"Przesłano zmianę rejestrów do urządzenia {slaveId}, adres początkowy: {register}, liczba rejestrów: {numberOfPoints}");
                    }
                }

            }
            catch (Exception ex)
            {
                _log.Error($"Błąd podczas obsługi zmiany rejestrów: {ex.Message}");
                Console.WriteLine($"Błąd podczas obsługi zmiany rejestrów: {ex.Message}");
            }
        }

        private void HandleClientConnectionChanged()
        {
            _log.Info($"Zmiana liczby połączonych klientów TCP. Aktualna liczba: {_tcpServer.NumberOfConnections}");
            Console.WriteLine($"Zmiana liczby połączonych klientów TCP. Aktualna liczba: {_tcpServer.NumberOfConnections}");
        }

        
        private async Task SynchronizeRegisters()
        {
            while (_isRunning)
            {
                try
                {
                    _rtuClient.UpdateParameters(_tcpServer.CurrentUnitIdentifier,
                                        _tcpServer.LastStartingAddress,
                                        _tcpServer.LastQuantity);

                    if (_slaveDevices.TryGetValue(_tcpServer.CurrentUnitIdentifier, out ModbusSlaveDevice slave))
                    {
                        try
                        {

                            // Sprawdzenie długości tablic przed synchronizacją
                            if (slave.Coils == null || slave.Coils.Length == 0)
                            {
                                Console.WriteLine($"Błąd: Slave {slave.UnitId} - brak cewek (Coils).");
                            }
                            else
                            {
                                for (ushort i = 0; i < slave.Coils.Length && i + 1 < _tcpServer.coils.localArray.Length; i++)
                                {
                                    lock (_syncLock)
                                    {
                                        _tcpServer.coils[i + 1] = slave.Coils[i];
                                    }
                                }
                            }

                            if (slave.HoldingRegisters == null || slave.HoldingRegisters.Length == 0)
                            {
                                Console.WriteLine($"Błąd: Slave {slave.UnitId} - brak rejestrów holding.");
                            }
                            else
                            {
                                for (ushort i = 0; i < slave.HoldingRegisters.Length && i + 1 < _tcpServer.holdingRegisters.localArray.Length; i++)
                                {
                                    lock (_syncLock)
                                    {
                                        _tcpServer.holdingRegisters[i + 1] = (short)slave.HoldingRegisters[i];
                                    }
                                }
                            }

                            if (slave.InputRegisters == null || slave.InputRegisters.Length == 0)
                            {
                                Console.WriteLine($"Błąd: Slave {slave.UnitId} - brak rejestrów wejściowych.");
                            }
                            else
                            {
                                for (ushort i = 0; i < slave.InputRegisters.Length && i + 1 < _tcpServer.inputRegisters.localArray.Length; i++)
                                {
                                    lock (_syncLock)
                                    {
                                        _tcpServer.inputRegisters[i + 1] = (short)slave.InputRegisters[i];
                                    }
                                }
                            }
                        }



                        catch (Exception ex)
                        {
                            Console.WriteLine($"Błąd w synchronizacji danych dla slave {slave.UnitId}: {ex.Message}");
                        }

                        // Poczekaj przed następną synchronizacją
                        await Task.Delay(100); // Synchronizacja co 100ms
                    }
                }
                catch (Exception ex)
                {
                    _log.Error($"Błąd podczas synchronizacji: {ex.Message}");
                    await Task.Delay(1000); // W przypadku błędu, poczekaj dłużej
                }
            }
        }


        public void Dispose()
        {
            Stop();
            _rtuClient.Dispose();
            _tcpServer = null;
        }
    }
}