using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;

namespace AutoScan_Weight_WinUI.Services
{
    public class SerialPortService : ISerialPortService
    {
        //private SerialPort? _serialPort;

        //public bool IsConnected => _serialPort?.IsOpen ?? true;

        //public event Action<string>? DataReceived;

        //public bool Connect(string portName, int baudRate)
        //{
        //    try
        //    {
        //        _serialPort = new SerialPort(portName, baudRate)
        //        { 
        //            Parity = Parity.Even,
        //            DataBits = 7,
        //            StopBits = StopBits.One,

        //        };
        //        _serialPort.DataReceived += SerialPort_DataReceived;
        //        _serialPort.Open();
        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        // Handle connection error
        //        //App.DialogService.ShowMessageAsync("❌ Error connecting to serial port", "Please Check Weight Machine");
        //        Console.WriteLine($"Error connecting to serial port: {ex.Message}");
        //        return false;
        //    }
        //}

        //public void Disconnect()
        //{
        //    if (_serialPort == null) return;
        //    _serialPort.DataReceived -= SerialPort_DataReceived;

        //    if (_serialPort.IsOpen)
        //    {
        //        _serialPort.Close();

        //        _serialPort.Dispose();
        //        _serialPort = null;
        //    }
        //}

        //public void SendData(string data)
        //{
        //    if (IsConnected)
        //    {
        //        _serialPort?.WriteLine(data);
        //    }
        //}

        //private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        //{
        //    if (_serialPort != null)
        //    {
        //        string data = _serialPort.ReadLine();
        //        DataReceived?.Invoke(data);
        //    }
        //}

        private readonly SerialPort _serialPort = new();

        //public bool IsOpen => _serialPort.IsOpen;
        public bool IsConnected => _serialPort?.IsOpen ?? true;

        public event Action<string>? DataReceived;

        public bool Connect(string protName , int baudRate)
        {
            try
            {
                _serialPort.PortName = protName;
                _serialPort.BaudRate = baudRate;
                _serialPort.Parity = Parity.Even;
                _serialPort.StopBits = StopBits.One;
                _serialPort.DataBits = 7;
                _serialPort.Open();

                _serialPort.DataReceived += OnDatareceived;

                return true;

            }
            catch (Exception ex)
            {
                // Handle connection error
                //App.DialogService.ShowMessageAsync("❌ Error connecting to serial port", "Please Check Weight Machine");
                Console.WriteLine($"Error connecting to serial port: {ex.Message}");
                return false;
            }

            
        }

        private void OnDatareceived(object sender, SerialDataReceivedEventArgs e)
        { 
            var data = _serialPort.ReadLine();
            DataReceived?.Invoke(data);

        }

        public void SendData(string data)
        {
            if (_serialPort.IsOpen)
                _serialPort.Write(data);
        }
        public void Disconnect()
        {
            if (_serialPort == null) return;
            _serialPort.DataReceived -= OnDatareceived;

            if (_serialPort.IsOpen)
            {
                _serialPort.Close();

                _serialPort.Dispose();
                
            }
        }
    }
}
