using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;

namespace AutoScan_Weight_WinUI.Services
{
    public interface ISerialPortService
    {
        bool IsConnected { get; }

        bool Connect(string portName = "COM3", int baudRate = 9600);

        void Disconnect();

        void SendData(string data);

        event Action<string> DataReceived;
    }
}
