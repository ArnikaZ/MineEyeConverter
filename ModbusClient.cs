﻿using NModbus.IO;
using NModbus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MineEyeConverter
{
    public class ModbusClient:IDisposable
    {
        /// <summary>
        /// Client class for communicating with UGS via RTU over TCP.
        /// </summary>
        private readonly log4net.ILog _log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private readonly string _ipAddress;
        private readonly int _port;
        private readonly Dictionary<byte, ModbusSlaveDevice> _slaveDevices;
        private TcpClient _tcpClient;
        private TcpClientAdapter _tcpClientAdapter;
        private IModbusMaster _master;
        private readonly ModbusFactory _factory;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _pollingTask;
        private readonly int _pollingInterval;
        private bool _isRunning;
        private bool _isDisposed;

        /// <summary>
        /// Gets the collection of slave devices managed by this client
        /// </summary>
        public Dictionary<byte, ModbusSlaveDevice> SlaveDevices => _slaveDevices;

        /// <summary>
        /// Initializes a new instance of the ModbusClient class for UGS communication
        /// </summary>
        /// <param name="ipAddress">IP address of the UGS</param>
        /// <param name="port">TCP port of the UGS</param>
        /// <param name="pollingInterval">Interval in milliseconds between polling cycles</param>
        public ModbusClient(string ipAddress, int port, int pollingInterval = 1000)
        {
            _ipAddress = ipAddress;
            _port = port;
            _pollingInterval = pollingInterval;
            _slaveDevices = new Dictionary<byte, ModbusSlaveDevice>();
            _factory = new ModbusFactory();
        }

        /// <summary>
        /// Adds a slave device to be managed by this client
        /// </summary>
        /// <param name="unitId">The Modbus unit ID of the slave device</param>
        /// <returns>The added ModbusSlaveDevice instance</returns>
        public ModbusSlaveDevice AddSlaveDevice(byte unitId)
        {
            if (_slaveDevices.ContainsKey(unitId))
            {
                _log.WarnFormat("Slave device with ID {0} already exists", unitId);
                return _slaveDevices[unitId];
            }

            var slave = new ModbusSlaveDevice(unitId);
            _slaveDevices.Add(unitId, slave);
            _log.InfoFormat("Added slave device with ID {0}", unitId);
            return slave;
        }


        public void Start()
        {
            if (_isRunning)
                return;

            _isRunning = true;
            _cancellationTokenSource = new CancellationTokenSource();

            _pollingTask = Task.Run(() => PollDevicesAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);

            _log.InfoFormat("Started ModbusClient communication with UGS at {0}:{1}", _ipAddress, _port);
        }


        public void Stop()
        {
            if (!_isRunning)
                return;

            _isRunning = false;
            _cancellationTokenSource?.Cancel();

            try
            {
                _pollingTask?.Wait(5000);
            }
            catch (AggregateException)
            {
            }

            DisconnectFromUGS();

            _log.Info("Stopped ModbusClient communication with UGS");
        }

        #region Holding Registers Methods
        /// <summary>
        /// Reads holding registers from a specific slave device
        /// </summary>
        /// <param name="unitId">The unit ID of the slave device</param>
        /// <param name="startAddress">Starting address of registers to read</param>
        /// <param name="count">Number of registers to read</param>
        /// <returns>Array of register values</returns>
        public ushort[] ReadHoldingRegisters(byte unitId, ushort startAddress, ushort count)
        {
            if (!_isRunning)
                throw new InvalidOperationException("Client is not running.");

            try
            {
                EnsureConnected();

                var data = _master.ReadHoldingRegisters(unitId, startAddress, count);

                // If slave device registered, update its data
                if (_slaveDevices.TryGetValue(unitId, out var device))
                {
                    Array.Copy(data, 0, device.HoldingRegisters, startAddress, count);
                    _log.DebugFormat("Updated holding registers for device {0}, starting at {1}, count {2}", unitId, startAddress, count);
                }

                return data;
            }
            catch (Exception ex)
            {
                _log.ErrorFormat("Error reading holding registers from device {0}: {1}", unitId, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Writes a single holding register value to a slave device
        /// </summary>
        /// <param name="unitId">The unit ID of the slave device</param>
        /// <param name="address">Register address to write to</param>
        /// <param name="value">Value to write</param>
        public void WriteSingleRegister(byte unitId, ushort address, ushort value)
        {
            if (!_isRunning)
                throw new InvalidOperationException("Client is not running. Call Start() first.");

            try
            {
                EnsureConnected();

                _master.WriteSingleRegister(unitId, address, value);

                if (_slaveDevices.TryGetValue(unitId, out var device))
                {
                    device.HoldingRegisters[address] = value;
                    _log.DebugFormat("Updated holding register for device {0} at address {1} with value {2}", unitId, address, value);
                }
            }
            catch (Exception ex)
            {
                _log.ErrorFormat("Error writing holding register to device {0}: {1}", unitId, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Writes multiple holding register values to a slave device
        /// </summary>
        /// <param name="unitId">The unit ID of the slave device</param>
        /// <param name="startAddress">Starting register address</param>
        /// <param name="values">Values to write</param>
        public void WriteMultipleRegisters(byte unitId, ushort startAddress, ushort[] values)
        {
            if (!_isRunning)
                throw new InvalidOperationException("Client is not running. Call Start() first.");

            try
            {
                EnsureConnected();

                _master.WriteMultipleRegisters(unitId, startAddress, values);

                if (_slaveDevices.TryGetValue(unitId, out var device))
                {
                    Array.Copy(values, 0, device.HoldingRegisters, startAddress, values.Length);
                    _log.DebugFormat("Updated holding registers for device {0}, starting at {1}, count {2}", unitId, startAddress, values.Length);
                }
            }
            catch (Exception ex)
            {
                _log.ErrorFormat("Error writing holding registers to device {0}: {1}", unitId, ex.Message);
                throw;
            }
        }
        #endregion

        #region Input Registers Methods
        /// <summary>
        /// Reads input registers from a specific slave device
        /// </summary>
        /// <param name="unitId">The unit ID of the slave device</param>
        /// <param name="startAddress">Starting address of registers to read</param>
        /// <param name="count">Number of registers to read</param>
        /// <returns>Array of register values</returns>
        public ushort[] ReadInputRegisters(byte unitId, ushort startAddress, ushort count)
        {
            if (!_isRunning)
                throw new InvalidOperationException("Client is not running. Call Start() first.");

            try
            {
                EnsureConnected();

                var data = _master.ReadInputRegisters(unitId, startAddress, count);

                if (_slaveDevices.TryGetValue(unitId, out var device))
                {
                    Array.Copy(data, 0, device.InputRegisters, startAddress, count);
                    _log.DebugFormat("Updated input registers for device {0}, starting at {1}, count {2}", unitId, startAddress, count);
                }

                return data;
            }
            catch (Exception ex)
            {
                _log.ErrorFormat("Error reading input registers from device {0}: {1}", unitId, ex.Message);
                throw;
            }
        }
        #endregion

        #region Coils Methods
        /// <summary>
        /// Reads coils from a specific slave device
        /// </summary>
        /// <param name="unitId">The unit ID of the slave device</param>
        /// <param name="startAddress">Starting address of coils to read</param>
        /// <param name="count">Number of coils to read</param>
        /// <returns>Array of coil values</returns>
        public bool[] ReadCoils(byte unitId, ushort startAddress, ushort count)
        {
            if (!_isRunning)
                throw new InvalidOperationException("Client is not running. Call Start() first.");

            try
            {
                EnsureConnected();

                var data = _master.ReadCoils(unitId, startAddress, count);

                if (_slaveDevices.TryGetValue(unitId, out var device))
                {
                    Array.Copy(data, 0, device.Coils, startAddress, count);
                    _log.DebugFormat("Updated coils for device {0}, starting at {1}, count {2}", unitId, startAddress, count);
                }

                return data;
            }
            catch (Exception ex)
            {
                _log.ErrorFormat("Error reading coils from device {0}: {1}", unitId, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Writes a single coil value to a slave device
        /// </summary>
        /// <param name="unitId">The unit ID of the slave device</param>
        /// <param name="address">Coil address to write to</param>
        /// <param name="value">Value to write</param>
        public void WriteSingleCoil(byte unitId, ushort address, bool value)
        {
            if (!_isRunning)
                throw new InvalidOperationException("Client is not running. Call Start() first.");

            try
            {
                EnsureConnected();

                _master.WriteSingleCoil(unitId, address, value);

                if (_slaveDevices.TryGetValue(unitId, out var device))
                {
                    device.Coils[address] = value;
                    _log.DebugFormat("Updated coil for device {0} at address {1} with value {2}", unitId, address, value);
                }
            }
            catch (Exception ex)
            {
                _log.ErrorFormat("Error writing coil to device {0}: {1}", unitId, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Writes multiple coil values to a slave device
        /// </summary>
        /// <param name="unitId">The unit ID of the slave device</param>
        /// <param name="startAddress">Starting coil address</param>
        /// <param name="values">Values to write</param>
        public void WriteMultipleCoils(byte unitId, ushort startAddress, bool[] values)
        {
            if (!_isRunning)
                throw new InvalidOperationException("Client is not running. Call Start() first.");

            try
            {
                EnsureConnected();

                _master.WriteMultipleCoils(unitId, startAddress, values);

                if (_slaveDevices.TryGetValue(unitId, out var device))
                {
                    Array.Copy(values, 0, device.Coils, startAddress, values.Length);
                    _log.DebugFormat("Updated coils for device {0}, starting at {1}, count {2}", unitId, startAddress, values.Length);
                }
            }
            catch (Exception ex)
            {
                _log.ErrorFormat("Error writing coils to device {0}: {1}", unitId, ex.Message);
                throw;
            }
        }
        #endregion

        

        private async Task PollDevicesAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _isRunning)
            {
                try
                {
                    EnsureConnected();
                    foreach (var device in _slaveDevices.Values)
                    {
                        await PollDeviceHoldingRegistersAsync(device, cancellationToken);
                        await PollDeviceInputRegistersAsync(device, cancellationToken);
                        await PollDeviceCoilsAsync(device, cancellationToken);
                    }

                    // Wait before next polling cycle
                    await Task.Delay(_pollingInterval, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _log.ErrorFormat("Error during UGS polling cycle: {0}", ex.Message);

                    // Wait before trying again
                    try
                    {
                        await Task.Delay(Math.Max(_pollingInterval, 5000), cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    // Reconnect on next attempt
                    DisconnectFromUGS();
                }
            }
        }

        private async Task PollDeviceHoldingRegistersAsync(ModbusSlaveDevice device, CancellationToken cancellationToken)
        {
            // ranges of holding registers to poll
            const ushort startAddress = 0;
            const ushort totalRegisters = 100;
            const ushort maxRegistersPerRequest = 125; 

            try
            {
                for (ushort address = startAddress; address < startAddress + totalRegisters; address += maxRegistersPerRequest)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // How many registers to read in this request
                    ushort registersToRead = (ushort)Math.Min(maxRegistersPerRequest, (startAddress + totalRegisters) - address);

                    try
                    {
                        var data = _master.ReadHoldingRegisters(device.UnitId, address, registersToRead);
                        Array.Copy(data, 0, device.HoldingRegisters, address, registersToRead);

                        _log.DebugFormat("Polled holding registers for device {0}, starting at {1}, count {2}",
                            device.UnitId, address, registersToRead);

                        // Delay between requests
                        await Task.Delay(50, cancellationToken);
                    }
                    catch (NModbus.SlaveException ex)
                    {
                        _log.WarnFormat("Slave exception when polling holding registers for device {0}, address range {1}-{2}: {3}",
                            device.UnitId, address, address + registersToRead - 1, ex.Message);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.ErrorFormat("Error polling holding registers for device {0}: {1}", device.UnitId, ex.Message);
            }
        }

        private async Task PollDeviceInputRegistersAsync(ModbusSlaveDevice device, CancellationToken cancellationToken)
        {
            const ushort startAddress = 0;
            const ushort totalRegisters = 100;
            const ushort maxRegistersPerRequest = 125;

            try
            {
                for (ushort address = startAddress; address < startAddress + totalRegisters; address += maxRegistersPerRequest)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    ushort registersToRead = (ushort)Math.Min(maxRegistersPerRequest, (startAddress + totalRegisters) - address);

                    try
                    {
                        var data = _master.ReadInputRegisters(device.UnitId, address, registersToRead);

                        Array.Copy(data, 0, device.InputRegisters, address, registersToRead);

                        _log.DebugFormat("Polled input registers for device {0}, starting at {1}, count {2}",
                            device.UnitId, address, registersToRead);

                        await Task.Delay(50, cancellationToken);
                    }
                    catch (NModbus.SlaveException ex)
                    {
                        _log.WarnFormat("Slave exception when polling input registers for device {0}, address range {1}-{2}: {3}",
                            device.UnitId, address, address + registersToRead - 1, ex.Message);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.ErrorFormat("Error polling input registers for device {0}: {1}", device.UnitId, ex.Message);
            }
        }

        private async Task PollDeviceCoilsAsync(ModbusSlaveDevice device, CancellationToken cancellationToken)
        {
            const ushort startAddress = 0;
            const ushort totalCoils = 100;
            const ushort maxCoilsPerRequest = 2000; 
            try
            {
                for (ushort address = startAddress; address < startAddress + totalCoils; address += maxCoilsPerRequest)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ushort coilsToRead = (ushort)Math.Min(maxCoilsPerRequest, (startAddress + totalCoils) - address);

                    try
                    {
                        var data = _master.ReadCoils(device.UnitId, address, coilsToRead);
                        Array.Copy(data, 0, device.Coils, address, coilsToRead);

                        _log.DebugFormat("Polled coils for device {0}, starting at {1}, count {2}",
                            device.UnitId, address, coilsToRead);
                        await Task.Delay(50, cancellationToken);
                    }
                    catch (NModbus.SlaveException ex)
                    {
                        _log.WarnFormat("Slave exception when polling coils for device {0}, address range {1}-{2}: {3}",
                            device.UnitId, address, address + coilsToRead - 1, ex.Message);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.ErrorFormat("Error polling coils for device {0}: {1}", device.UnitId, ex.Message);
            }
        }

        private void EnsureConnected()
        {
            if (_master != null && _tcpClient != null && _tcpClient.Connected)
                return;

            DisconnectFromUGS();

            try
            {
                _tcpClient = new TcpClient();
                _tcpClient.Connect(_ipAddress, _port);

                _tcpClient.ReceiveTimeout = 5000;
                _tcpClient.SendTimeout = 5000;

                _tcpClientAdapter = new TcpClientAdapter(_tcpClient);
                _master = _factory.CreateRtuMaster(_tcpClientAdapter);

                _master.Transport.Retries = 3;
                _master.Transport.ReadTimeout = 3000;
                _master.Transport.WriteTimeout = 3000;
                _master.Transport.StreamResource.DiscardInBuffer();

                _log.InfoFormat("Connected to UGS at {0}:{1}", _ipAddress, _port);
            }
            catch (Exception ex)
            {
                _log.ErrorFormat("Failed to connect to UGS at {0}:{1} - {2}", _ipAddress, _port, ex.Message);
                DisconnectFromUGS();
                throw;
            }
        }

        private void DisconnectFromUGS()
        {
            try
            {
                _master?.Dispose();
                _tcpClientAdapter?.Dispose();
                _tcpClient?.Close();
            }
            catch (Exception ex)
            {
                _log.ErrorFormat("Error disconnecting from UGS: {0}", ex.Message);
            }
            finally
            {
                _master = null;
                _tcpClientAdapter = null;
                _tcpClient = null;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    Stop();
                    _cancellationTokenSource?.Dispose();
                }

                _isDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~ModbusClient()
        {
            Dispose(false);
        }
    }
}

