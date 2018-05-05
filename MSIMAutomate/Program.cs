using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.IO;
using System.Timers;
using FSFB2_structure;

namespace MSIMAutomate
{
    /*
    class Display
    {
        private object lockInput;
        private int windowHeight, windowWidth;
        public enum InputOutput { input = 1, output = 2 }

        public void InitDisplay() {
            windowHeight = Console.WindowHeight;
            windowWidth = Console.WindowWidth;
            Console.CursorVisible = false;
            Console.Clear();
            Console.SetCursorPosition(0, 0);
            Console.Write("#### Inputs " + new string('#', (Console.WindowWidth / 2 - 12)) + "#### Outputs " + new string('#', (Console.WindowWidth / 2 - 13)));
        }

        public void UpdateDisplay(List<string> listOutputSet, int counter) {
            if ((windowWidth != Console.WindowWidth) || (windowHeight != Console.WindowHeight)) { InitDisplay(); }

            Console.SetCursorPosition(Console.WindowWidth / 2, 1);
            Console.Write(((float)counter / 1000).ToString("0.0"));
            for (int i = 0; i < inputArray.Length; i++) {
                Console.SetCursorPosition(0, i + 2);
                Console.Write("{0}: {1} ", inputArray[i], UDPinterface.inputSet[i]);
                if (UDPinterface.inputChanged[i] && (UDPinterface.inputSet[i] == 1)) {
                    logFile.WriteLine(DateTime.Now + "::" + inputArray[i] + ": Set");
                    UDPinterface.inputChanged[i] = false;
                }
                if (UDPinterface.inputChanged[i] && (UDPinterface.inputSet[i] == 0)) {
                    logFile.WriteLine(DateTime.Now + "::" + inputArray[i] + ": UnSet");
                    UDPinterface.inputChanged[i] = false;
                }
            }
            Console.SetCursorPosition(0, 1);
            Console.Write(UDPinterface.inputPktCnt.ToString("000"));
            int j = 2;
            foreach (TrkObject TrkObj in automataInput.trkObjectList) {
                foreach (TrkInterface TrkInt in TrkObj.listTrkInterface) {
                    Console.SetCursorPosition(Console.WindowWidth / 2, j++);
                    bool isSet = false;
                    if (listOutputSet.Contains(TrkInt.outputId)) { isSet = true; }
                    Console.Write("{0} - {1}; ", TrkInt.outputId, isSet);
                }
            }
            if (inputDisplay) {
                Console.SetCursorPosition(0, Console.WindowHeight - 1);
                Console.Write("<< {0}", UDPinterface.Convert2String(UDPinterface.receivedBytes));
            }
            if (outputDisplay) {
                Console.SetCursorPosition(0, Console.WindowHeight - 2);
                Console.Write(">> {0}", UDPinterface.Convert2String(UDPinterface.transmittedBytes));
            }
        }

    }
    */

    class TrkInterface
    {
        public string outputId, inputId;
        public int timeOperate;
        public bool state, command;

        public TrkInterface(string name) { outputId = name; }
    }

    class TrkObject
    {
        public string name;
        public int period;
        public List<TrkInterface> listTrkInterface;
        public TrkObject() { listTrkInterface = new List<TrkInterface>(); }
    }

    class XMLReader
    {
        public ushort SrcAddr, DstAddr;
        public string ZC_NAME, SIO_NAME;
        public List<TrkObject> trkObjectList;
        public XMLReader(string xmlFile) {
            trkObjectList = new List<TrkObject>();
            
            XmlDataDocument xmldoc = new XmlDataDocument();
            XmlNodeList xmlnode;
            FileStream fs = new FileStream(xmlFile, FileMode.Open, FileAccess.Read);
            xmldoc.Load(fs);
            xmlnode = xmldoc.GetElementsByTagName("ZC_NAME");
            ZC_NAME = xmlnode[0].InnerText.Trim();
            xmlnode = xmldoc.GetElementsByTagName("SIO_NAME");
            SIO_NAME = xmlnode[0].InnerText.Trim();
            xmlnode = xmldoc.GetElementsByTagName("SRCADDR");
            SrcAddr = Convert.ToUInt16(xmlnode[0].InnerText); // There must be only 1! SrdAddr - no verification of consistency performed
            xmlnode = xmldoc.GetElementsByTagName("DSTADDR");
            DstAddr = Convert.ToUInt16(xmlnode[0].InnerText); // There must be only 1! DstAddr - no verification of consistency performed
            xmlnode = xmldoc.GetElementsByTagName("POINT");
            for (int i  = 0; i < xmlnode.Count; i++) {
                trkObjectList.Add(new TrkObject());
                trkObjectList[trkObjectList.Count - 1].name = xmlnode[i].Attributes["name"].Value;
                int nbObjects = xmlnode[i].ChildNodes.Count;
                for (int j = 0; j < nbObjects; j++) {
                    string localName = xmlnode[i].ChildNodes[j].LocalName;
                    if (localName == "PERIOD") { trkObjectList[trkObjectList.Count - 1].period = Convert.ToInt32(xmlnode[i].ChildNodes[j].InnerText); }
                    else if (localName == "OUTPUT") { trkObjectList[trkObjectList.Count - 1].listTrkInterface.Add(new TrkInterface(xmlnode[i].ChildNodes[j].InnerText.Trim())); }
                    else {
                        TrkInterface _trkInterface = trkObjectList[trkObjectList.Count - 1].listTrkInterface[trkObjectList[trkObjectList.Count - 1].listTrkInterface.Count-1];
                        if (localName == "TIME") { _trkInterface.timeOperate = Convert.ToInt32(xmlnode[i].ChildNodes[j].InnerText); }
                        if (localName == "INPUT") { _trkInterface.inputId = xmlnode[i].ChildNodes[j].InnerText.Trim(); }
                    }
                }
            }
        }
    }

    static class Automata
    {
        private static XMLReader automataInput;
        private static int cycleTime = 200; // cycle time of the boolean machine in milliseconds
        private static int currentTime = 0; // current time
        private static int commandDuration = 2; // duration of the command
        private static NetworkUDP UDPinterface;
        private static FSFB2DataFlow myFSFB2_DataFlow;
        private static int sizeTX;
        private static string[] inputArray;
        private static bool outputDisplay, inputDisplay;
        private static int windowHeight, windowWidth;
        private static TextWriter logFile;
        private static Object lockInputState;
        private static string version;

        private static void initDisplay(string version_i) {
            version = version_i;
            windowHeight = Console.WindowHeight;
            windowWidth = Console.WindowWidth;
            Console.CursorVisible = false;
            Console.Clear();
            Console.SetCursorPosition(0, 0);
            Console.Write("#### Inputs " + new string('#', (Console.WindowWidth / 2 - 12) ) + "#### Outputs " + new string('#', (Console.WindowWidth / 2 - 13) ) );
            Console.SetCursorPosition(Console.WindowWidth - 6, 0);
            Console.Write(" v." + version);
        }

        private static void Display(List<string> listOutputSet, int counter) {
            if ( (windowWidth != Console.WindowWidth) || (windowHeight != Console.WindowHeight) ) { initDisplay(version); }

            Console.SetCursorPosition(Console.WindowWidth / 2, 1);
            Console.Write(((float)counter / 1000).ToString("0.0"));
            for (int i = 0; i < inputArray.Length; i++) {
                Console.SetCursorPosition(0, i + 2);
                Console.Write("{0}: {1} ", inputArray[i], UDPinterface.inputSet[i]);
                if (UDPinterface.inputChanged[i]&&(UDPinterface.inputSet[i] == 1) ) {
                    logFile.WriteLine(DateTime.Now + "::" + inputArray[i] + ": Set");
                    UDPinterface.inputChanged[i] = false;
                }
                if (UDPinterface.inputChanged[i] && (UDPinterface.inputSet[i] == 0)) {
                    logFile.WriteLine(DateTime.Now + "::" + inputArray[i] + ": UnSet");
                    UDPinterface.inputChanged[i] = false;
                }
            }
            Console.SetCursorPosition(0, 1);
            Console.Write(UDPinterface.inputPktCnt.ToString("000"));
            int j = 2;
            foreach (TrkObject TrkObj in automataInput.trkObjectList) {
                foreach (TrkInterface TrkInt in TrkObj.listTrkInterface) {
                    Console.SetCursorPosition(Console.WindowWidth / 2, j++);
                    bool isSet = false;
                    if (listOutputSet.Contains(TrkInt.outputId)) { isSet = true; }
                    Console.Write("{0} - {1}; ", TrkInt.outputId, isSet);
                }
            }
            if (inputDisplay) {
                Console.SetCursorPosition(0, Console.WindowHeight - 1);
                Console.Write("<< {0}", UDPinterface.Convert2String(UDPinterface.receivedBytes));
            }
            if (outputDisplay) {
                Console.SetCursorPosition(0, Console.WindowHeight - 2);
                Console.Write(">> {0}", UDPinterface.Convert2String(UDPinterface.transmittedBytes));
            }
        }

        public static void ComputeStatus(object source, ElapsedEventArgs e) {
            List<string> listOutputSet = new List<string>();
            sizeTX = myFSFB2_DataFlow.GetLength("TX");

            currentTime += cycleTime;
            lock (lockInputState) {
                foreach (TrkObject TrkObj in automataInput.trkObjectList) {
                    int localTime = currentTime / 1000 % TrkObj.period;
                    foreach (TrkInterface TrkInt in TrkObj.listTrkInterface) {
                        if ((localTime >= TrkInt.timeOperate) && (localTime < TrkInt.timeOperate + commandDuration)) {
                            if (TrkInt.command == false) { logFile.WriteLine(DateTime.Now + "::" + TrkInt.outputId + ": Set"); }
                            TrkInt.command = true; listOutputSet.Add(TrkInt.outputId);
                        }
                        else if (((localTime >= TrkInt.timeOperate + commandDuration) || (localTime < TrkInt.timeOperate)) && (TrkInt.command == true)) { TrkInt.command = false; logFile.WriteLine(DateTime.Now + "::" + TrkInt.outputId + ": UnSet"); }
                    }
                }
                if (listOutputSet.Count > 0) { UDPinterface.SendMessage(sizeTX, myFSFB2_DataFlow.GetIndex(listOutputSet.ToArray(), "TX")); }
                else { UDPinterface.SendMessage(sizeTX, new int[0]); }
                Display(listOutputSet, currentTime);
            }
        }

        public static void InitAutomata(XMLReader input, NetworkUDP UDPinterface_i, FSFB2DataFlow FSFB2_DataFlow_i, string[] inputArray_i, 
            TextWriter logFile_i, ref object lockInputState_i, string version) {
            lockInputState = lockInputState_i;
            logFile = logFile_i;
            inputArray = inputArray_i;
            automataInput = input;
            UDPinterface = UDPinterface_i;
            myFSFB2_DataFlow = FSFB2_DataFlow_i;
            initDisplay(version);
            Timer myTimer = new Timer();
            myTimer.Elapsed += new ElapsedEventHandler(ComputeStatus);
            myTimer.Interval = cycleTime;
            myTimer.Start();

            // Management of key pressed - Q for quitting, O for displaying output to SIO, I for displaying input from SIO, R redraw the whole Console (if resized)
            bool Qpressed = false;
            while (!Qpressed){
                var keyPressed = Console.ReadKey(true).Key;
                if (keyPressed == ConsoleKey.Q) { Qpressed = true; }
                else if (keyPressed == ConsoleKey.R) { initDisplay(version); }
                else if ((keyPressed == ConsoleKey.O) && (outputDisplay == false)) { outputDisplay = true; }
                else if ((keyPressed == ConsoleKey.O) && (outputDisplay == true)) { outputDisplay = false; }
                else if ((keyPressed == ConsoleKey.I) && (inputDisplay == false)) { inputDisplay = true; }
                else if ((keyPressed == ConsoleKey.I) && (inputDisplay == true)) { inputDisplay = false; }
            }
            logFile.Close();
            UDPinterface.Quit();
        }
    }

    class Program
    {
        static string version = "1.0";

        static void Main(string[] args) {
            NetworkUDP myUDPinterface;
            TextWriter logFile;
            Object lockFileLog = new object();
            Object lockInputState = new object();

            if (args.Length != 3) {
                Console.WriteLine("Please provide (1) the address of the multisim engine, (2) the name of the XML configuration file, (3) the name of the log file");
                Console.WriteLine("E.g: MSIMAutomate 127.0.0.1 TSW_config.xml log.txt");
                return;
            }
            logFile = new StreamWriter(args[2], true);
            XMLReader myReader = new XMLReader(args[1]);
            string IPDst = args[0];
            FSFB2Node myFSFB2Node = new FSFB2Node();
            FSFB2DataFlow myFSFB2_DataFlow = new FSFB2DataFlow();
            myFSFB2Node.NameHost = myReader.ZC_NAME;
            if (myFSFB2Node.InitListNotes() != ERRORS.NO_ERROR) { string[] listOfNodes = myFSFB2Node.getListNodes(); }
            if (myFSFB2_DataFlow.InitFSFB2DataFlow(myReader.SIO_NAME, myFSFB2Node) == ERRORS.NO_ERROR) {
                Console.WriteLine("FSFB2 data structure initialised");
            }
            else {
                Console.WriteLine("Error encountered while attempting to initialised {0} FSFB2 data structure", myFSFB2Node.NameHost);
                return;
            }

            myUDPinterface = new NetworkUDP();
            List<string> inputList = new List<string>();
            foreach (TrkObject trkObj in myReader.trkObjectList) {
                foreach (TrkInterface trkInt in trkObj.listTrkInterface) {
                    inputList.Add(trkInt.inputId);
                }
            }
            string[] inputArray = inputList.ToArray();

            myUDPinterface.InitUDP(IPDst, myFSFB2_DataFlow.GetIndex(inputArray, "RX"), myReader.SrcAddr, myReader.DstAddr, ref lockInputState);
            Automata.InitAutomata(myReader, myUDPinterface, myFSFB2_DataFlow, inputArray, logFile, ref lockInputState, version);

        }
    }
}
