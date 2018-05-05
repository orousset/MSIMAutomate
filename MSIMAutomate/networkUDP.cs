using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;

namespace MSIMAutomate
{
    class NetworkUDP
    {
        int portSrc = 50042;
        int portDst = 50041;
        UInt16 SrcAddr;// = 0x09;
        UInt16 DstAddr;// = 0x2719;
        byte AppDom = 0x32;
        byte Type = 0x03;
        byte ElementSize = 1;
        byte[] mask = { 0x01, 0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x80 };
        UdpClient socket;
        byte PktNum = 0;
        string IPdst;
        System.Threading.Thread listenThread;
        bool quit = false;
        int sizeHeader = 10;
        int[] inputIndex;
        public byte[] transmittedBytes, receivedBytes;
        public int[] inputSet;
        public bool[] inputChanged;
        public byte inputPktCnt;
        private object lockInputState;

        public string Convert2String(byte[] inputArray) {
            StringBuilder sb = new StringBuilder();
            string ReturnString;
            foreach (byte b in inputArray) {
                string st = b.ToString("X");
                if (st.Length == 1) { sb.Append("0" + st + " "); }
                else {sb.Append(st + " "); }
            }
            ReturnString = sb.ToString();
            return ReturnString;
        }

        public void SendMessage(int lengthBit, int[] bitSet) {
            PktNum++;
            int length = lengthBit / 8;
            if (lengthBit % 8 != 0) { length++; }

            transmittedBytes = new byte[length + sizeHeader];
            transmittedBytes[0] = AppDom;
            transmittedBytes[1] = Type;
            transmittedBytes[6] = PktNum;
            transmittedBytes[7] = ElementSize;
            transmittedBytes[8] = (byte)(lengthBit & 0xFF);
            transmittedBytes[9] = (byte)(lengthBit >> 8);
            transmittedBytes[2] = (byte)(DstAddr & 0xFF);
            transmittedBytes[3] = (byte)(DstAddr >> 8);
            transmittedBytes[4] = (byte)(SrcAddr & 0xFF);
            transmittedBytes[5] = (byte)(SrcAddr >> 8);

            // Set all the required bits to 1
            for (int b = 0; b < bitSet.Length; b++) {
                transmittedBytes[bitSet[b] / 8 + sizeHeader] += mask[bitSet[b] % 8];
            }
            socket.Send(transmittedBytes, transmittedBytes.Length);
        }

        public void Quit() {
            quit = true;
            socket.Close();
        }

        public void Listen() {
            while (quit == false) {
                IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
                try {
                    byte[] receiveBytes = socket.Receive(ref RemoteIpEndPoint);
                    if ( ((receiveBytes[3] << 8) + receiveBytes[2] == SrcAddr) && ((receiveBytes[5] << 8) + receiveBytes[4] == DstAddr) && (receiveBytes[1] == Type) ) {
                        lock (lockInputState) {
                            receivedBytes = receiveBytes;
                            inputPktCnt = receivedBytes[6];
                            for (int i = 0; i < inputSet.Length; i++) {
                                int previousValue = inputSet[i];
                                inputSet[i] = receivedBytes[10 + inputIndex[i] / 8] & mask[inputIndex[i] % 8];
                                if (inputSet[i] > 0) { inputSet[i] = 1; }
                                if ((previousValue ^ inputSet[i]) == 1) { inputChanged[i] = true; } else { inputChanged[i] = false; }
                            }
                        }
                    }
                } catch(SocketException ex) { if (ex.SocketErrorCode != SocketError.Interrupted) { throw (ex); } } // The socket will throw 'Interrupted' exception when it is closed by other
            }
        }

        public void InitUDP(string IPdst_i, int[] inputIndex_i, ushort SrcAddr_i, ushort DstAddr_i, ref object lockInputState_i) {
            lockInputState = lockInputState_i;
            SrcAddr = SrcAddr_i;
            DstAddr = DstAddr_i;
            IPdst = IPdst_i;
            inputIndex = inputIndex_i;
            socket = new UdpClient(portSrc);
            socket.Connect(IPdst, portDst);
            inputSet = new int[inputIndex.Length];
            inputChanged = new bool[inputIndex.Length];
            listenThread = new System.Threading.Thread(new System.Threading.ThreadStart(Listen));
            listenThread.Start();
        }
    }
}
