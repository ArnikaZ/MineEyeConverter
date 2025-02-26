using EasyModbus;
using log4net;
using Microsoft.Win32;
using NModbus;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static EasyModbus.ModbusServer;

namespace MineEyeConverter
{
    public class AutoModeHandler : IOperationModeHandler
    {
       
       
        public void HandleCoilsChanged(byte slaveId, int coil, int numberOfPoints, ModbusServer tcpServer, ClientHandler rtuClient, Dictionary<byte, ModbusSlaveDevice> slaveDevices)
        {
            // Pobierz dane z serwera TCP
            bool[] values = new bool[numberOfPoints];
            for (int i = 0; i < numberOfPoints; i++)
            {
                values[i] = tcpServer.coils[coil+i];
            }


            if (slaveDevices.ContainsKey(slaveId))
            {
                // Zapisz do urządzenia RTU
                rtuClient.WriteMultipleCoils(slaveId, (ushort)(coil-1 ), values);
                //log.Debug($"Przesłano zmianę coils do urządzenia {slaveId}, adres początkowy: {coil}, liczba punktów: {numberOfPoints}");
                Console.WriteLine($"[Auto] Przesłano zmianę coils do urządzenia {slaveId}, adres początkowy: {coil}, liczba punktów: {numberOfPoints}");
            }
        }
        public void HandleHoldingRegistersChanged(byte slaveId, int register, int numberOfPoints, ModbusServer tcpServer, ClientHandler rtuClient, Dictionary<byte, ModbusSlaveDevice> slaveDevices)
        {
            // Pobierz dane z serwera TCP
            ushort[] values = new ushort[numberOfPoints];
            for (int i = 0; i < numberOfPoints; i++)
            {
                values[i] = (ushort)tcpServer.holdingRegisters[register+i];
            }

            if (slaveDevices.ContainsKey(slaveId))
            {
                // Zapisz do urządzenia RTU
                rtuClient.WriteMultipleRegisters(slaveId, (ushort)(register-1), values);
                //_log.Debug($"Przesłano zmianę rejestrów do urządzenia {slaveId}, adres początkowy: {register}, liczba rejestrów: {numberOfPoints}");
                Console.WriteLine($"[Auto] Przesłano zmianę rejestrów do urządzenia {slaveId}, adres początkowy: {register}, liczba rejestrów: {numberOfPoints}");
                
            }
        }

        public void ReadHoldingRegisters(ModbusSlaveDevice slave, IModbusMaster master, ModbusServer server)
        {
                // Czytanie po 125 rejestrów na raz (ograniczenie protokołu Modbus)
                for (ushort startAddress = server.LastStartingAddress; startAddress < (server.LastQuantity + server.LastStartingAddress); startAddress += 125)
                {
                    // Obliczenie ile rejestrów zostało do końca
                    ushort registersToRead = (ushort)Math.Min(125, (server.LastStartingAddress + server.LastQuantity) - startAddress);
                 var data=master.ReadHoldingRegisters(slave.UnitId, server.LastStartingAddress, registersToRead);
                // Kopiowanie odczytane dane do tablicy
                Array.Copy(data, 0, slave.HoldingRegisters, server.LastStartingAddress, server.LastQuantity);
               Array.Copy(data, 0, server.holdingRegisters.localArray, server.LastStartingAddress, server.LastQuantity);
            }
        }
        public void ReadInputRegisters(ModbusSlaveDevice slave, IModbusMaster master, ModbusServer server)
        {
            for (ushort startAddress = server.LastStartingAddress; startAddress < (server.LastQuantity+server.LastStartingAddress); startAddress += 125)
            {
                ushort registersToRead = (ushort)Math.Min(125, (server.LastStartingAddress + server.LastQuantity) - startAddress);
                var data= master.ReadInputRegisters(slave.UnitId, startAddress, registersToRead);
                Array.Copy(data, 0, slave.InputRegisters, startAddress, registersToRead);
               Array.Copy(data, 0, server.inputRegisters.localArray, startAddress, registersToRead);
            }
        }
        public void ReadCoils(ModbusSlaveDevice slave, IModbusMaster master, ModbusServer server)
        {
            for (ushort startAddress = server.LastStartingAddress; startAddress < (server.LastQuantity+server.LastStartingAddress); startAddress += 125)
            {
                ushort coilsToRead = (ushort)Math.Min(125, (server.LastStartingAddress + server.LastQuantity) - startAddress);
                
                var data=master.ReadCoils(slave.UnitId, startAddress, coilsToRead);
                Array.Copy(data, 0, slave.Coils, startAddress, coilsToRead);
                Array.Copy(data, 0, server.coils.localArray, startAddress, coilsToRead);

            }
        }
        public void WriteSingleRegister(IModbusMaster master, byte address, ushort startRegister, ushort value)
        {
            
            master.WriteSingleRegister(address, startRegister, value);
        }

        public void WriteMultipleRegisters(IModbusMaster master, byte address, ushort startRegister, ushort[] values)
        {
            master.WriteMultipleRegisters(address, startRegister, values);
        }
        public void WriteSingleCoil(IModbusMaster master, byte address, ushort startRegister, bool value)
        {
            master.WriteSingleCoil(address, startRegister, value);
        }
        public void WriteMultipleCoils(IModbusMaster master, byte address, ushort startRegister, bool[] values)
        {
            master.WriteMultipleCoils(address, startRegister, values);
        }
        
    }
}
