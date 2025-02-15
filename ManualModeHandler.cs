using EasyModbus;
using EasyModbus.Exceptions;
using NModbus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp9
{
    public class ManualModeHandler : IOperationModeHandler
    {
        private RegisterManager registerManager;

        // Flag dla poszczególnych komunikatów:
        private bool holdingNotFoundMessagePrinted = false;
        private bool inputNotFoundMessagePrinted = false;
        private bool coilNotFoundMessagePrinted = false;

      
        public ManualModeHandler()
        {
            registerManager = RegisterManager.Instance;
            registerManager.LoadFromFile("registers.xml");
        }

        public void Synchronize(ModbusSlaveDevice slave, ModbusServer tcpServer)
        {
            
            var holdingRegister = registerManager.HoldingRegisters.FirstOrDefault(r =>
                r.SlaveId == slave.UnitId &&
                r.IsActive &&
                (tcpServer.LastStartingAddress >= r.StartAddress &&
                tcpServer.LastStartingAddress < r.StartAddress + r.Quantity) &&
                r.functionCode == tcpServer.FunctionCode);
           
            
            if (holdingRegister != null)
            {
                holdingNotFoundMessagePrinted = false;
                for (ushort i = holdingRegister.StartAddress; i < holdingRegister.StartAddress + holdingRegister.Quantity; i++)
                {
                    if (i < tcpServer.holdingRegisters.localArray.Length && i < slave.HoldingRegisters.Length)
                    {
                        tcpServer.holdingRegisters[i+1] = (short)slave.HoldingRegisters[i];
                   
                    }
                }
            }
            else
            {
                if (!holdingNotFoundMessagePrinted)
                {
                    Console.WriteLine("Nie znaleziono odpowiadającego rejestru holding");
                    
                    holdingNotFoundMessagePrinted = true;
                    
                }
            }


            var inputRegister = registerManager.InputRegisters.FirstOrDefault(r =>
            r.SlaveId == slave.UnitId &&
            r.IsActive &&
            (tcpServer.LastStartingAddress >= r.StartAddress &&
            tcpServer.LastStartingAddress < r.StartAddress + r.Quantity) &&
            r.functionCode == tcpServer.FunctionCode);


            if (inputRegister != null)
            {
                inputNotFoundMessagePrinted = false;
                for (ushort i = inputRegister.StartAddress; i < inputRegister.StartAddress + inputRegister.Quantity; i++)
                {
                    if (i < tcpServer.inputRegisters.localArray.Length && i < slave.InputRegisters.Length)
                    {
                        tcpServer.inputRegisters[i+1] = (short)slave.InputRegisters[i];
                    }
                }
            }
            else
            {
                if (!inputNotFoundMessagePrinted)
                {
                    Console.WriteLine("Nie znaleziono odpowiadającego rejestru input");
                    inputNotFoundMessagePrinted = true;
                }
            }

            var coilRegister = registerManager.Coils.FirstOrDefault(r =>
            r.SlaveId == slave.UnitId &&
            r.IsActive &&
            (tcpServer.LastStartingAddress >= r.StartAddress &&
            tcpServer.LastStartingAddress < r.StartAddress + r.Quantity) &&
            r.functionCode == tcpServer.FunctionCode);

            if (coilRegister != null)
            {
                coilNotFoundMessagePrinted = false;
                for (ushort i = coilRegister.StartAddress; i < coilRegister.StartAddress + coilRegister.Quantity; i++)
                {
                    if (i < tcpServer.coils.localArray.Length && i < slave.Coils.Length)
                    {
                        tcpServer.coils[i+1] = slave.Coils[i];
                    }
                }
            }
            else
            {
                if (!coilNotFoundMessagePrinted)
                {
                    Console.WriteLine("Nie znaleziono odpowiadającego coil");
                    coilNotFoundMessagePrinted = true;
                }
            }
        }

        public void HandleCoilsChanged(byte slaveId, int coil, int numberOfPoints, ModbusServer tcpServer, ClientHandler rtuClient, Dictionary<byte, ModbusSlaveDevice> slaveDevices)
        {
            try
            {
                // czy rejestr jest w pliku
                var manualCoil = registerManager.Coils.FirstOrDefault(r =>
                    r.SlaveId == slaveId &&
                    r.IsActive &&
                    (coil >= r.StartAddress && coil < r.StartAddress + r.Quantity));

                if (manualCoil != null)
                {
                    // Pobierz wartości z serwera TCP 
                    bool[] values = new bool[numberOfPoints];
                    for (int i = 0; i < numberOfPoints; i++)
                    {
                        values[i] = tcpServer.coils[coil+i];
                    }

                    // Jeśli warunki są spełnione przekaż dane do RTU:
                    if (slaveDevices.ContainsKey(slaveId))
                    {
                        rtuClient.WriteMultipleCoils(slaveId, (ushort)(coil-1), values);
                        Console.WriteLine($"[Manual] Przesłano zmianę cewek do urządzenia {slaveId}, początkowy adres: {coil}, liczba punktów: {numberOfPoints}");
                    }
                   
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Manual] Błąd podczas obsługi zmiany cewek: {ex.Message}");
            }
        }

        public void HandleHoldingRegistersChanged(byte slaveId, int register, int numberOfPoints, ModbusServer tcpServer, ClientHandler rtuClient, Dictionary<byte, ModbusSlaveDevice> slaveDevices)
        {
            try
            {
                var manualRegister = registerManager.HoldingRegisters.FirstOrDefault(r =>
                    r.SlaveId == slaveId &&
                    r.IsActive &&
                    (register >= r.StartAddress && register < r.StartAddress + r.Quantity));

                if (manualRegister != null)
                {
                    ushort[] values = new ushort[numberOfPoints];
                    for (int i = 0; i < numberOfPoints; i++)
                    {
                        values[i] = (ushort)tcpServer.holdingRegisters[register+i];
                    }

                    if (slaveDevices.ContainsKey(slaveId))
                    {
                        rtuClient.WriteMultipleRegisters(slaveId, (ushort)(register-1), values);
                        Console.WriteLine($"[Manual] Przesłano zmianę rejestrów do urządzenia {slaveId}, początkowy adres: {register}, liczba rejestrów: {numberOfPoints}");
                    }
                   
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Manual] Błąd podczas obsługi zmiany rejestrów: {ex.Message}");
            }
        }

        public void ReadHoldingRegisters(ModbusSlaveDevice slave, IModbusMaster master, ushort startingAddress, ushort quantity)
        {
            for (ushort start = startingAddress; start < (quantity+startingAddress); start += 125)
            {
                ushort regsToRead = (ushort)Math.Min(125, (startingAddress + quantity) - startingAddress);
                var holdingData = master.ReadHoldingRegisters(slave.UnitId, start, regsToRead);
                Array.Copy(holdingData, 0, slave.HoldingRegisters, start, regsToRead);
            }
        }

        public void ReadInputRegisters(ModbusSlaveDevice slave, IModbusMaster master, ushort startingAddress, ushort quantity)
        {
            for (ushort start = startingAddress; start < (quantity+startingAddress); start += 125)
            {
                ushort regsToRead = (ushort)Math.Min(125, (startingAddress + quantity) - startingAddress);
                var inputData = master.ReadInputRegisters(slave.UnitId, start, regsToRead);
                Array.Copy(inputData, 0, slave.InputRegisters, start, regsToRead);
            }
        }

        public void ReadCoils(ModbusSlaveDevice slave, IModbusMaster master, ushort startingAddress, ushort quantity)
        {
            for (ushort start = startingAddress; start < (quantity+startingAddress); start += 2000)
            {
                ushort coilsToRead = (ushort)Math.Min(125, (startingAddress + quantity) - startingAddress);
                var coilsData = master.ReadCoils(slave.UnitId, start, coilsToRead);
                Array.Copy(coilsData, 0, slave.Coils, start, coilsToRead);
            }
        }
        public void WriteSingleRegister(IModbusMaster master, byte address, ushort startRegister, ushort value)
        {
            try
            {
                var register = registerManager.HoldingRegisters.FirstOrDefault
                    (r => r.SlaveId == address && 
                    r.StartAddress == startRegister &&
                    r.AccessMode=="W" && r.IsActive==true) ;
                if (register != null)
                {
                    master.WriteSingleRegister(address, startRegister, value);
                }
                else
                {
                    Console.WriteLine($"Brak aktywnego rejestru o adresie {startRegister} z uprawnieniami W dla slave {address}.");
                    
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine("Wystąpił błąd podczas próby zapisu rejestru: " + ex.Message);
            }
     
        }

        public void WriteMultipleRegisters(IModbusMaster master, byte address, ushort startRegister, ushort[] values)
        {
            try
            {
                for (ushort i = 0; i < values.Length; i++)
                {
                    ushort currentAddress = (ushort)(startRegister + i);
                    var register = registerManager.HoldingRegisters.FirstOrDefault(
                        r => r.SlaveId == address &&
                             currentAddress >= r.StartAddress &&
                             currentAddress < r.StartAddress + r.Quantity &&
                             r.AccessMode == "W" &&
                             r.IsActive);

                    if (register == null)
                    {
                        Console.WriteLine($"Brak aktywnego rejestru o adresie {currentAddress} z uprawnieniami W dla slave {address}.");
                        return;
                    }
                }

                master.WriteMultipleRegisters(address, startRegister, values);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Wystąpił błąd podczas próby zapisu rejestrów: " + ex.Message);
            }
        }

        public void WriteSingleCoil(IModbusMaster master, byte address, ushort startRegister, bool value)
        {
            try
            {
                var register = registerManager.Coils.FirstOrDefault
                    (r => r.SlaveId == address &&
                    r.StartAddress == startRegister &&
                    r.AccessMode == "W" && r.IsActive == true);
                if (register != null)
                {
                    master.WriteSingleCoil(address, startRegister, value);
                }
                else
                {
                    Console.WriteLine($"Brak aktywnego rejestru o adresie {startRegister} z uprawnieniami W dla slave {address}.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Wystąpił błąd podczas próby zapisu rejestru: " + ex.Message);
            }

        }

        public void WriteMultipleCoils(IModbusMaster master, byte address, ushort startRegister, bool[] values)
        {
            try
            {
                for (ushort i = 0; i < values.Length; i++)
                {
                    ushort currentAddress = (ushort)(startRegister + i);
                    var register = registerManager.Coils.FirstOrDefault(
                        r => r.SlaveId == address &&
                             currentAddress >= r.StartAddress &&
                             currentAddress < r.StartAddress + r.Quantity &&
                             r.AccessMode == "W" &&
                             r.IsActive);

                    if (register == null)
                    {
                        Console.WriteLine($"Brak aktywnego rejestru o adresie {currentAddress} z uprawnieniami W dla slave {address}.");
                        return;
                    }
                }

                master.WriteMultipleCoils(address, startRegister, values);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Wystąpił błąd podczas próby zapisu rejestrów: " + ex.Message);
            }
        }
    }
    
}
