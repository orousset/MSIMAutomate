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
        public List<TrkObject> trkObjectList;
        public XMLReader(string xmlFile) {
            trkObjectList = new List<TrkObject>();
            
            XmlDataDocument xmldoc = new XmlDataDocument();
            XmlNodeList xmlnode;
            FileStream fs = new FileStream(xmlFile, FileMode.Open, FileAccess.Read);
            xmldoc.Load(fs);
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

        public static void computeStatus(object source, ElapsedEventArgs e) {
            List<string> listOutputSet = new List<string>();

            currentTime += cycleTime;
            foreach (TrkObject TrkObj in automataInput.trkObjectList) {
                int localTime = currentTime / 1000 % TrkObj.period;
                foreach (TrkInterface TrkInt in TrkObj.listTrkInterface) {
                    if ( (localTime >= TrkInt.timeOperate) && (localTime < TrkInt.timeOperate + commandDuration) ) 
                        { TrkInt.command = true; /* Console.WriteLine("{0} -- {1}: {2}" , currentTime, TrkInt.outputId, TrkInt.command) */; listOutputSet.Add(TrkInt.outputId); }
                    else if ( ((localTime >= TrkInt.timeOperate + commandDuration) || (localTime < TrkInt.timeOperate) ) && (TrkInt.command == true) )
                        { TrkInt.command = false; /* Console.WriteLine("{0} -- {1}: {2}", currentTime, TrkInt.outputId, TrkInt.command); */ }
                }
            }
            if (listOutputSet.Count > 0) { UDPinterface.SendMessage(48, myFSFB2_DataFlow.GetIndex(listOutputSet.ToArray(), "TX")); }
            else { UDPinterface.SendMessage(48, new int[0]); }
        }

        public static void InitAutomata(XMLReader input, NetworkUDP UDPinterface_i, FSFB2DataFlow FSFB2_DataFlow_i) {
            automataInput = input;
            UDPinterface = UDPinterface_i;
            myFSFB2_DataFlow = FSFB2_DataFlow_i;
            Timer myTimer = new Timer();
            myTimer.Elapsed += new ElapsedEventHandler(computeStatus);
            myTimer.Interval = cycleTime;
            myTimer.Start();
            while (Console.ReadKey(true).Key != ConsoleKey.Q) { /* DO NOTHING */ }
            UDPinterface.Quit();
        }
    }

    class Program
    {
        static void Main(string[] args) {
            string[] variableInput, variableOutput;
            List<string> listVarI, listVarO;
            int[] indexO, indexI;
            NetworkUDP myUDPinterface;
            string IPDst = "10.1.2.4";

            FSFB2Node myFSFB2Node = new FSFB2Node();
            FSFB2DataFlow myFSFB2_DataFlow = new FSFB2DataFlow();
            myFSFB2Node.NameHost = "ZC2_A";
            if (myFSFB2Node.InitListNotes() != ERRORS.NO_ERROR) { string[] listOfNodes = myFSFB2Node.getListNodes(); }
            if (myFSFB2_DataFlow.InitFSFB2DataFlow("TSW1", myFSFB2Node) == ERRORS.NO_ERROR) {
                Console.WriteLine("FSFB2 data structure initialised");
            }
            else {
                Console.WriteLine("Error encountered while attempting to initialised {0} FSFB2 data structure", myFSFB2Node.NameHost);
                return;
            }

            XMLReader myReader = new XMLReader("essai.xml");

            // Initialisation of the list of input/output variables
            listVarI = new List<string>();
            listVarO = new List<string>();
            foreach (TrkObject TrkObj in myReader.trkObjectList) {
                foreach (TrkInterface TrkInt in TrkObj.listTrkInterface) {
                    listVarO.Add(TrkInt.outputId);
                    listVarI.Add(TrkInt.inputId);
                }
            }
            variableOutput = listVarO.ToArray();
            variableInput = listVarI.ToArray();
            indexO = myFSFB2_DataFlow.GetIndex(variableOutput, "TX");
            indexI = myFSFB2_DataFlow.GetIndex(variableInput, "RX");

            myUDPinterface = new NetworkUDP();
            myUDPinterface.InitUDP(IPDst);

            Automata.InitAutomata(myReader, myUDPinterface, myFSFB2_DataFlow);

        }
    }
}
