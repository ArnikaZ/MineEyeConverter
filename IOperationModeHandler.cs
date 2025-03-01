using EasyModbus;
using NModbus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MineEyeConverter
{
    public interface IOperationModeHandler
    {
        void HandleCoilsChanged(byte slaveId, int coil, int numberOfPoints, ModbusServer tcpServer, ClientHandler rtuClient, Dictionary<byte, ModbusSlaveDevice> slaveDevices);
        void HandleHoldingRegistersChanged(byte slaveId, int register, int numberOfPoints, ModbusServer tcpServer, ClientHandler rtuClient, Dictionary<byte, ModbusSlaveDevice> slaveDevices);
        void ReadHoldingRegisters(ModbusSlaveDevice slave, IModbusMaster master, ModbusServer server);
        void ReadInputRegisters(ModbusSlaveDevice slave, IModbusMaster master, ModbusServer server);
        void ReadCoils(ModbusSlaveDevice slave, IModbusMaster master, ModbusServer server);
        void WriteSingleRegister(IModbusMaster master, byte address, ushort startRegister, ushort value);
        void WriteMultipleRegisters(IModbusMaster master, byte address, ushort startRegister, ushort[] values);
        void WriteSingleCoil(IModbusMaster master, byte address, ushort startRegister, bool value);
        void WriteMultipleCoils(IModbusMaster master, byte address, ushort startRegister, bool[] values);
    }
}
