using EasyModbus;
using log4net;
using Microsoft.Win32;
using NModbus;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static EasyModbus.ModbusServer;

namespace MineEyeConverter
{
    public class AutoModeHandler : IOperationModeHandler
    {
        private bool holdingNotFoundMessagePrinted = false;
        private bool inputNotFoundMessagePrinted = false;
        private bool coilNotFoundMessagePrinted = false;

        public void Synchronize(ModbusSlaveDevice slave, ModbusServer tcpServer)
        {

            // Sprawdzenie długości tablic przed synchronizacją
            if (slave.Coils == null || slave.Coils.Length == 0)
            {
                Console.WriteLine($"Błąd: Slave {slave.UnitId} - brak cewek (Coils).");
            }
            else
            {
                for (ushort i = 0; i < slave.Coils.Length && i + 1 < tcpServer.coils.localArray.Length; i++)
                {

                    tcpServer.coils[i + 1] = slave.Coils[i];

                }
            }

            if (slave.HoldingRegisters == null || slave.HoldingRegisters.Length == 0)
            {
                Console.WriteLine($"Błąd: Slave {slave.UnitId} - brak rejestrów holding.");
            }
            else
            {
                for (ushort i = 0; i < slave.HoldingRegisters.Length && i + 1 < tcpServer.holdingRegisters.localArray.Length; i++)
                {

                    tcpServer.holdingRegisters[i+1] = (short)slave.HoldingRegisters[i];

                }
            }

            if (slave.InputRegisters == null || slave.InputRegisters.Length == 0)
            {
                Console.WriteLine($"Błąd: Slave {slave.UnitId} - brak rejestrów wejściowych.");
            }
            else
            {
                for (ushort i = 0; i < slave.InputRegisters.Length && i + 1 < tcpServer.inputRegisters.localArray.Length; i++)
                {

                    tcpServer.inputRegisters[i + 1] = (short)slave.InputRegisters[i];

                }
            }
        }
        public void HandleCoilsChanged(byte slaveId, int coil, int numberOfPoints, ModbusServer tcpServer, ClientHandler rtuClient, Dictionary<byte, ModbusSlaveDevice> slaveDevices)
        {
            // Pobierz dane z serwera TCP
            bool[] values = new bool[numberOfPoints];
            for (int i = 0; i < numberOfPoints; i++)
            {
                values[i] = tcpServer.coils[coil];
            }


            if (slaveDevices.ContainsKey(slaveId))
            {
                // Zapisz do urządzenia RTU
                rtuClient.WriteMultipleCoils(slaveId, (ushort)(coil - 1), values);
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

        public void ReadHoldingRegisters(ModbusSlaveDevice slave, IModbusMaster master, ushort StartingAddress, ushort Quantity)
        {
            
            // Czytanie po 125 rejestrów na raz (ograniczenie protokołu Modbus)
            for (ushort startAddress = StartingAddress; startAddress < (Quantity+StartingAddress); startAddress += 125)
            {

                // Obliczenie ile rejestrów zostało do końca
                ushort registersToRead = (ushort)Math.Min(125, (StartingAddress + Quantity) - startAddress);


                var holdingData = master.ReadHoldingRegisters(slave.UnitId, (ushort)(startAddress), registersToRead);
                // Kopiowanie odczytane dane do tablicy
                Array.Copy(holdingData, 0, slave.HoldingRegisters, startAddress, registersToRead);
            }
        }
        public void ReadInputRegisters(ModbusSlaveDevice slave, IModbusMaster master, ushort StartingAddress, ushort Quantity)
        {
            for (ushort startAddress = StartingAddress; startAddress < (Quantity+StartingAddress); startAddress += 125)
            {
                ushort registersToRead = (ushort)Math.Min(125, (StartingAddress + Quantity) - startAddress);

                var inputData = master.ReadInputRegisters(slave.UnitId, startAddress, registersToRead);
                Array.Copy(inputData, 0, slave.InputRegisters, startAddress, registersToRead);
            }
        }
        public void ReadCoils(ModbusSlaveDevice slave, IModbusMaster master, ushort StartingAddress, ushort Quantity)
        {
            for (ushort startAddress = StartingAddress; startAddress < (Quantity+StartingAddress); startAddress += 2000)
            {
                ushort coilsToRead = (ushort)Math.Min(125, (StartingAddress + Quantity) - startAddress);

                var coilsData = master.ReadCoils(slave.UnitId, startAddress, coilsToRead);
                Array.Copy(coilsData, 0, slave.Coils, startAddress, coilsToRead);
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
