﻿/*
Copyright (c) 2018-2020 Rossmann-Engineering
Permission is hereby granted, free of charge, 
to any person obtaining a copy of this software
and associated documentation files (the "Software"),
to deal in the Software without restriction, 
including without limitation the rights to use, 
copy, modify, merge, publish, distribute, sublicense, 
and/or sell copies of the Software, and to permit 
persons to whom the Software is furnished to do so, 
subject to the following conditions:

The above copyright notice and this permission 
notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, 
DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, 
ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE 
OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Net.NetworkInformation;
using System.IO.Ports;
using MineEyeConverter;
using static EasyModbus.TCPHandler;
using System.Security.Cryptography.X509Certificates;

namespace EasyModbus
{
#region class ModbusProtocol
    /// <summary>
    /// Modbus Protocol informations.
    /// </summary>
    public class ModbusProtocol
    {
    	public enum ProtocolType { ModbusTCP = 0, ModbusUDP = 1, ModbusRTU = 2};
        public DateTime timeStamp;
        public bool request;
        public bool response;
        public UInt16 transactionIdentifier;
        public UInt16 protocolIdentifier;
        public UInt16 length;
        public byte unitIdentifier;
        public byte functionCode;
        public UInt16 startingAdress;
        public UInt16 startingAddressRead;
        public UInt16 startingAddressWrite;
        public UInt16 quantity;
        public UInt16 quantityRead;
        public UInt16 quantityWrite;
        public byte byteCount;
        public byte exceptionCode;
        public byte errorCode;
        public UInt16[] receiveCoilValues;
        public UInt16[] receiveRegisterValues;
        public Int16[] sendRegisterValues;
        public bool[] sendCoilValues;    
        public UInt16 crc;
    }
#endregion

#region structs
    struct NetworkConnectionParameter
    {
        public NetworkStream stream;        //For TCP-Connection only
        public Byte[] bytes;
        public int portIn;                  //For UDP-Connection only
        public IPAddress ipAddressIn;       //For UDP-Connection only
        public TcpClient tcpClient;
    }
#endregion

#region TCPHandler class
    public class TCPHandler
    {
        public ModbusTcpServer _tcpServer;
        public delegate void DataChanged(object networkConnectionParameter);
        public event DataChanged dataChanged;

        public delegate void NumberOfClientsChanged();
        public event NumberOfClientsChanged numberOfClientsChanged;

        TcpListener server = null;


        private List<Client> tcpClientLastRequestList = new List<Client>();

        public int NumberOfConnectedClients { get; set; }

        public string ipAddress = null;

        /// When making a server TCP listen socket, will listen to this IP address.
        public IPAddress LocalIPAddress {
            get { return localIPAddress; }
        }
        private IPAddress localIPAddress = IPAddress.Any;

        /// <summary>
        /// Listen to all network interfaces.
        /// </summary>
        /// <param name="port">TCP port to listen</param>
        public TCPHandler(int port)
        {
            server = new TcpListener(LocalIPAddress, port);
            server.Start();
            server.BeginAcceptTcpClient(AcceptTcpClientCallback, null);
        }

        /// <summary>
        /// Listen to a specific network interface.
        /// </summary>
        /// <param name="localIPAddress">IP address of network interface to listen</param>
        /// <param name="port">TCP port to listen</param>
        public TCPHandler(IPAddress localIPAddress, int port)
        {
            this.localIPAddress = localIPAddress;
            server = new TcpListener(LocalIPAddress, port);
            server.Start();
            server.BeginAcceptTcpClient(AcceptTcpClientCallback, null);
        }


        private void AcceptTcpClientCallback(IAsyncResult asyncResult)
        {
            TcpClient tcpClient = new TcpClient();
            try
            {
                tcpClient = server.EndAcceptTcpClient(asyncResult);
                tcpClient.ReceiveTimeout = 4000;
                if (ipAddress != null)
                {
                    string ipEndpoint = tcpClient.Client.RemoteEndPoint.ToString();
                    ipEndpoint = ipEndpoint.Split(':')[0];
                    
                   
                    if (ipEndpoint != ipAddress)
                    {
                        tcpClient.Client.Disconnect(false);
                        return;
                    }
                }
            }
            catch (Exception) { }
            try
            {
                server.BeginAcceptTcpClient(AcceptTcpClientCallback, null);
                Client client = new Client(tcpClient);
                NetworkStream networkStream = client.NetworkStream;
                networkStream.ReadTimeout = 4000;
                networkStream.BeginRead(client.Buffer, 0, client.Buffer.Length, ReadCallback, client);
            }
            catch (Exception) { }
        }

        private int GetAndCleanNumberOfConnectedClients(Client client)
        {
            lock (this)
            {
                int i = 0;
                bool objetExists = false;
                foreach (Client clientLoop in tcpClientLastRequestList)
                {
                    if (client.Equals(clientLoop))
                        objetExists = true;
                }
                try
                {
                    tcpClientLastRequestList.RemoveAll(delegate (Client c)
                        {
                            return ((DateTime.Now.Ticks - c.Ticks) > 40000000);
                        }

                        );
                }
                catch (Exception) { }
                if (!objetExists)
                    tcpClientLastRequestList.Add(client);


                return tcpClientLastRequestList.Count;
            }
        }

        private void ReadCallback(IAsyncResult asyncResult)
        {
            NetworkConnectionParameter networkConnectionParameter = new NetworkConnectionParameter();
            Client client = asyncResult.AsyncState as Client;
            client.Ticks = DateTime.Now.Ticks;
            NumberOfConnectedClients = GetAndCleanNumberOfConnectedClients(client);
            if (numberOfClientsChanged != null)
                numberOfClientsChanged();
            if (client != null)
            {
                int read;
                NetworkStream networkStream = null;
                try
                {
                networkStream = client.NetworkStream;

                    read = networkStream.EndRead(asyncResult);
                }
                catch (Exception ex)
                {
                    return;
                }


                if (read == 0)
                {
                    //OnClientDisconnected(client.TcpClient);
                    //connectedClients.Remove(client);
                    return;
                }
                byte[] data = new byte[read];
                Buffer.BlockCopy(client.Buffer, 0, data, 0, read);
                networkConnectionParameter.bytes = data;
                networkConnectionParameter.stream = networkStream;
                networkConnectionParameter.tcpClient = client.TcpClient;
                if (dataChanged != null)
                    dataChanged(networkConnectionParameter);
                try
                {
                    networkStream.BeginRead(client.Buffer, 0, client.Buffer.Length, ReadCallback, client);
                }
                catch (Exception)
                {
                }
            }
        }

        public void Disconnect()
        {
            try
            {
                foreach (Client clientLoop in tcpClientLastRequestList)
                {
                    clientLoop.NetworkStream.Close(00);
                }
            }
            catch (Exception) { }
            server.Stop();
            
        }


        internal class Client
        {
            private readonly TcpClient tcpClient;
            private readonly byte[] buffer;
            public long Ticks { get; set; }

            public Client(TcpClient tcpClient)
            {
                this.tcpClient = tcpClient;
                int bufferSize = tcpClient.ReceiveBufferSize;
                buffer = new byte[bufferSize];
            }

            public TcpClient TcpClient
            {
                get { return tcpClient; }
            }

            public byte[] Buffer
            {
                get { return buffer; }
            }

            public NetworkStream NetworkStream
            {
                get {
                    
                        return tcpClient.GetStream();

                }
            }
        }
    }
#endregion
    
    /// <summary>
    /// Modbus TCP Server.
    /// </summary>
    public class ModbusServer
    {
        public bool UseWhiteList { get; set; }
        public List<MineEyeConverter.Client> WhiteList { get; set; }
        public IPAddress clientIp;
        public IOperationModeHandler OperationModeHandler { get; set; }

        
        private bool debug = false;
        Int32 port = 502;
        ModbusProtocol receiveData;
        public ModbusProtocol sendData =  new ModbusProtocol();
        Byte[] bytes = new Byte[2100];
        //public Int16[] _holdingRegisters = new Int16[65535];
        public HoldingRegisters holdingRegisters;      
        public InputRegisters inputRegisters;
        public Coils coils;
        public DiscreteInputs discreteInputs;
        private int numberOfConnections = 0;
        private bool udpFlag;
        private bool serialFlag;
        private int baudrate = 9600;
        private System.IO.Ports.Parity parity = Parity.Even;
        private System.IO.Ports.StopBits stopBits = StopBits.One;
        private string serialPort = "COM1";
        private SerialPort serialport;

        private byte unitIdentifier;
        private ushort _lastStartingAddress;
        private ushort _lastQuantity;
        private int _functionCode;

        public byte UnitIdentifierValue => unitIdentifier;
        public byte CurrentUnitIdentifier { get; set; }
        public ushort LastStartingAddress => _lastStartingAddress;
        public ushort LastQuantity => _lastQuantity;
        public int FunctionCode => _functionCode;



        private int portIn;
        public IPAddress ipAddressIn;
        private UdpClient udpClient;
        private IPEndPoint iPEndPoint;
        private TCPHandler tcpHandler;
        Thread listenerThread;
        Thread clientConnectionThread;
        private ModbusProtocol[] modbusLogData = new ModbusProtocol[100];
        public bool FunctionCode1Disabled {get; set;}
        public bool FunctionCode2Disabled { get; set; }
        public bool FunctionCode3Disabled { get; set; }
        public bool FunctionCode4Disabled { get; set; }
        public bool FunctionCode5Disabled { get; set; }
        public bool FunctionCode6Disabled { get; set; }
        public bool FunctionCode15Disabled { get; set; }
        public bool FunctionCode16Disabled { get; set; }
        public bool FunctionCode23Disabled { get; set; }
        public bool PortChanged { get; set; }
        object lockCoils = new object();
        object lockHoldingRegisters = new object();
        private volatile bool shouldStop;

        private IPAddress localIPAddress = IPAddress.Any;

        /// <summary>
        /// When creating a TCP or UDP socket, the local IP address to attach to.
        /// </summary>
        public IPAddress LocalIPAddress
        {
            get { return localIPAddress; }
            set { if (listenerThread == null) localIPAddress = value; }
        }

        public ModbusServer()
        {
            holdingRegisters = new HoldingRegisters(this);
            inputRegisters = new InputRegisters(this);
            coils = new Coils(this);
            discreteInputs = new DiscreteInputs(this);

        }

        #region events
        public delegate void CoilsChangedHandler(byte slaveId, int coil, int numberOfCoils);
        public event CoilsChangedHandler CoilsChanged;

        public delegate void HoldingRegistersChangedHandler(byte slaveId, int register, int numberOfRegisters);
        public event HoldingRegistersChangedHandler HoldingRegistersChanged;

        public delegate void NumberOfConnectedClientsChangedHandler();
        public event NumberOfConnectedClientsChangedHandler NumberOfConnectedClientsChanged;

        public delegate void LogDataChangedHandler();
        public event LogDataChangedHandler LogDataChanged;
        #endregion

        public void Listen()
        {
            
            listenerThread = new Thread(ListenerThread);
            listenerThread.Start();
        }

        public void StopListening()
        {
        	if (SerialFlag & (serialport != null))
        	{
        		if (serialport.IsOpen)
        			serialport.Close();
                shouldStop = true;
            }
            try
            {
                tcpHandler.Disconnect();
                listenerThread.Abort();
                
            }
            catch (Exception) { }
            listenerThread.Join();
            try
            {

                clientConnectionThread.Abort();
            }
            catch (Exception) { }
        }
        
        private void ListenerThread()
        {
            if (!udpFlag & !serialFlag)
            {
                if (udpClient != null)
                {
                    try
                    {
                        udpClient.Close();
                    }
                    catch (Exception) { }
                }
                tcpHandler = new TCPHandler(LocalIPAddress, port);
                if (debug) StoreLogData.Instance.Store($"EasyModbus Server listing for incomming data at Port {port}, local IP {LocalIPAddress}", System.DateTime.Now);
                tcpHandler.dataChanged += new TCPHandler.DataChanged(ProcessReceivedData);
                tcpHandler.numberOfClientsChanged += new TCPHandler.NumberOfClientsChanged(numberOfClientsChanged);
            }
            else if (serialFlag)
            {
                if (serialport == null)
                {
                    if (debug) StoreLogData.Instance.Store("EasyModbus RTU-Server listing for incomming data at Serial Port " + serialPort, System.DateTime.Now);
                    serialport = new SerialPort();
                    serialport.PortName = serialPort;
                    serialport.BaudRate = this.baudrate;
                    serialport.Parity = this.parity;
                    serialport.StopBits = stopBits;
                    serialport.WriteTimeout = 10000;
                    serialport.ReadTimeout = 1000;
                    serialport.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
                    serialport.Open();
                }
            }
            else
               while (!shouldStop)
               {
            	if (udpFlag)
            	{
                    if (udpClient == null | PortChanged)
                    {
                        IPEndPoint localEndoint = new IPEndPoint(LocalIPAddress, port);
                        udpClient = new UdpClient(localEndoint);
                        if (debug) StoreLogData.Instance.Store($"EasyModbus Server listing for incomming data at Port {port}, local IP {LocalIPAddress}", System.DateTime.Now);
                        udpClient.Client.ReceiveTimeout = 1000;
                        iPEndPoint = new IPEndPoint(IPAddress.Any, port);
                        PortChanged = false;                      
                    }
                    if (tcpHandler != null)
                        tcpHandler.Disconnect();
                    try
                    {                       
                        bytes = udpClient.Receive(ref iPEndPoint);
                        portIn = iPEndPoint.Port;
                        NetworkConnectionParameter networkConnectionParameter = new NetworkConnectionParameter();
                        networkConnectionParameter.bytes = bytes;
                        ipAddressIn = iPEndPoint.Address;
                        networkConnectionParameter.portIn = portIn;
                        networkConnectionParameter.ipAddressIn = ipAddressIn;
                        ParameterizedThreadStart pts = new ParameterizedThreadStart(this.ProcessReceivedData);
                        Thread processDataThread = new Thread(pts);
                        processDataThread.Start(networkConnectionParameter);
                    }
                    catch (Exception)
                    {                       
                    }    
            	}

                }
        }
    
		#region SerialHandler
        private bool dataReceived = false;
        private byte[] readBuffer = new byte[2094];
        private DateTime lastReceive;
        private int nextSign = 0;
        private void DataReceivedHandler(object sender,
                        SerialDataReceivedEventArgs e)
        {
            int silence = 4000 / baudrate;
            if ((DateTime.Now.Ticks - lastReceive.Ticks) > TimeSpan.TicksPerMillisecond*silence)
                nextSign = 0;


            SerialPort sp = (SerialPort)sender;

            int numbytes = sp.BytesToRead;
            byte[] rxbytearray = new byte[numbytes];

            sp.Read(rxbytearray, 0, numbytes);
            
            Array.Copy(rxbytearray, 0,  readBuffer, nextSign, rxbytearray.Length);
            lastReceive= DateTime.Now;
            nextSign = numbytes+ nextSign;
            if (ModbusClient.DetectValidModbusFrame(readBuffer, nextSign))
            {
                
                dataReceived = true;
                nextSign= 0;

                    NetworkConnectionParameter networkConnectionParameter = new NetworkConnectionParameter();
                    networkConnectionParameter.bytes = readBuffer;
                    ParameterizedThreadStart pts = new ParameterizedThreadStart(this.ProcessReceivedData);
                    Thread processDataThread = new Thread(pts);
                    processDataThread.Start(networkConnectionParameter);
                    dataReceived = false;
                
            }
            else
                dataReceived = false;
        }
		#endregion
 
		#region Method numberOfClientsChanged
        private void numberOfClientsChanged()
        {
            numberOfConnections = tcpHandler.NumberOfConnectedClients;
            if (NumberOfConnectedClientsChanged != null)
                NumberOfConnectedClientsChanged();
        }
        #endregion
        

        object lockProcessReceivedData = new object();
        #region Method ProcessReceivedData
        public void ProcessReceivedData(object networkConnectionParameter)
        {
            lock (lockProcessReceivedData)
            {
                NetworkConnectionParameter netParam = (NetworkConnectionParameter)networkConnectionParameter;
                clientIp = ((IPEndPoint)netParam.tcpClient.Client.RemoteEndPoint).Address;
         
                Byte[] bytes = new byte[((NetworkConnectionParameter)networkConnectionParameter).bytes.Length];
                if (debug) StoreLogData.Instance.Store("Received Data: " + BitConverter.ToString(bytes), System.DateTime.Now);
                NetworkStream stream = ((NetworkConnectionParameter)networkConnectionParameter).stream;
                int portIn = ((NetworkConnectionParameter)networkConnectionParameter).portIn;
                
                IPAddress ipAddressIn = ((NetworkConnectionParameter)networkConnectionParameter).ipAddressIn;
                

                Array.Copy(((NetworkConnectionParameter)networkConnectionParameter).bytes, 0, bytes, 0, ((NetworkConnectionParameter)networkConnectionParameter).bytes.Length);

                ModbusProtocol receiveDataThread = new ModbusProtocol();
                ModbusProtocol sendDataThread = new ModbusProtocol();

                try
                {
                    UInt16[] wordData = new UInt16[1];
                    byte[] byteData = new byte[2];
                    receiveDataThread.timeStamp = DateTime.Now;
                    receiveDataThread.request = true;
                    if (!serialFlag)
                    {
                        //Lese Transaction identifier
                        byteData[1] = bytes[0];
                        byteData[0] = bytes[1];
                        Buffer.BlockCopy(byteData, 0, wordData, 0, 2);
                        receiveDataThread.transactionIdentifier = wordData[0];

                        //Lese Protocol identifier
                        byteData[1] = bytes[2];
                        byteData[0] = bytes[3];
                        Buffer.BlockCopy(byteData, 0, wordData, 0, 2);
                        receiveDataThread.protocolIdentifier = wordData[0];

                        //Lese length
                        byteData[1] = bytes[4];
                        byteData[0] = bytes[5];
                        Buffer.BlockCopy(byteData, 0, wordData, 0, 2);
                        receiveDataThread.length = wordData[0];
                    }

                    //Lese unit identifier

                    receiveDataThread.unitIdentifier = bytes[6 - 6 * Convert.ToInt32(serialFlag)];
                    CurrentUnitIdentifier = receiveDataThread.unitIdentifier;

                    //Check UnitIdentifier
                    if (receiveDataThread.unitIdentifier == 0)
                        return;

                    // Lese function code
                    receiveDataThread.functionCode = bytes[7 - 6 * Convert.ToInt32(serialFlag)];
                    _functionCode = receiveDataThread.functionCode;

                    // Lese starting address 
                    byteData[1] = bytes[8 - 6 * Convert.ToInt32(serialFlag)];
                    byteData[0] = bytes[9 - 6 * Convert.ToInt32(serialFlag)];
                    Buffer.BlockCopy(byteData, 0, wordData, 0, 2);
                    receiveDataThread.startingAdress = wordData[0];

                    _lastStartingAddress = receiveDataThread.startingAdress;

                    if (receiveDataThread.functionCode <= 4)
                    {
                        // Lese quantity
                        byteData[1] = bytes[10 - 6 * Convert.ToInt32(serialFlag)];
                        byteData[0] = bytes[11 - 6 * Convert.ToInt32(serialFlag)];
                        Buffer.BlockCopy(byteData, 0, wordData, 0, 2);
                        receiveDataThread.quantity = wordData[0];
                        _lastQuantity = receiveDataThread.quantity;
                    }

                    if (receiveDataThread.functionCode == 5)
                    {
                        receiveDataThread.receiveCoilValues = new ushort[1];
                        // Lese Value
                        byteData[1] = bytes[10 - 6 * Convert.ToInt32(serialFlag)];
                        byteData[0] = bytes[11 - 6 * Convert.ToInt32(serialFlag)];
                        Buffer.BlockCopy(byteData, 0, receiveDataThread.receiveCoilValues, 0, 2);
                    }
                    if (receiveDataThread.functionCode == 6)
                    {
                        receiveDataThread.receiveRegisterValues = new ushort[1];
                        // Lese Value
                        byteData[1] = bytes[10 - 6 * Convert.ToInt32(serialFlag)];
                        byteData[0] = bytes[11 - 6 * Convert.ToInt32(serialFlag)];
                        Buffer.BlockCopy(byteData, 0, receiveDataThread.receiveRegisterValues, 0, 2);
                    }
                    if (receiveDataThread.functionCode == 15)
                    {
                        // Lese quantity
                        byteData[1] = bytes[10 - 6 * Convert.ToInt32(serialFlag)];
                        byteData[0] = bytes[11 - 6 * Convert.ToInt32(serialFlag)];
                        Buffer.BlockCopy(byteData, 0, wordData, 0, 2);
                        receiveDataThread.quantity = wordData[0];

                        receiveDataThread.byteCount = bytes[12 - 6 * Convert.ToInt32(serialFlag)];

                        if ((receiveDataThread.byteCount % 2) != 0)
                            receiveDataThread.receiveCoilValues = new ushort[receiveDataThread.byteCount / 2 + 1];
                        else
                            receiveDataThread.receiveCoilValues = new ushort[receiveDataThread.byteCount / 2];
                        // Lese Value
                        Buffer.BlockCopy(bytes, 13 - 6 * Convert.ToInt32(serialFlag), receiveDataThread.receiveCoilValues, 0, receiveDataThread.byteCount);
                    }
                    if (receiveDataThread.functionCode == 16)
                    {
                        // Lese quantity
                        byteData[1] = bytes[10 - 6 * Convert.ToInt32(serialFlag)];
                        byteData[0] = bytes[11 - 6 * Convert.ToInt32(serialFlag)];
                        Buffer.BlockCopy(byteData, 0, wordData, 0, 2);
                        receiveDataThread.quantity = wordData[0];

                        receiveDataThread.byteCount = bytes[12 - 6 * Convert.ToInt32(serialFlag)];
                        receiveDataThread.receiveRegisterValues = new ushort[receiveDataThread.quantity];
                        for (int i = 0; i < receiveDataThread.quantity; i++)
                        {
                            // Lese Value
                            byteData[1] = bytes[13 + i * 2 - 6 * Convert.ToInt32(serialFlag)];
                            byteData[0] = bytes[14 + i * 2 - 6 * Convert.ToInt32(serialFlag)];
                            Buffer.BlockCopy(byteData, 0, receiveDataThread.receiveRegisterValues, i * 2, 2);
                        }

                    }
                    if (receiveDataThread.functionCode == 23)
                    {
                        // Lese starting Address Read
                        byteData[1] = bytes[8 - 6 * Convert.ToInt32(serialFlag)];
                        byteData[0] = bytes[9 - 6 * Convert.ToInt32(serialFlag)];
                        Buffer.BlockCopy(byteData, 0, wordData, 0, 2);
                        receiveDataThread.startingAddressRead = wordData[0];
                        // Lese quantity Read
                        byteData[1] = bytes[10 - 6 * Convert.ToInt32(serialFlag)];
                        byteData[0] = bytes[11 - 6 * Convert.ToInt32(serialFlag)];
                        Buffer.BlockCopy(byteData, 0, wordData, 0, 2);
                        receiveDataThread.quantityRead = wordData[0];
                        // Lese starting Address Write
                        byteData[1] = bytes[12 - 6 * Convert.ToInt32(serialFlag)];
                        byteData[0] = bytes[13 - 6 * Convert.ToInt32(serialFlag)];
                        Buffer.BlockCopy(byteData, 0, wordData, 0, 2);
                        receiveDataThread.startingAddressWrite = wordData[0];
                        // Lese quantity Write
                        byteData[1] = bytes[14 - 6 * Convert.ToInt32(serialFlag)];
                        byteData[0] = bytes[15 - 6 * Convert.ToInt32(serialFlag)];
                        Buffer.BlockCopy(byteData, 0, wordData, 0, 2);
                        receiveDataThread.quantityWrite = wordData[0];

                        receiveDataThread.byteCount = bytes[16 - 6 * Convert.ToInt32(serialFlag)];
                        receiveDataThread.receiveRegisterValues = new ushort[receiveDataThread.quantityWrite];
                        for (int i = 0; i < receiveDataThread.quantityWrite; i++)
                        {
                            // Lese Value
                            byteData[1] = bytes[17 + i * 2 - 6 * Convert.ToInt32(serialFlag)];
                            byteData[0] = bytes[18 + i * 2 - 6 * Convert.ToInt32(serialFlag)];
                            Buffer.BlockCopy(byteData, 0, receiveDataThread.receiveRegisterValues, i * 2, 2);
                        }
                    }
                }
                catch (Exception exc)
                { }
                this.CreateAnswer(receiveDataThread, sendDataThread, stream, portIn, ipAddressIn);
                //this.sendAnswer();
                this.CreateLogData(receiveDataThread, sendDataThread);

                if (LogDataChanged != null)
                    LogDataChanged();
            }
        }
        #endregion
        private bool IsClientAuthorized(IPAddress clientIp, int functionCode)
        {
            // if not using white list, every client is authorised
            if (!UseWhiteList)
                return true;

            bool requiresWrite = false;
            switch (functionCode)
            {
                case 5:
                case 6:
                case 15:
                case 16:
                case 23:
                    requiresWrite = true;
                    break;
            }

            string clientIpStr = clientIp.ToString();
            var client = WhiteList.FirstOrDefault(c => c.IpAddress == clientIpStr);
            if (client == null)
                return false; // client is not on the white list

            // if write operation, client needs "W" permission
            if (requiresWrite && !string.Equals(client.Permission, "W", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }
        #region Method CreateAnswer
        private void CreateAnswer(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
        {
            
            if (!IsClientAuthorized(clientIp, receiveData.functionCode))
            {
                // if client is not on the white list or does not have permissions – sending exception message
                sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                sendData.exceptionCode = 0x01; 
                sendData.length = 0x03; 

                sendException(sendData.errorCode, sendData.exceptionCode, receiveData, sendData, stream, portIn, ipAddressIn);
                return;
            }
            
            if (OperationModeHandler is ManualModeHandler)
            {
                //for manual mode if register is not defind
                if (!IsRegisterDefined(receiveData))
                {
                    // odpowiedź exception:
                    sendData.errorCode = (byte)(receiveData.functionCode | 0x80);
                    sendData.exceptionCode = 0x02; // Illegal Data Address
                    sendData.length = 0x03; 

                    sendException(sendData.errorCode, sendData.exceptionCode, receiveData, sendData, stream, portIn, ipAddressIn);
                    return;
                }
            }
                      
            

            switch (receiveData.functionCode)
            {
                // Read Coils
                case 1:
                    if (!FunctionCode1Disabled)
                        this.ReadCoils(receiveData, sendData, stream, portIn, ipAddressIn);
                    else
                    {
                        sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                        sendData.exceptionCode = 1;
                        sendException(sendData.errorCode, sendData.exceptionCode, receiveData, sendData, stream, portIn, ipAddressIn);
                    }
                    break;
                // Read Input Registers
                case 2:
                    if (!FunctionCode2Disabled)
                        this.ReadDiscreteInputs(receiveData, sendData, stream, portIn, ipAddressIn);
                    else
                    {
                        sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                        sendData.exceptionCode = 1;
                        sendException(sendData.errorCode, sendData.exceptionCode, receiveData, sendData, stream, portIn, ipAddressIn);
                    }
                    
                    break;
                // Read Holding Registers
                case 3:
                    if (!FunctionCode3Disabled)
                        this.ReadHoldingRegisters(receiveData, sendData, stream, portIn, ipAddressIn);
                    else
                    {
                        sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                        sendData.exceptionCode = 1;
                        sendException(sendData.errorCode, sendData.exceptionCode, receiveData, sendData, stream, portIn, ipAddressIn);
                    }
                    
                    break;
                // Read Input Registers
                case 4:
                    if (!FunctionCode4Disabled)
                        this.ReadInputRegisters(receiveData, sendData, stream, portIn, ipAddressIn);
                    else
                    {
                        sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                        sendData.exceptionCode = 1;
                        sendException(sendData.errorCode, sendData.exceptionCode, receiveData, sendData, stream, portIn, ipAddressIn);
                    }
                    
                    break;
                // Write single coil
                case 5:
                    if (!FunctionCode5Disabled)
                        this.WriteSingleCoil(receiveData, sendData, stream, portIn, ipAddressIn);
                    else
                    {
                        sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                        sendData.exceptionCode = 1;
                        sendException(sendData.errorCode, sendData.exceptionCode, receiveData, sendData, stream, portIn, ipAddressIn);
                    }
                    
                    break;
                // Write single register
                case 6:
                    if (!FunctionCode6Disabled)
                        this.WriteSingleRegister(receiveData, sendData, stream, portIn, ipAddressIn);
                    else
                    {
                        sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                        sendData.exceptionCode = 1;
                        sendException(sendData.errorCode, sendData.exceptionCode, receiveData, sendData, stream, portIn, ipAddressIn);
                    }
                    
                        break;
                // Write Multiple coils
                case 15:
                        if (!FunctionCode15Disabled)
                            this.WriteMultipleCoils(receiveData, sendData, stream, portIn, ipAddressIn);
                        else
                        {
                            sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                            sendData.exceptionCode = 1;
                            sendException(sendData.errorCode, sendData.exceptionCode, receiveData, sendData, stream, portIn, ipAddressIn);
                        }

                        break;
                // Write Multiple registers
                case 16:
                        if (!FunctionCode16Disabled)
                            this.WriteMultipleRegisters(receiveData, sendData, stream, portIn, ipAddressIn);
                        else
                        {
                            sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                            sendData.exceptionCode = 1;
                            sendException(sendData.errorCode, sendData.exceptionCode, receiveData, sendData, stream, portIn, ipAddressIn);
                        }

                        break;
                // Error: Function Code not supported
                case 23:
                        if (!FunctionCode23Disabled)
                            this.ReadWriteMultipleRegisters(receiveData, sendData, stream, portIn, ipAddressIn);
                        else
                        {
                            sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                            sendData.exceptionCode = 1;
                            sendException(sendData.errorCode, sendData.exceptionCode, receiveData, sendData, stream, portIn, ipAddressIn);
                        }

                        break;
                // Error: Function Code not supported
                default: sendData.errorCode = (byte) (receiveData.functionCode + 0x80);
                        sendData.exceptionCode = 1;
                        sendException(sendData.errorCode, sendData.exceptionCode, receiveData, sendData, stream, portIn, ipAddressIn);
                        break;
            }
            sendData.timeStamp = DateTime.Now;
        }
        
        private bool IsRegisterDefined(ModbusProtocol receiveData)
        {
            var rm = RegisterManager.Instance;

            if (receiveData.functionCode == 3)
            {
                for (ushort i = 0; i < receiveData.quantity; i++)
                {
                    ushort currentAddress = (ushort)(receiveData.startingAdress + i);
                    var register = rm.HoldingRegisters.FirstOrDefault(
                        r => r.SlaveId == receiveData.unitIdentifier &&
                             currentAddress >= r.StartAddress &&
                             currentAddress < r.StartAddress + r.Quantity &&
                             r.IsActive);

                    if (register == null)
                    {
                        return false;
                    }
                }
                return true;
            }

            if (receiveData.functionCode == 6 || receiveData.functionCode == 16)
            {
                for (ushort i = 0; i < receiveData.quantity; i++)
                {
                    ushort currentAddress = (ushort)(receiveData.startingAdress + i);
                    var register = rm.HoldingRegisters.FirstOrDefault(
                        r => r.SlaveId == receiveData.unitIdentifier &&
                             currentAddress >= r.StartAddress &&
                             currentAddress < r.StartAddress + r.Quantity &&
                             r.AccessMode == "W" &&
                             r.IsActive);

                    if (register == null)
                    {
                        return false;
                    }
                }
                return true;
            }
            else if (receiveData.functionCode == 4)
            {
                for (ushort i = 0; i < receiveData.quantity; i++)
                {
                    ushort currentAddress = (ushort)(receiveData.startingAdress + i);
                    var register = rm.InputRegisters.FirstOrDefault(
                        r => r.SlaveId == receiveData.unitIdentifier &&
                             currentAddress >= r.StartAddress &&
                             currentAddress < r.StartAddress + r.Quantity &&
                             r.IsActive);

                    if (register == null)
                    {
                        return false;
                    }
                }
                return true;
            }

            else if (receiveData.functionCode == 1)
            {
                for (ushort i = 0; i < receiveData.quantity; i++)
                {
                    ushort currentAddress = (ushort)(receiveData.startingAdress + i);
                    var register = rm.Coils.FirstOrDefault(
                        r => r.SlaveId == receiveData.unitIdentifier &&
                             currentAddress >= r.StartAddress &&
                             currentAddress < r.StartAddress + r.Quantity &&
                             r.IsActive);

                    if (register == null)
                    {
                        return false;
                    }
                }
                return true;
            }
       
            else if(receiveData.functionCode==5 || receiveData.functionCode == 15)
            {
                for (ushort i = 0; i < receiveData.quantity; i++)
                {
                    ushort currentAddress = (ushort)(receiveData.startingAdress + i);
                    var register = rm.Coils.FirstOrDefault(
                        r => r.SlaveId == receiveData.unitIdentifier &&
                             currentAddress >= r.StartAddress &&
                             currentAddress < r.StartAddress + r.Quantity &&
                             r.AccessMode == "W" &&
                             r.IsActive);

                    if (register == null)
                    {
                       
                        return false;
                    }
                }
                return true;
            }
            return false; 
            
        }
        #endregion

        private void ReadCoils(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
        {
            sendData.response = true;

            sendData.transactionIdentifier = receiveData.transactionIdentifier;
            sendData.protocolIdentifier = receiveData.protocolIdentifier;

            sendData.unitIdentifier = receiveData.unitIdentifier;
            sendData.functionCode = receiveData.functionCode;
            if ((receiveData.quantity < 1) | (receiveData.quantity > 0x07D0))  //Invalid quantity
            {
                sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                sendData.exceptionCode = 3;
            }
            if (((receiveData.startingAdress + 1 + receiveData.quantity) > 65535) | (receiveData.startingAdress < 0))     //Invalid Starting adress or Starting address + quantity
            {
                sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                sendData.exceptionCode = 2;
            }
            if (sendData.exceptionCode == 0)
            {
                if ((receiveData.quantity % 8) == 0)
                    sendData.byteCount = (byte)(receiveData.quantity / 8);
                else
                    sendData.byteCount = (byte)(receiveData.quantity / 8 + 1);

                sendData.sendCoilValues = new bool[receiveData.quantity];
                lock (lockCoils)
                {
                    int offset = receiveData.unitIdentifier * 10000;
                    Buffer.BlockCopy(coils.localArray,
                        offset + receiveData.startingAdress,
                        sendData.sendCoilValues, 0,
                        receiveData.quantity);
                }
                   // Array.Copy(coils.localArray, receiveData.startingAdress + 1, sendData.sendCoilValues, 0, receiveData.quantity);
            }
            if (true)
            {
                Byte[] data;

                if (sendData.exceptionCode > 0)
                	data = new byte[9 + 2*Convert.ToInt32(serialFlag)];
                else
                   	data = new byte[9 + sendData.byteCount+ 2*Convert.ToInt32(serialFlag)];
              
                Byte[] byteData = new byte[2];

                sendData.length = (byte)(data.Length - 6);

                //Send Transaction identifier
                byteData = BitConverter.GetBytes((int)sendData.transactionIdentifier);
                data[0] = byteData[1];
                data[1] = byteData[0];

                //Send Protocol identifier
                byteData = BitConverter.GetBytes((int)sendData.protocolIdentifier);
                data[2] = byteData[1];
                data[3] = byteData[0];

                //Send length
                byteData = BitConverter.GetBytes((int)sendData.length);
                data[4] = byteData[1];
                data[5] = byteData[0];
                //Unit Identifier
                data[6] = sendData.unitIdentifier;

                //Function Code
                data[7] = sendData.functionCode;

                //ByteCount
                data[8] = sendData.byteCount;

                if (sendData.exceptionCode > 0)
                {
                    data[7] = sendData.errorCode;
                    data[8] = sendData.exceptionCode;
                    sendData.sendCoilValues = null;
                }

                if (sendData.sendCoilValues != null)
                    for (int i = 0; i < (sendData.byteCount); i++)
                    {
                        byteData = new byte[2];
                        for (int j = 0; j < 8; j++)
                        {

                            byte boolValue;
                            if (sendData.sendCoilValues[i * 8 + j] == true)
                                boolValue = 1;
                            else
                                boolValue = 0;
                            byteData[1] = (byte)((byteData[1]) | (boolValue << j));
                            if ((i * 8 + j + 1) >= sendData.sendCoilValues.Length)
                                break;
                        }
                        data[9 + i] = byteData[1];
                    }
                try
                {
                    if (serialFlag)
                    {
                        if (!serialport.IsOpen)
                            throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened");
                        //Create CRC
                        sendData.crc = ModbusClient.calculateCRC(data, Convert.ToUInt16(data.Length - 8), 6);
                        byteData = BitConverter.GetBytes((int)sendData.crc);
                        data[data.Length - 2] = byteData[0];
                        data[data.Length - 1] = byteData[1];
                        serialport.Write(data, 6, data.Length - 6);
                        if (debug)
                        {
                            byte[] debugData = new byte[data.Length - 6];
                            Array.Copy(data, 6, debugData, 0, data.Length - 6);
                            if (debug) StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                        }
                    }
                    else if (udpFlag)
                    {
                        //UdpClient udpClient = new UdpClient();
                        IPEndPoint endPoint = new IPEndPoint(ipAddressIn, portIn);
                        if (debug) StoreLogData.Instance.Store("Send Data: " + BitConverter.ToString(data), System.DateTime.Now);
                        udpClient.Send(data, data.Length, endPoint);

                    }
                    else
                    {
                        stream.Write(data, 0, data.Length);
                        if (debug) StoreLogData.Instance.Store("Send Data: " + BitConverter.ToString(data), System.DateTime.Now);
                    }
                }
                catch (Exception) { }
            }  
        }

        private void ReadDiscreteInputs(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
        {
            sendData.response = true;

            sendData.transactionIdentifier = receiveData.transactionIdentifier;
            sendData.protocolIdentifier = receiveData.protocolIdentifier;

            sendData.unitIdentifier = receiveData.unitIdentifier;
            sendData.functionCode = receiveData.functionCode;
            if ((receiveData.quantity < 1) | (receiveData.quantity > 0x07D0))  //Invalid quantity
            {
                sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                sendData.exceptionCode = 3;
            }
            if (((receiveData.startingAdress + 1 + receiveData.quantity) > 65535) | (receiveData.startingAdress < 0))   //Invalid Starting adress or Starting address + quantity
            {
                sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                sendData.exceptionCode = 2;
            }
            if (sendData.exceptionCode == 0)
            {
                if ((receiveData.quantity % 8) == 0)
                    sendData.byteCount = (byte)(receiveData.quantity / 8);
                else
                    sendData.byteCount = (byte)(receiveData.quantity / 8 + 1);

                sendData.sendCoilValues = new bool[receiveData.quantity];
                Array.Copy(discreteInputs.localArray, receiveData.startingAdress + 1, sendData.sendCoilValues, 0, receiveData.quantity);
            }
            if (true)
            {
                Byte[] data;
                if (sendData.exceptionCode > 0)
                    data = new byte[9 + 2 * Convert.ToInt32(serialFlag)];
                else
                    data = new byte[9 + sendData.byteCount + 2 * Convert.ToInt32(serialFlag)];
                Byte[] byteData = new byte[2];
                sendData.length = (byte)(data.Length - 6);

                //Send Transaction identifier
                byteData = BitConverter.GetBytes((int)sendData.transactionIdentifier);
                data[0] = byteData[1];
                data[1] = byteData[0];

                //Send Protocol identifier
                byteData = BitConverter.GetBytes((int)sendData.protocolIdentifier);
                data[2] = byteData[1];
                data[3] = byteData[0];

                //Send length
                byteData = BitConverter.GetBytes((int)sendData.length);
                data[4] = byteData[1];
                data[5] = byteData[0];

                //Unit Identifier
                data[6] = sendData.unitIdentifier;

                //Function Code
                data[7] = sendData.functionCode;

                //ByteCount
                data[8] = sendData.byteCount;


                if (sendData.exceptionCode > 0)
                {
                    data[7] = sendData.errorCode;
                    data[8] = sendData.exceptionCode;
                    sendData.sendCoilValues = null;
                }

                if (sendData.sendCoilValues != null)
                    for (int i = 0; i < (sendData.byteCount); i++)
                    {
                        byteData = new byte[2];
                        for (int j = 0; j < 8; j++)
                        {

                            byte boolValue;
                            if (sendData.sendCoilValues[i * 8 + j] == true)
                                boolValue = 1;
                            else
                                boolValue = 0;
                            byteData[1] = (byte)((byteData[1]) | (boolValue << j));
                            if ((i * 8 + j + 1) >= sendData.sendCoilValues.Length)
                                break;
                        }
                        data[9 + i] = byteData[1];
                    }

                try
                {
                    if (serialFlag)
                    {
                        if (!serialport.IsOpen)
                            throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened");
                        //Create CRC
                        sendData.crc = ModbusClient.calculateCRC(data, Convert.ToUInt16(data.Length - 8), 6);
                        byteData = BitConverter.GetBytes((int)sendData.crc);
                        data[data.Length - 2] = byteData[0];
                        data[data.Length - 1] = byteData[1];
                        serialport.Write(data, 6, data.Length - 6);
                        if (debug)
                        {
                            byte[] debugData = new byte[data.Length - 6];
                            Array.Copy(data, 6, debugData, 0, data.Length - 6);
                            if (debug) StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                        }
                    }
                    else if (udpFlag)
                    {
                        //UdpClient udpClient = new UdpClient();
                        IPEndPoint endPoint = new IPEndPoint(ipAddressIn, portIn);
                        udpClient.Send(data, data.Length, endPoint);

                    }
                    else
                    {
                        stream.Write(data, 0, data.Length);
                        if(debug) StoreLogData.Instance.Store("Send Data: " + BitConverter.ToString(data), System.DateTime.Now);
                    }
                }
                catch (Exception) { }
            }
        }

        private void ReadHoldingRegisters(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
        {
            
            sendData.response = true;

            sendData.transactionIdentifier = receiveData.transactionIdentifier;
            sendData.protocolIdentifier = receiveData.protocolIdentifier;

            sendData.unitIdentifier = receiveData.unitIdentifier;
            sendData.functionCode = receiveData.functionCode;
            if ((receiveData.quantity < 1) | (receiveData.quantity > 0x007D))  //Invalid quantity
            {
                sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                sendData.exceptionCode = 3;
            }
            if (((receiveData.startingAdress + 1 + receiveData.quantity) > 65535)  | (receiveData.startingAdress < 0))   //Invalid Starting adress or Starting address + quantity
            {
                sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                sendData.exceptionCode = 2;
            }
            if (sendData.exceptionCode == 0)
            {
                sendData.byteCount = (byte)(2 * receiveData.quantity);
                sendData.sendRegisterValues = new Int16[receiveData.quantity];
                lock (lockHoldingRegisters)
                {
                    int offset = receiveData.unitIdentifier * 10000;
                    Buffer.BlockCopy(holdingRegisters.localArray,
                        (offset + receiveData.startingAdress) * 2 ,
                        sendData.sendRegisterValues, 0,
                        receiveData.quantity * 2);
                }
                   // Buffer.BlockCopy(holdingRegisters.localArray, receiveData.startingAdress * 2 + 2, sendData.sendRegisterValues, 0, receiveData.quantity * 2);
                
            }
                if (sendData.exceptionCode > 0)
                    sendData.length = 0x03;
                else
                    sendData.length = (ushort)(0x03 + sendData.byteCount);
            
            if (true)
            {
                Byte[] data;
                if (sendData.exceptionCode > 0)
                    data = new byte[9 + 2 * Convert.ToInt32(serialFlag)];
                else
                    data = new byte[9 + sendData.byteCount + 2 * Convert.ToInt32(serialFlag)];
                Byte[] byteData = new byte[2];
                sendData.length = (byte)(data.Length - 6);

                //Send Transaction identifier
                byteData = BitConverter.GetBytes((int)sendData.transactionIdentifier);
                data[0] = byteData[1];
                data[1] = byteData[0];

                //Send Protocol identifier
                byteData = BitConverter.GetBytes((int)sendData.protocolIdentifier);
                data[2] = byteData[1];
                data[3] = byteData[0];

                //Send length
                byteData = BitConverter.GetBytes((int)sendData.length);
                data[4] = byteData[1];
                data[5] = byteData[0];

                //Unit Identifier
                data[6] = sendData.unitIdentifier;

                //Function Code
                data[7] = sendData.functionCode;

                //ByteCount
                data[8] = sendData.byteCount;

                if (sendData.exceptionCode > 0)
                {
                    data[7] = sendData.errorCode;
                    data[8] = sendData.exceptionCode;
                    sendData.sendRegisterValues = null;
                }
   

                if (sendData.sendRegisterValues != null)
                    for (int i = 0; i < (sendData.byteCount / 2); i++)
                    {
                        byteData = BitConverter.GetBytes((Int16)sendData.sendRegisterValues[i]);
                        data[9 + i * 2] = byteData[1];
                        data[10 + i * 2] = byteData[0];
                    }
                try
                {
                    if (serialFlag)
                    {
                        if (!serialport.IsOpen)
                            throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened");
                        //Create CRC
                        sendData.crc = ModbusClient.calculateCRC(data, Convert.ToUInt16(data.Length - 8), 6);
                        byteData = BitConverter.GetBytes((int)sendData.crc);
                        data[data.Length - 2] = byteData[0];
                        data[data.Length - 1] = byteData[1];
                        serialport.Write(data, 6, data.Length - 6);
                        if (debug)
                        {
                            byte[] debugData = new byte[data.Length - 6];
                            Array.Copy(data, 6, debugData, 0, data.Length - 6);
                            if (debug) StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                        }
                    }
                    else if (udpFlag)
                    {
                        //UdpClient udpClient = new UdpClient();
                        IPEndPoint endPoint = new IPEndPoint(ipAddressIn, portIn);
                        udpClient.Send(data, data.Length, endPoint);

                    }
                    else
                    {
                        stream.Write(data, 0, data.Length);
                        if (debug) StoreLogData.Instance.Store("Send Data: " + BitConverter.ToString(data), System.DateTime.Now);
                    }
                }
                catch (Exception) { }
            }       
        }

        private void ReadInputRegisters(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
        {
            sendData.response = true;

            sendData.transactionIdentifier = receiveData.transactionIdentifier;
            sendData.protocolIdentifier = receiveData.protocolIdentifier;

            sendData.unitIdentifier = receiveData.unitIdentifier;
            sendData.functionCode = receiveData.functionCode;
            if ((receiveData.quantity < 1) | (receiveData.quantity > 0x007D))  //Invalid quantity
            {
                sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                sendData.exceptionCode = 3;
            }
            if (((receiveData.startingAdress + 1 + receiveData.quantity) > 65535)  | (receiveData.startingAdress < 0))   //Invalid Starting adress or Starting address + quantity
            {
                sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                sendData.exceptionCode = 2;
            }
            if (sendData.exceptionCode == 0)
            {
                sendData.byteCount = (byte)(2 * receiveData.quantity);
                sendData.sendRegisterValues = new Int16[receiveData.quantity];

                int offset = receiveData.unitIdentifier * 10000;
                Buffer.BlockCopy(inputRegisters.localArray,
                    (offset + receiveData.startingAdress) * 2,
                    sendData.sendRegisterValues, 0,
                    receiveData.quantity * 2);
               // Buffer.BlockCopy(inputRegisters.localArray, receiveData.startingAdress * 2 + 2, sendData.sendRegisterValues, 0, receiveData.quantity * 2);
            }
                if (sendData.exceptionCode > 0)
                    sendData.length = 0x03;
                else
                    sendData.length = (ushort)(0x03 + sendData.byteCount);
            
            if (true)
            {
                Byte[] data;
                if (sendData.exceptionCode > 0)
                    data = new byte[9 + 2 * Convert.ToInt32(serialFlag)];
                else
                    data = new byte[9 + sendData.byteCount + 2 * Convert.ToInt32(serialFlag)];
                Byte[] byteData = new byte[2];
                sendData.length = (byte)(data.Length - 6);

                //Send Transaction identifier
                byteData = BitConverter.GetBytes((int)sendData.transactionIdentifier);
                data[0] = byteData[1];
                data[1] = byteData[0];

                //Send Protocol identifier
                byteData = BitConverter.GetBytes((int)sendData.protocolIdentifier);
                data[2] = byteData[1];
                data[3] = byteData[0];

                //Send length
                byteData = BitConverter.GetBytes((int)sendData.length);
                data[4] = byteData[1];
                data[5] = byteData[0];

                //Unit Identifier
                data[6] = sendData.unitIdentifier;

                //Function Code
                data[7] = sendData.functionCode;

                //ByteCount
                data[8] = sendData.byteCount;

                
                if (sendData.exceptionCode > 0)
                {
                    data[7] = sendData.errorCode;
                    data[8] = sendData.exceptionCode;
                    sendData.sendRegisterValues = null;
                }


                if (sendData.sendRegisterValues != null)
                    for (int i = 0; i < (sendData.byteCount / 2); i++)
                    {
                        byteData = BitConverter.GetBytes((Int16)sendData.sendRegisterValues[i]);
                        data[9 + i * 2] = byteData[1];
                        data[10 + i * 2] = byteData[0];
                    }
                try
                {
                    if (serialFlag)
                    {
                        if (!serialport.IsOpen)
                            throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened");
                        //Create CRC
                        sendData.crc = ModbusClient.calculateCRC(data, Convert.ToUInt16(data.Length - 8), 6);
                        byteData = BitConverter.GetBytes((int)sendData.crc);
                        data[data.Length - 2] = byteData[0];
                        data[data.Length - 1] = byteData[1];
                        serialport.Write(data, 6, data.Length - 6);
                        if (debug)
                        {
                            byte[] debugData = new byte[data.Length - 6];
                            Array.Copy(data, 6, debugData, 0, data.Length - 6);
                            if (debug) StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                        }

                    }
                    else if (udpFlag)
                    {
                        //UdpClient udpClient = new UdpClient();
                        IPEndPoint endPoint = new IPEndPoint(ipAddressIn, portIn);
                        udpClient.Send(data, data.Length, endPoint);

                    }
                    else
                    {
                        stream.Write(data, 0, data.Length);
                        if (debug) StoreLogData.Instance.Store("Send Data: " + BitConverter.ToString(data), System.DateTime.Now);
                    }
                }
                catch (Exception) { }
            }
        }

        private void WriteSingleCoil(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
        {
            sendData.response = true;

            sendData.transactionIdentifier = receiveData.transactionIdentifier;
            sendData.protocolIdentifier = receiveData.protocolIdentifier;

            sendData.unitIdentifier = receiveData.unitIdentifier;
            sendData.functionCode = receiveData.functionCode;
            sendData.startingAdress = receiveData.startingAdress;
            sendData.receiveCoilValues = receiveData.receiveCoilValues;
            if ((receiveData.receiveCoilValues[0] != 0x0000) & (receiveData.receiveCoilValues[0] != 0xFF00))  //Invalid Value
            {
                sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                sendData.exceptionCode = 3;
            }
            if (((receiveData.startingAdress + 1) > 65535)  | (receiveData.startingAdress < 0))    //Invalid Starting adress or Starting address + quantity
            {
                sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                sendData.exceptionCode = 2;
            }
            if (sendData.exceptionCode == 0)
            {
                if (receiveData.receiveCoilValues[0] == 0xFF00)
                {
                    lock (lockCoils)
                        coils[receiveData.startingAdress + 1] = true;
                }
                if (receiveData.receiveCoilValues[0] == 0x0000)
                {
                    lock (lockCoils)
                        coils[receiveData.startingAdress + 1] = false;
                }
            }
                if (sendData.exceptionCode > 0)
                    sendData.length = 0x03;
                else
                    sendData.length = 0x06;
            
            if (true)
            {
                Byte[] data;
                if (sendData.exceptionCode > 0)
                    data = new byte[9 + 2 * Convert.ToInt32(serialFlag)];
                else
                    data = new byte[12 + 2 * Convert.ToInt32(serialFlag)];

                Byte[] byteData = new byte[2];
                sendData.length = (byte)(data.Length - 6);

                //Send Transaction identifier
                byteData = BitConverter.GetBytes((int)sendData.transactionIdentifier);
                data[0] = byteData[1];
                data[1] = byteData[0];

                //Send Protocol identifier
                byteData = BitConverter.GetBytes((int)sendData.protocolIdentifier);
                data[2] = byteData[1];
                data[3] = byteData[0];

                //Send length
                byteData = BitConverter.GetBytes((int)sendData.length);
                data[4] = byteData[1];
                data[5] = byteData[0];

                //Unit Identifier
                data[6] = sendData.unitIdentifier;

                //Function Code
                data[7] = sendData.functionCode;



                if (sendData.exceptionCode > 0)
                {
                    data[7] = sendData.errorCode;
                    data[8] = sendData.exceptionCode;
                    sendData.sendRegisterValues = null;
                }
                else
                {
                    byteData = BitConverter.GetBytes((int)receiveData.startingAdress);
                    data[8] = byteData[1];
                    data[9] = byteData[0];
                    byteData = BitConverter.GetBytes((int)receiveData.receiveCoilValues[0]);
                    data[10] = byteData[1];
                    data[11] = byteData[0];
                }


                try
                {
                    if (serialFlag)
                    {
                        if (!serialport.IsOpen)
                            throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened");
                        //Create CRC
                        sendData.crc = ModbusClient.calculateCRC(data, Convert.ToUInt16(data.Length - 8), 6);
                        byteData = BitConverter.GetBytes((int)sendData.crc);
                        data[data.Length - 2] = byteData[0];
                        data[data.Length - 1] = byteData[1];
                        serialport.Write(data, 6, data.Length - 6);
                        if (debug)
                        {
                            byte[] debugData = new byte[data.Length - 6];
                            Array.Copy(data, 6, debugData, 0, data.Length - 6);
                            if (debug) StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                        }

                    }
                    else if (udpFlag)
                    {
                        //UdpClient udpClient = new UdpClient();
                        IPEndPoint endPoint = new IPEndPoint(ipAddressIn, portIn);
                        udpClient.Send(data, data.Length, endPoint);

                    }
                    else
                    {
                        stream.Write(data, 0, data.Length);
                        if (debug) StoreLogData.Instance.Store("Send Data: " + BitConverter.ToString(data), System.DateTime.Now);
                    }
                }
                catch (Exception) { }
                if (CoilsChanged != null)
                    CoilsChanged(receiveData.unitIdentifier,receiveData.startingAdress+1, 1);
            }
        }

        private void WriteSingleRegister(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
        {
            sendData.response = true;

            sendData.transactionIdentifier = receiveData.transactionIdentifier;
            sendData.protocolIdentifier = receiveData.protocolIdentifier;

            sendData.unitIdentifier = receiveData.unitIdentifier;
            sendData.functionCode = receiveData.functionCode;
            sendData.startingAdress = receiveData.startingAdress;
            sendData.receiveRegisterValues = receiveData.receiveRegisterValues;
           
            if ((receiveData.receiveRegisterValues[0] < 0x0000) | (receiveData.receiveRegisterValues[0] > 0xFFFF))  //Invalid Value
            {
                sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                sendData.exceptionCode = 3;
            }
            if (((receiveData.startingAdress + 1) > 65535)  | (receiveData.startingAdress < 0))    //Invalid Starting adress or Starting address + quantity
            {
                sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                sendData.exceptionCode = 2;
            }
            if (sendData.exceptionCode == 0)
            {
                lock (lockHoldingRegisters)
                    holdingRegisters[receiveData.startingAdress+1 ] = unchecked((short)receiveData.receiveRegisterValues[0]);
            }
                if (sendData.exceptionCode > 0)
                    sendData.length = 0x03;
                else
                    sendData.length = 0x06;
            
            if (true)
            {
                Byte[] data;
                if (sendData.exceptionCode > 0)
                    data = new byte[9 + 2 * Convert.ToInt32(serialFlag)];
                else
                    data = new byte[12 + 2 * Convert.ToInt32(serialFlag)];

                Byte[] byteData = new byte[2];
                sendData.length = (byte)(data.Length - 6);


                //Send Transaction identifier
                byteData = BitConverter.GetBytes((int)sendData.transactionIdentifier);
                data[0] = byteData[1];
                data[1] = byteData[0];

                //Send Protocol identifier
                byteData = BitConverter.GetBytes((int)sendData.protocolIdentifier);
                data[2] = byteData[1];
                data[3] = byteData[0];

                //Send length
                byteData = BitConverter.GetBytes((int)sendData.length);
                data[4] = byteData[1];
                data[5] = byteData[0];

                //Unit Identifier
                data[6] = sendData.unitIdentifier;

                //Function Code
                data[7] = sendData.functionCode;



                if (sendData.exceptionCode > 0)
                {
                    data[7] = sendData.errorCode;
                    data[8] = sendData.exceptionCode;
                    sendData.sendRegisterValues = null;
                }
                else
                {
                    byteData = BitConverter.GetBytes((int)receiveData.startingAdress);
                    data[8] = byteData[1];
                    data[9] = byteData[0];
                    byteData = BitConverter.GetBytes((int)receiveData.receiveRegisterValues[0]);
                    data[10] = byteData[1];
                    data[11] = byteData[0];
                }


                try
                {
                    if (serialFlag)
                    {
                        if (!serialport.IsOpen)
                            throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened");
                        //Create CRC
                        sendData.crc = ModbusClient.calculateCRC(data, Convert.ToUInt16(data.Length - 8), 6);
                        byteData = BitConverter.GetBytes((int)sendData.crc);
                        data[data.Length - 2] = byteData[0];
                        data[data.Length - 1] = byteData[1];
                        serialport.Write(data, 6, data.Length - 6);
                        if (debug)
                        {
                            byte[] debugData = new byte[data.Length - 6];
                            Array.Copy(data, 6, debugData, 0, data.Length - 6);
                            if (debug) StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                        }

                    }
                    else if (udpFlag)
                    {
                        //UdpClient udpClient = new UdpClient();
                        IPEndPoint endPoint = new IPEndPoint(ipAddressIn, portIn);
                        udpClient.Send(data, data.Length, endPoint);

                    }
                    else
                    {
                        stream.Write(data, 0, data.Length);
                        if (debug) StoreLogData.Instance.Store("Send Data: " + BitConverter.ToString(data), System.DateTime.Now);
                    }
                }
                catch (Exception) { }
                if (HoldingRegistersChanged != null)
                    HoldingRegistersChanged(receiveData.unitIdentifier,receiveData.startingAdress+1, 1);
            }
        }

        private void WriteMultipleCoils(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
        {
            sendData.response = true;

            sendData.transactionIdentifier = receiveData.transactionIdentifier;
            sendData.protocolIdentifier = receiveData.protocolIdentifier;

            sendData.unitIdentifier = receiveData.unitIdentifier;
            sendData.functionCode = receiveData.functionCode;
            sendData.startingAdress = receiveData.startingAdress;
            sendData.quantity = receiveData.quantity;
            
            if ((receiveData.quantity == 0x0000) | (receiveData.quantity > 0x07B0))  //Invalid Quantity
            {
                sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                sendData.exceptionCode = 3;
            }
            if ((((int)receiveData.startingAdress + 1 + (int)receiveData.quantity) > 65535)  | (receiveData.startingAdress < 0))    //Invalid Starting adress or Starting address + quantity
            {
                sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                sendData.exceptionCode = 2;
            }
            if (sendData.exceptionCode == 0)
            {
                lock (lockCoils)
                    for (int i = 0; i < receiveData.quantity; i++)
                    {
                        int shift = i % 16;
                    /*                if ((i == receiveData.quantity - 1) & (receiveData.quantity % 2 != 0))
                                    {
                                        if (shift < 8)
                                            shift = shift + 8;
                                        else
                                            shift = shift - 8;
                                    }*/
                        int mask = 0x1;
                        mask = mask << (shift);
                        if ((receiveData.receiveCoilValues[i / 16] & (ushort)mask) == 0)
                        
                            coils[receiveData.startingAdress + i + 1] = false;
                        else
                        
                            coils[receiveData.startingAdress + i + 1] = true;

                    }
            }
            if (sendData.exceptionCode > 0)
                sendData.length = 0x03;
            else
                sendData.length = 0x06;
            if (true)
            {
                Byte[] data;
                if (sendData.exceptionCode > 0)
                    data = new byte[9 + 2 * Convert.ToInt32(serialFlag)];
                else
                    data = new byte[12 + 2 * Convert.ToInt32(serialFlag)];

                Byte[] byteData = new byte[2];
                sendData.length = (byte)(data.Length - 6);

                //Send Transaction identifier
                byteData = BitConverter.GetBytes((int)sendData.transactionIdentifier);
                data[0] = byteData[1];
                data[1] = byteData[0];

                //Send Protocol identifier
                byteData = BitConverter.GetBytes((int)sendData.protocolIdentifier);
                data[2] = byteData[1];
                data[3] = byteData[0];

                //Send length
                byteData = BitConverter.GetBytes((int)sendData.length);
                data[4] = byteData[1];
                data[5] = byteData[0];

                //Unit Identifier
                data[6] = sendData.unitIdentifier;

                //Function Code
                data[7] = sendData.functionCode;



                if (sendData.exceptionCode > 0)
                {
                    data[7] = sendData.errorCode;
                    data[8] = sendData.exceptionCode;
                    sendData.sendRegisterValues = null;
                }
                else
                {
                    byteData = BitConverter.GetBytes((int)receiveData.startingAdress);
                    data[8] = byteData[1];
                    data[9] = byteData[0];
                    byteData = BitConverter.GetBytes((int)receiveData.quantity);
                    data[10] = byteData[1];
                    data[11] = byteData[0];
                }


                try
                {
                    if (serialFlag)
                    {
                        if (!serialport.IsOpen)
                            throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened");
                        //Create CRC
                        sendData.crc = ModbusClient.calculateCRC(data, Convert.ToUInt16(data.Length - 8), 6);
                        byteData = BitConverter.GetBytes((int)sendData.crc);
                        data[data.Length - 2] = byteData[0];
                        data[data.Length - 1] = byteData[1];
                        serialport.Write(data, 6, data.Length - 6);
                        if (debug)
                        {
                            byte[] debugData = new byte[data.Length - 6];
                            Array.Copy(data, 6, debugData, 0, data.Length - 6);
                            if (debug) StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                        }

                    }
                    else if (udpFlag)
                    {
                        //UdpClient udpClient = new UdpClient();
                        IPEndPoint endPoint = new IPEndPoint(ipAddressIn, portIn);
                        udpClient.Send(data, data.Length, endPoint);

                    }
                    else
                    {
                        stream.Write(data, 0, data.Length);
                        if (debug) StoreLogData.Instance.Store("Send Data: " + BitConverter.ToString(data), System.DateTime.Now);
                    }
                }
                catch (Exception) { }
                if (CoilsChanged != null)
                    CoilsChanged(receiveData.unitIdentifier,receiveData.startingAdress+1, receiveData.quantity);
            }
        }

        private void WriteMultipleRegisters(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
        {
            sendData.response = true;

            sendData.transactionIdentifier = receiveData.transactionIdentifier;
            sendData.protocolIdentifier = receiveData.protocolIdentifier;

            sendData.unitIdentifier = receiveData.unitIdentifier;
            sendData.functionCode = receiveData.functionCode;
            sendData.startingAdress = receiveData.startingAdress;
            sendData.quantity = receiveData.quantity;

            if ((receiveData.quantity == 0x0000) | (receiveData.quantity > 0x07B0))  //Invalid Quantity
            {
                sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                sendData.exceptionCode = 3;
            }
            if ((((int)receiveData.startingAdress + 1 + (int)receiveData.quantity) > 65535)  | (receiveData.startingAdress < 0))   //Invalid Starting adress or Starting address + quantity
            {
                sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                sendData.exceptionCode = 2;
            }
            if (sendData.exceptionCode == 0)
            {
                lock (lockHoldingRegisters)
                    for (int i = 0; i < receiveData.quantity; i++)
                    {
                        holdingRegisters[receiveData.startingAdress + i + 1] = unchecked((short)receiveData.receiveRegisterValues[i]);
                    }
            }
            if (sendData.exceptionCode > 0)
                sendData.length = 0x03;
            else
                sendData.length = 0x06;
            if (true)
            {
                Byte[] data;
                if (sendData.exceptionCode > 0)
                    data = new byte[9 + 2 * Convert.ToInt32(serialFlag)];
                else
                    data = new byte[12 + 2 * Convert.ToInt32(serialFlag)];

                Byte[] byteData = new byte[2];
                sendData.length = (byte)(data.Length - 6);

                //Send Transaction identifier
                byteData = BitConverter.GetBytes((int)sendData.transactionIdentifier);
                data[0] = byteData[1];
                data[1] = byteData[0];

                //Send Protocol identifier
                byteData = BitConverter.GetBytes((int)sendData.protocolIdentifier);
                data[2] = byteData[1];
                data[3] = byteData[0];

                //Send length
                byteData = BitConverter.GetBytes((int)sendData.length);
                data[4] = byteData[1];
                data[5] = byteData[0];

                //Unit Identifier
                data[6] = sendData.unitIdentifier;

                //Function Code
                data[7] = sendData.functionCode;



                if (sendData.exceptionCode > 0)
                {
                    data[7] = sendData.errorCode;
                    data[8] = sendData.exceptionCode;
                    sendData.sendRegisterValues = null;
                }
                else
                {
                    byteData = BitConverter.GetBytes((int)receiveData.startingAdress);
                    data[8] = byteData[1];
                    data[9] = byteData[0];
                    byteData = BitConverter.GetBytes((int)receiveData.quantity);
                    data[10] = byteData[1];
                    data[11] = byteData[0];
                }


                try
                {
                    if (serialFlag)
                    {
                        if (!serialport.IsOpen)
                            throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened");
                        //Create CRC
                        sendData.crc = ModbusClient.calculateCRC(data, Convert.ToUInt16(data.Length - 8), 6);
                        byteData = BitConverter.GetBytes((int)sendData.crc);
                        data[data.Length - 2] = byteData[0];
                        data[data.Length - 1] = byteData[1];
                        serialport.Write(data, 6, data.Length - 6);
                        if (debug)
                        {
                            byte[] debugData = new byte[data.Length - 6];
                            Array.Copy(data, 6, debugData, 0, data.Length - 6);
                            if (debug) StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                        }

                    }
                    else if (udpFlag)
                    {
                        //UdpClient udpClient = new UdpClient();
                        IPEndPoint endPoint = new IPEndPoint(ipAddressIn, portIn);
                        udpClient.Send(data, data.Length, endPoint);

                    }
                    else
                    {
                        stream.Write(data, 0, data.Length);
                        if (debug) StoreLogData.Instance.Store("Send Data: " + BitConverter.ToString(data), System.DateTime.Now);
                    }
                    }
                catch (Exception) { }
                if (HoldingRegistersChanged != null)
                    HoldingRegistersChanged(receiveData.unitIdentifier,receiveData.startingAdress+1, receiveData.quantity);
            }
        }

        private void ReadWriteMultipleRegisters(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
        {
            sendData.response = true;

            sendData.transactionIdentifier = receiveData.transactionIdentifier;
            sendData.protocolIdentifier = receiveData.protocolIdentifier;

            sendData.unitIdentifier = receiveData.unitIdentifier;
            sendData.functionCode = receiveData.functionCode;


            if ((receiveData.quantityRead < 0x0001) | (receiveData.quantityRead > 0x007D) | (receiveData.quantityWrite < 0x0001) | (receiveData.quantityWrite > 0x0079) | (receiveData.byteCount != (receiveData.quantityWrite * 2)))  //Invalid Quantity
            {
                sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                sendData.exceptionCode = 3;
            }
            if ((((int)receiveData.startingAddressRead + 1 + (int)receiveData.quantityRead) > 65535) | (((int)receiveData.startingAddressWrite + 1 + (int)receiveData.quantityWrite) > 65535) | (receiveData.quantityWrite < 0) | (receiveData.quantityRead < 0))    //Invalid Starting adress or Starting address + quantity
            {
                sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                sendData.exceptionCode = 2;
            }
            if (sendData.exceptionCode == 0)
            {
                sendData.sendRegisterValues = new Int16[receiveData.quantityRead];
                lock (lockHoldingRegisters)
                    Buffer.BlockCopy(holdingRegisters.localArray, receiveData.startingAddressRead * 2 + 2, sendData.sendRegisterValues, 0, receiveData.quantityRead * 2);

                lock (holdingRegisters)
                    for (int i = 0; i < receiveData.quantityWrite; i++)
                    {
                        holdingRegisters[receiveData.startingAddressWrite + i + 1] = unchecked((short)receiveData.receiveRegisterValues[i]);
                    }
                sendData.byteCount = (byte)(2 * receiveData.quantityRead);
            }
            if (sendData.exceptionCode > 0)
                sendData.length = 0x03;
            else
                sendData.length = Convert.ToUInt16(3 + 2 * receiveData.quantityRead);
            if (true)
            {
                Byte[] data;
                if (sendData.exceptionCode > 0)
                    data = new byte[9 + 2 * Convert.ToInt32(serialFlag)];
                else
                    data = new byte[9 + sendData.byteCount + 2 * Convert.ToInt32(serialFlag)];

                Byte[] byteData = new byte[2];

                //Send Transaction identifier
                byteData = BitConverter.GetBytes((int)sendData.transactionIdentifier);
                data[0] = byteData[1];
                data[1] = byteData[0];

                //Send Protocol identifier
                byteData = BitConverter.GetBytes((int)sendData.protocolIdentifier);
                data[2] = byteData[1];
                data[3] = byteData[0];

                //Send length
                byteData = BitConverter.GetBytes((int)sendData.length);
                data[4] = byteData[1];
                data[5] = byteData[0];

                //Unit Identifier
                data[6] = sendData.unitIdentifier;

                //Function Code
                data[7] = sendData.functionCode;

                //ByteCount
                data[8] = sendData.byteCount;


                if (sendData.exceptionCode > 0)
                {
                    data[7] = sendData.errorCode;
                    data[8] = sendData.exceptionCode;
                    sendData.sendRegisterValues = null;
                }
                else
                {
                    if (sendData.sendRegisterValues != null)
                        for (int i = 0; i < (sendData.byteCount / 2); i++)
                        {
                            byteData = BitConverter.GetBytes((Int16)sendData.sendRegisterValues[i]);
                            data[9 + i * 2] = byteData[1];
                            data[10 + i * 2] = byteData[0];
                        }

                }


                try
                {
                    if (serialFlag)
                    {
                        if (!serialport.IsOpen)
                            throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened");
                        //Create CRC
                        sendData.crc = ModbusClient.calculateCRC(data, Convert.ToUInt16(data.Length - 8), 6);
                        byteData = BitConverter.GetBytes((int)sendData.crc);
                        data[data.Length - 2] = byteData[0];
                        data[data.Length - 1] = byteData[1];
                        serialport.Write(data, 6, data.Length - 6);
                        if (debug)
                        {
                            byte[] debugData = new byte[data.Length - 6];
                            Array.Copy(data, 6, debugData, 0, data.Length - 6);
                            if (debug) StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                        }

                    }
                    else if (udpFlag)
                    {
                        //UdpClient udpClient = new UdpClient();
                        IPEndPoint endPoint = new IPEndPoint(ipAddressIn, portIn);
                        udpClient.Send(data, data.Length, endPoint);

                    }
                    else
                    {
                        stream.Write(data, 0, data.Length);
                        if (debug) StoreLogData.Instance.Store("Send Data: " + BitConverter.ToString(data), System.DateTime.Now);
                    }
                }
                catch (Exception) { }
                if (HoldingRegistersChanged != null)
                    HoldingRegistersChanged(receiveData.unitIdentifier,receiveData.startingAddressWrite+1, receiveData.quantityWrite);
            }
        }

        public void sendException(int errorCode, int exceptionCode, ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
        {
            sendData.response = true;

            sendData.transactionIdentifier = receiveData.transactionIdentifier;
            sendData.protocolIdentifier = receiveData.protocolIdentifier;

            sendData.unitIdentifier = receiveData.unitIdentifier;
            sendData.errorCode = (byte)errorCode;
            sendData.exceptionCode = (byte)exceptionCode;

             if (sendData.exceptionCode > 0)
                sendData.length = 0x03;
            else
                sendData.length = (ushort)(0x03 + sendData.byteCount);

             if (true)
             {
                 Byte[] data;
                 if (sendData.exceptionCode > 0)
                     data = new byte[9 + 2 * Convert.ToInt32(serialFlag)];
                 else
                     data = new byte[9 + sendData.byteCount + 2 * Convert.ToInt32(serialFlag)];
                 Byte[] byteData = new byte[2];
                 sendData.length = (byte)(data.Length - 6);

                 //Send Transaction identifier
                 byteData = BitConverter.GetBytes((int)sendData.transactionIdentifier);
                 data[0] = byteData[1];
                 data[1] = byteData[0];

                 //Send Protocol identifier
                 byteData = BitConverter.GetBytes((int)sendData.protocolIdentifier);
                 data[2] = byteData[1];
                 data[3] = byteData[0];

                 //Send length
                 byteData = BitConverter.GetBytes((int)sendData.length);
                 data[4] = byteData[1];
                 data[5] = byteData[0];

                 //Unit Identifier
                 data[6] = sendData.unitIdentifier;


                 data[7] = sendData.errorCode;
                 data[8] = sendData.exceptionCode;


                 try
                 {
                    if (serialFlag)
                    {
                        if (!serialport.IsOpen)
                            throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened");
                        //Create CRC
                        sendData.crc = ModbusClient.calculateCRC(data, Convert.ToUInt16(data.Length - 8), 6);
                        byteData = BitConverter.GetBytes((int)sendData.crc);
                        data[data.Length - 2] = byteData[0];
                        data[data.Length - 1] = byteData[1];
                        serialport.Write(data, 6, data.Length - 6);
                        if (debug)
                        {
                            byte[] debugData = new byte[data.Length - 6];
                            Array.Copy(data, 6, debugData, 0, data.Length - 6);
                            if (debug) StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                        }
                    }
                    else if (udpFlag)
                    {
                        //UdpClient udpClient = new UdpClient();
                        IPEndPoint endPoint = new IPEndPoint(ipAddressIn, portIn);
                        udpClient.Send(data, data.Length, endPoint);

                    }
                    else
                    {
                        stream.Write(data, 0, data.Length);
                        if (debug) StoreLogData.Instance.Store("Send Data: " + BitConverter.ToString(data), System.DateTime.Now);
                    }
                 }
                 catch (Exception) { }
             }
        }

        private void CreateLogData(ModbusProtocol receiveData, ModbusProtocol sendData)
        {
            for (int i = 0; i < 98; i++)
            {
                modbusLogData[99 - i] = modbusLogData[99 - i - 2];

            }
            modbusLogData[0] = receiveData;
            modbusLogData[1] = sendData;

        }



        public int NumberOfConnections
        {
            get
            {
                return numberOfConnections;
            }
        }

        public ModbusProtocol[] ModbusLogData
        {
            get
            {
                return modbusLogData;
            }
        }

        public int Port
        {
            get
            {
                return port;
            }
            set
            {
                port = value;
                

            }
        }

        public bool UDPFlag
        {
            get
            {
                return udpFlag;
            }
            set
            {
                udpFlag = value;
            }
        }
        
 		public bool SerialFlag
 		{
 			get
 			{
 				return serialFlag;
 			}
 			set
 			{
 				serialFlag = value;
 			}
 		}

        public int Baudrate
        {
            get
            {
                return baudrate;
            }
            set
            {
                baudrate = value;
            }
        }

        public System.IO.Ports.Parity Parity
        {
            get
            {
                return parity;
            }
            set
            {
                parity = value;
            }
        }

        public System.IO.Ports.StopBits StopBits
        {
            get
            {
                return stopBits;
            }
            set
            {
                stopBits = value;
            }
        }

        public string SerialPort
        {
            get
            {
                return serialPort;
            }
            set
            {
                serialPort = value;
                if (serialPort != null)
                    serialFlag = true;
                else
                    serialFlag = false;
            }
        }

        public byte UnitIdentifier
        {
            get
            {
                return unitIdentifier;
            }
            set
            {
                unitIdentifier = value;
            }
        }




        /// <summary>
        /// Gets or Sets the Filename for the LogFile
        /// </summary>
        public string LogFileFilename
        {
            get
            {
                return StoreLogData.Instance.Filename;
            }
            set
            {
                StoreLogData.Instance.Filename = value;
                if (StoreLogData.Instance.Filename != null)
                    debug = true;
                else
                    debug = false;
            }
        }




    public class HoldingRegisters
    {
        public Int16[] localArray = new Int16[65535];
        ModbusServer modbusServer;
     
        public HoldingRegisters(EasyModbus.ModbusServer modbusServer)
        {
            this.modbusServer = modbusServer;
        }

        public Int16 this[int x]
        {
            get { return this.localArray[x]; }
            set
            {              
                this.localArray[x] = value;
                
            }
        }
    }

    public class InputRegisters
    {
        public Int16[] localArray = new Int16[65535];
        ModbusServer modbusServer;

        public InputRegisters(EasyModbus.ModbusServer modbusServer)
        {
            this.modbusServer = modbusServer;
        }

        public Int16 this[int x]
        {
            get { return this.localArray[x]; }
            set
            {
                this.localArray[x] = value;

            }
        }
    }

    public class Coils
    {
        public bool[] localArray = new bool[65535];
        ModbusServer modbusServer;

        public Coils(EasyModbus.ModbusServer modbusServer)
        {
            this.modbusServer = modbusServer;
        }

        public bool this[int x]
        {
            get { return this.localArray[x]; }
            set
            {
                this.localArray[x] = value;
            
            }
        }
    }

    public class DiscreteInputs
    {
        public bool[] localArray = new bool[65535];
        ModbusServer modbusServer;

        public DiscreteInputs(EasyModbus.ModbusServer modbusServer)
        {
            this.modbusServer = modbusServer;
        }

        public bool this[int x]
        {
            get { return this.localArray[x]; }
            set
            {
                this.localArray[x] = value;
              
            }
        }

      
        }
    }
}
   