using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MCProtocol;

namespace gcp_Wpf.commClass
{
    internal class mcProtocolClass
    {
        private PLCData<int> readAddr = new PLCData<int>(Mitsubishi.PlcDeviceType.D, 500, 6);
        private PLCData<int> writeAddr = new PLCData<int>(Mitsubishi.PlcDeviceType.D, 500, 6);
        private async void ConnectToPlc()
        {
            PLCData.PLC = new Mitsubishi.McProtocolTcp("192.168.1.4", 5012, Mitsubishi.McFrame.MC3E);
            await PLCData.PLC.Open();
        }

        private void WriteData()
        {
            writeAddr.WriteData();
        }
        private void ReadData()
        {
            readAddr.ReadData();
        }
    }
}
