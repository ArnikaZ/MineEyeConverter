using EasyModbus;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MineEyeConverter
{
    public class ModbusTcpServer
    {
        private readonly log4net.ILog _log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private ModbusServer _tcpServer; //przez serwer przychodzą żądania od klientów TCP/IP
        private ClientHandler _rtuClient; //obiekt odpowiedzialny za komunikację z urządzeniami Modbus RTU
        private readonly Dictionary<byte, ModbusSlaveDevice> _slaveDevices; //klucz - adres urządzenia, wartość - obiekt slave
        private bool _isRunning; //czy gateway jest aktualnie uruchomiony
        private readonly object _syncLock = new object();
        public readonly Configuration _config;

        public IOperationModeHandler operationModeHandler;
        //ClientWhiteList whiteList;
        private List<Client> modbusClientAccounts { get; set; }
        
        public ModbusTcpServer(string instanceName, bool useWhiteList=false)
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
            
            
            
            _slaveDevices = new Dictionary<byte, ModbusSlaveDevice>();

            // Odczyt trybu pracy z konfiguracji
            string operationMode = instanceConfig.OperationMode;
            

            // Wybór handlera trybu na podstawie konfiguracji
            switch (operationMode.ToLower())
            {
                case "auto":
                    operationModeHandler = new AutoModeHandler();
                    break;
                case "manual":
                    operationModeHandler = new ManualModeHandler();
                    break;
                default:
                    Console.WriteLine("Nieznany tryb pracy: " + operationMode);
                    break;
            }


            // Konfiguracja klienta RTU
            if (connectionType.Equals("COM", StringComparison.OrdinalIgnoreCase))
            {
                _rtuClient = new ClientHandler(operationModeHandler)
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
                _rtuClient = new ClientHandler(operationModeHandler)
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
            

            modbusClientAccounts = instanceConfig.ClientWhiteList.Clients;
            

            // Konfiguracja serwera TCP
            _tcpServer = new ModbusServer
            { 
               LocalIPAddress=IPAddress.Any,
                Port = listeningPort,
                UseWhiteList=useWhiteList,
               WhiteList=modbusClientAccounts,
                FunctionCode1Disabled = false,
                FunctionCode2Disabled = false,
                FunctionCode3Disabled = false,
                FunctionCode4Disabled = false,
                FunctionCode5Disabled = false,
                FunctionCode6Disabled = false,
                FunctionCode15Disabled = false,
                FunctionCode16Disabled = false
            };
            
            // Podpięcie handlerów zdarzeń dla serwera TCP
            
            _tcpServer.CoilsChanged += HandleCoilsChanged;
            _tcpServer.HoldingRegistersChanged += HandleHoldingRegistersChanged;
            _tcpServer.NumberOfConnectedClientsChanged += HandleClientConnectionChanged;

            _tcpServer.OperationModeHandler = operationModeHandler;
            

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
        //Metoda jest wywoływana gdy zmienią się stany cewek w ModbusPoll
        //coil: adres pierwszej zmienionej cewki
        //numberOfPoints: liczba kolejnych cewek, które uległy zmianie
        private void HandleCoilsChanged(byte slaveId, int coil, int numberOfPoints)
        {
            try
            {
                lock (_syncLock)
                {

                    operationModeHandler.HandleCoilsChanged(slaveId, coil, numberOfPoints, _tcpServer, _rtuClient, _slaveDevices);

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
                    operationModeHandler.HandleHoldingRegistersChanged(slaveId, register, numberOfPoints, _tcpServer, _rtuClient, _slaveDevices);
                    
                   
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
           // Console.WriteLine($"Zmiana liczby połączonych klientów TCP. Aktualna liczba: {_tcpServer.NumberOfConnections}");
            
            

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
                                operationModeHandler.Synchronize(slave, _tcpServer);

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