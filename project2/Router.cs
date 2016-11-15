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
        int m_commandPort;
        int m_updatePort;
        int m_infinity = 64;
        UdpClient neighborClient;

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
            CommandPort = cPort;
            UpdatePort = uPort;
            Poisoned = poison;
        }
        ///// <summary>
        ///// Creates a router object.
        ///// </summary>
        ///// <param name="name"> Router name </param>
        ///// <param name="host"> Host IP </param>
        ///// <param name="cPort"> Command Port Number </param>
        ///// <param name="uPort"> Update Port Number </param>
        ///// <param name="poison"> Set for Poisoned Reverse </param>
        //public Router(string name, IPEndPoint host, int cPort, int uPort, bool poison)
        //{
        //    Name = name;
        //    Host = host.Address.ToString();
        //    m_commandPort = cPort;
        //    m_updatePort = uPort;
        //    CommandPort = cPort;
        //    UpdatePort = uPort;
        //    Poisoned = poison;
        //    if (uPort == 0)
        //        m_update = null;
        //    else
        //    {
        //        m_update = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        //        m_update.Bind(host);

        //    }
        //    if (cPort == 0)
        //        m_command = null;
        //    else
        //    {
        //        m_command = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        //        m_command.Bind(host);
        //    }
        //}

        public string Name { get; private set; }
        string Host { get; set; }
        public string Directory { get; set; }
        public int CommandPort { get; set; }
        public int UpdatePort {get; set;}
        public bool Poisoned { get; set; }
        Socket Update { get; set; }
        Socket Command { get; set; }
        #endregion
        public void Start()
        {
            IPHostEntry hostEntry = Dns.GetHostEntry(Host);
            IPAddress ipAddress = hostEntry.AddressList[1];
            neighborClient = new UdpClient(UpdatePort);
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
                System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
                watch.Start();

                while (true)
                {
                    ArrayList listenList = new ArrayList();
                    ArrayList acceptList = new ArrayList();

                    // bind sockets to ports.
                    Update = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    Update.Bind(new IPEndPoint(ipAddress, UpdatePort));

                    Command = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    Command.Bind(new IPEndPoint(ipAddress, CommandPort));

                    listenList.Add(Update);
                    listenList.Add(Command);
                    Socket.Select(listenList, null, null, 10 - watch.Elapsed.Seconds);

                    int read = 0;
                    for (int i = 0; i < listenList.Count; i++)
                    {
                        read = ((Socket)listenList[i]).ReceiveFrom(bytes, ref neighborRouter);
                        if (read > 0)
                        {
                            string msg = Encoding.ASCII.GetString(bytes, 0, read);
                        }
                    }

                    if (watch.Elapsed.Seconds >= 10)
                    {
                        Console.WriteLine(Name.ToString() + " - " + (watch.ElapsedMilliseconds / 1000).ToString());
                        SendUMessage();
                        watch.Restart();
                    }

                    ShutdownSockets();
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

                // change routertable entry by difference then send update
                m_RoutingTable[parts[1]] = new Tuple<int, string>((m_Neighbors[parts[1]].Item1 - int.Parse(parts[2])) + m_RoutingTable[parts[1]].Item1,
                    m_RoutingTable[parts[1]].Item2);
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
            // get name of the neighbor router that sent the update
            string neighbor = ExtractRouterName(neighborRouter);
            int costToNeighbor = m_Neighbors[neighbor].Item1;
            bool routingTableUpdated = false;
            // compare cost to routingTable, parts contains command character 'U', hence why i starts at 1
            for (int i = 1; i < parts.Length; i++)
            {
                string dest = parts[i]; i++;
                int destCost = int.Parse(parts[i]);
                int costToDest = m_RoutingTable[dest].Item1;

                // check if update is more efficient
                if ((costToNeighbor + destCost) < costToDest)
                {
                    m_RoutingTable.Remove(dest);
                    m_RoutingTable.Add(dest, new Tuple<int, string>(costToNeighbor + destCost, neighbor));
                    routingTableUpdated = true;
                }
            }
            if (routingTableUpdated)
                // send advertisement
                SendUMessage();
        }

        private void SendUMessage()
        {
            byte[] buffer = new byte[1024];
            StringBuilder sb = new StringBuilder("");
            string completedMessage;

            sb.Append("U");
            foreach (KeyValuePair<string, Tuple<int, string>> entry in m_RoutingTable)
            {
                sb.Append(" " + entry.Key);
                sb.Append(" " + entry.Value.Item1.ToString());
            }
            completedMessage = sb.ToString().Trim();
            buffer = Encoding.ASCII.GetBytes(completedMessage);

            foreach (KeyValuePair<string, Tuple<int, int>> neighbor in m_Neighbors)
            {
                neighborClient.Send(buffer, buffer.Length, "localhost", neighbor.Value.Item2);
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

        public void ShutdownSockets()
        {
            Update.Shutdown(SocketShutdown.Both);
            Command.Shutdown(SocketShutdown.Both);
            Update.Close();
            Command.Close();
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