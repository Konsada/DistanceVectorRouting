using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace project2
{
    public class Router
    {
        UdpClient m_sender;
        Socket m_update;
        Socket m_command;
        int m_commandPort;
        int m_updatePort;
        int m_infinity = 64;
        
        /// <summary>
        /// table to send packets to |Destination|Cost|NextHop|
        /// </summary>
        public Dictionary<string, Tuple<int, string>> m_RoutingTable = new Dictionary<string, Tuple<int, string>>();

        /// <summary>
        /// Dictionary that keeps track of its neighbors names and update port # <name,<cost, updatePort>>
        /// </summary>
        public Dictionary<string, Tuple<int, int>> m_Neighbors = new Dictionary<string, Tuple<int, int>>();

        #region
        /// <summary>
        /// Creates a router object.
        /// </summary>
        /// <param name="name"> Router name </param>
        /// <param name="host"> Host IP </param>
        /// <param name="cPort"> Command Port Number </param>
        /// <param name="uPort"> Update Port Number </param>
        /// <param name="poison"> Set for Poisoned Reverse </param>
        public Router(string name, string host, int cPort, int uPort, bool poison)
        {
            Name = name;
            Host = host;
            m_commandPort = cPort;
            m_updatePort = uPort;
            CommandPort = cPort;
            UpdatePort = uPort;
            Poisoned = poison;

            m_update = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            m_command = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            m_update.Bind(new IPEndPoint(Dns.GetHostAddresses(host)[1], uPort));
            m_command.Bind(new IPEndPoint(Dns.GetHostAddresses(host)[1], cPort));
        }
        /// <summary>
        /// Creates a router object.
        /// </summary>
        /// <param name="name"> Router name </param>
        /// <param name="host"> Host IP </param>
        /// <param name="cPort"> Command Port Number </param>
        /// <param name="uPort"> Update Port Number </param>
        /// <param name="poison"> Set for Poisoned Reverse </param>
        public Router(string name, IPEndPoint host, int cPort, int uPort, bool poison)
        {
            Name = name;
            Host = host.Address.ToString();
            m_commandPort = cPort;
            m_updatePort = uPort;
            CommandPort = cPort;
            UpdatePort = uPort;
            Poisoned = poison;
            if (uPort == 0)
                m_update = null;
            else
            {
                m_update = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                m_update.Bind(host);

            }
            if (cPort == 0)
                m_command = null;
            else
            {
                m_command = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                m_command.Bind(host);
            }
        }
        public string Name { get; private set; }
        string Host { get; set; }
        public string Directory { get; set; }
        public int CommandPort
        {
            get
            {
                return m_commandPort;
            }
            set
            {
                m_command.Bind(new IPEndPoint(Dns.GetHostAddresses(Host)[1], value));
            }
        }
        public int UpdatePort
        {
            get
            {
                return m_updatePort;
            }
            set
            {
                m_update.Bind(new IPEndPoint(Dns.GetHostAddresses(Host)[1], value));
            }
        }
        public bool Poisoned { get; set; }
        #endregion
        public void Start()
        {
            IPHostEntry hostEntry = Dns.GetHostEntry(Host);
            IPAddress ipAddress = hostEntry.AddressList[1];
            UdpClient neighborClient = new UdpClient(UpdatePort);
            IPEndPoint sender = new IPEndPoint(IPAddress.Any, UpdatePort);
            EndPoint neighborRouter = (EndPoint)sender;

            // Data buffer for incoming data.
            byte[] bytes = new byte[1024];

            // read file
            ReadConfig();

            // Bind the socket to the local endpoint and 
            // listen for incoming connections.
            try
            {
                while (true)
                {
                    Socket updatePort = null;
                    Socket commandPort = null;
                    ArrayList listenList = new ArrayList();
                    ArrayList acceptList = new ArrayList();

                    // Start listening for connections.
                    updatePort = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    updatePort.Bind(new IPEndPoint(ipAddress, UpdatePort));

                    commandPort = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    commandPort.Bind(new IPEndPoint(ipAddress, CommandPort));

                    listenList.Add(updatePort);
                    listenList.Add(commandPort);
                    Socket.Select(listenList, null, null, -1);

                    int read = 0;
                    for (int i = 0; i < listenList.Count; i++)
                    {
                        read = ((Socket)listenList[i]).ReceiveFrom(bytes, ref neighborRouter);
                        if (read > 0)
                        {
                            string msg = Encoding.ASCII.GetString(bytes, 0, read);
                            if (RouterChange(msg))
                            {
                                ProcessMessage(msg, neighborRouter);
                                UpdateForwardTable();
                            }
                        }
                    }

                    updatePort.Shutdown(SocketShutdown.Both);
                    updatePort.Close();
                    commandPort.Shutdown(SocketShutdown.Both);
                    commandPort.Close();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Console.Read();
            }
        }

        private void ProcessMessage(string msg, EndPoint neighborRouter)
        {
            string[] parts = msg.Trim().Split(' ');
            switch (parts[0])
            {
                case "U":
                    UpdateTable(parts, neighborRouter);
                    break;
                case "L":
                    LinkCost(parts);
                    break;
                case "P":
                    HandlePMessage(parts);
                    break;
                default:
                    Console.WriteLine("Cannot recognize update message command");
                    break;
            }
        }

        private void LinkCost(string[] parts)
        {
            // if link cost changed, then change the dictionary value
            if (m_Neighbors[parts[1]].Item1 != int.Parse(parts[2]))
            {
                m_Neighbors[parts[1]] = new Tuple<int, int>(int.Parse(parts[2]), m_Neighbors[parts[1]].Item2);
            }

        }
        /// <summary>
        /// Check if update recv'd has an improved efficiency to route to
        /// </summary>
        /// <param name="parts">array of messages sent split by ' '</param>
        /// <param name="neighborRouter">router that sent the message</param>
        private void UpdateTable(string[] parts, EndPoint neighborRouter)
        {
            for (int i = 1; i < parts.Length; i++)
            {
                string neighbor = ExtractRouterName(neighborRouter);
                
            }
        }

        private string ExtractRouterName(EndPoint neighborRouter)
        {

            for (int i = 0; i < m_Neighbors.Count; i++)

            {
                if (m_Neighbors.ElementAt(i).Value.Item2 == ((IPEndPoint)neighborRouter).Port)
                {
                    return m_Neighbors.ElementAt(i).Key;
                }
            }
            throw new Exception("No linked router with that port");
        }

        private void ReadConfig()
        {
            string[] lines = System.IO.File.ReadAllLines(Directory + "/" + Name + ".cfg");

            foreach (string line in lines)
            {
                string[] parts = line.Split(' ');
                m_Neighbors[parts[0]] = new Tuple<int, int>(int.Parse(parts[1]), m_Neighbors[parts[0]].Item2);
            }
        }

        private void UpdateForwardTable()
        {
            throw new NotImplementedException();
        }

        private bool RouterChange(string message)
        {
            throw new NotImplementedException();
        }

        public void Shutdown()
        {
            m_update.Shutdown(SocketShutdown.Both);
            m_command.Shutdown(SocketShutdown.Both);
            m_update.Close();
            m_command.Close();
        }

        private void HandlePMessage(string[] pieces)
        {
            if (pieces.Length < 2)
            {
                Console.WriteLine("Not enough arguments were given");
            }
            if (m_RoutingTable.ContainsKey(pieces[1]))
            {
                Console.Write(pieces[0]);
                Console.Write(" ");
                Console.Write(m_RoutingTable[pieces[1]].Item1.ToString());
                Console.Write(" ");
                Console.WriteLine(m_RoutingTable[pieces[1]].Item2.ToString());
            }
            else
            {
                Console.WriteLine(pieces[0] + " was not found in the current routing table");
            }
        }
    }
}
