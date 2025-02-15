using EasyModbus;
using NModbus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp9
{
    public interface IOperationModeHandler
    {
        void Synchronize(ModbusSlaveDevice slave, ModbusServer tcpServer);
        void HandleCoilsChanged(byte slaveId, int coil, int numberOfPoints, ModbusServer tcpServer, ClientHandler rtuClient, Dictionary<byte, ModbusSlaveDevice> slaveDevices);
        void HandleHoldingRegistersChanged(byte slaveId, int register, int numberOfPoints, ModbusServer tcpServer, ClientHandler rtuClient, Dictionary<byte, ModbusSlaveDevice> slaveDevices);
        void ReadHoldingRegisters(ModbusSlaveDevice slave, IModbusMaster master, ushort startingAddress, ushort quantity);
        void ReadInputRegisters(ModbusSlaveDevice slave, IModbusMaster master, ushort startingAddress, ushort quantity);
        void ReadCoils(ModbusSlaveDevice slave, IModbusMaster master, ushort startingAddress, ushort quantity);
        void WriteSingleRegister(IModbusMaster master, byte address, ushort startRegister, ushort value);
        void WriteMultipleRegisters(IModbusMaster master, byte address, ushort startRegister, ushort[] values);
        void WriteSingleCoil(IModbusMaster master, byte address, ushort startRegister, bool value);
        void WriteMultipleCoils(IModbusMaster master, byte address, ushort startRegister, bool[] values);

    }
}
