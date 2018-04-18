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
        byte AppDom = 0x32;
        byte Type = 0x03;
        byte ElementSize = 1;
        byte[] mask = { 0x80, 0x40, 0x20, 0x10, 0x08, 0x04, 0x02, 0x01 };
        UdpClient socket;
        byte PktNum = 0;
        string IPdst;
        System.Threading.Thread listenThread;
        bool quit = false;

        static string Convert2String(byte[] inputArray) {
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

            byte[] message = new byte[length + 10];
            message[0] = AppDom;
            message[1] = Type;
            message[6] = PktNum;
            message[7] = ElementSize;
            message[8] = (byte)(lengthBit >> 8);
            message[9] = (byte)(lengthBit & 0xFF);

            // Set all the required bits to 1
            for (int b = 0; b < bitSet.Length; b++) {
                message[bitSet[b] / 8 + 10] += mask[bitSet[b] % 8];
            }
            Console.Write("Sending to {0}:{1} ==> {2}", IPdst, portDst, Convert2String(message));
            socket.Send(message, message.Length);
            Console.WriteLine(". Message Sent @ {0}", DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond);
        }

        public void Quit() {
            quit = true;
            socket.Close();
        }

        public void Listen() {
            while (quit == false) {
                IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, portDst);
                try {
                    Byte[] receiveBytes = socket.Receive(ref RemoteIpEndPoint);
                    Console.WriteLine("Received: {0}", Convert2String(receiveBytes));
                } catch(SocketException ex) { if (ex.SocketErrorCode != SocketError.Interrupted) { throw (ex); } } // The socket will throw 'Interrupted' exception when it is closed by other
            }
        }

        public void InitUDP(string IPdst_i) {
            IPdst = IPdst_i;
            socket = new UdpClient(portSrc);
            socket.Connect(IPdst, portDst);
            listenThread = new System.Threading.Thread(new System.Threading.ThreadStart(Listen));
            listenThread.Start();
        }
    }
}
