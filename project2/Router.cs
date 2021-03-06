﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

//Authors: Keon Sadatian (11169575) & Erik Lystad (11338869)
//Course: CptS455
//Instructor: Dr. Carl Hauser
//Assignment: Project 2

namespace project2
{
    public class Router
    {
        /// <summary>
        /// Village Bicycle of UdpClients, everyone gets to use it
        /// </summary>
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
        public int UpdatePort { get; set; }
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
            WriteToFile("Starting: " + Directory);
            SendUMessage();

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
                            ProcessMessage(msg, neighborRouter);
                        }
                    }

                    if (watch.Elapsed.Seconds >= 10)
                    {
                        Write((watch.ElapsedMilliseconds / 1000.00));
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

            string link = parts[1];

            // if link cost changed, then change the dictionary value
            if (m_Neighbors[link].Item1 != int.Parse(parts[2]))
            {
                Console.WriteLine("Recieved the following message:");
                Console.WriteLine(parts[0] + " " + parts[1] + " " + parts[2].ToString());
                // change routertable entry by difference then send update
                //m_RoutingTable[link] = new Tuple<int, string>(int.Parse(parts[2]), m_RoutingTable[link].Item2);
                m_RoutingTable[link] = new Tuple<int, string>(int.Parse(parts[2]), link);
                m_Neighbors[link] = new Tuple<int, int>(int.Parse(parts[2]), m_Neighbors[link].Item2);

                Write(link, m_RoutingTable[link].Item1, link);
                SendUMessage();
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
                if (string.Compare(dest, Name) == 0)
                    dest = neighbor;
                int costToDest = m_RoutingTable[dest].Item1;

                // check if update is more efficient
                if ((costToNeighbor + destCost) < costToDest)
                {
                    m_RoutingTable.Remove(dest);
                    m_RoutingTable.Add(dest, new Tuple<int, string>(costToNeighbor + destCost, neighbor));
                    routingTableUpdated = true;
                }

                Write(dest, m_RoutingTable[dest].Item1, m_RoutingTable[dest].Item2);
            }
            if (routingTableUpdated)
                // send advertisement
                SendUMessage();
        }
        private void SendPoisonUMessage()
        {
            byte[] buffer = new byte[1024];
            Dictionary<string, string> lines = new Dictionary<string, string>();
            foreach (KeyValuePair<string, Tuple<int, string>> router in m_RoutingTable)
            {
                StringBuilder sb = new StringBuilder("U");
                foreach (KeyValuePair<string, Tuple<int, string>> entry in m_RoutingTable)
                {
                    sb.Append(" " + entry.Key);
                    if (Poisoned && m_Neighbors[entry.Key].Item1 < 64 && m_RoutingTable[entry.Key].Item2 != entry.Key)
                    {
                        //lie!
                        sb.Append(" 64");

                    }
                    else
                    {
                        sb.Append(" " + entry.Value.Item1);
                    }
                }
                lines.Add(router.Key, sb.ToString().Trim());
            }
            foreach (KeyValuePair<string, Tuple<int, int>> neighbor in m_Neighbors)
            {
                if (neighbor.Value.Item1 < 64)
                {
                    buffer = Encoding.ASCII.GetBytes(lines[neighbor.Key]);
                    neighborClient.Send(buffer, buffer.Length, "localhost", neighbor.Value.Item2);
                }
            }
        }
        private void SendUMessage()
        {
            if (Poisoned)
            {
                SendPoisonUMessage();
                return;
            }

            byte[] buffer = new byte[1024];
            StringBuilder sb = new StringBuilder("");
            string completedMessage;

            // Assemble update message
            sb.Append("U");
            foreach (KeyValuePair<string, Tuple<int, string>> entry in m_RoutingTable)
            {
                sb.Append(" " + entry.Key);
                if (Poisoned && m_Neighbors[entry.Key].Item1 < 64)
                {
                    if (m_RoutingTable[entry.Key].Item2 != entry.Key)
                    {
                        sb.Append(" " + 64);
                    }
                }
                else
                {
                    sb.Append(" " + entry.Value.Item1.ToString());
                }
            }
            completedMessage = sb.ToString().Trim();
            buffer = Encoding.ASCII.GetBytes(completedMessage);

            foreach (KeyValuePair<string, Tuple<int, int>> neighbor in m_Neighbors)
            {
                if (neighbor.Value.Item1 < 64)
                {
                    neighborClient.Send(buffer, buffer.Length, "localhost", neighbor.Value.Item2);
                }
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
                m_RoutingTable[parts[0]] = new Tuple<int, string>(int.Parse(parts[1]), parts[0]);
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
                Console.WriteLine("Now printing routing table!");
                foreach (KeyValuePair<string, Tuple<int, string>> entry in m_RoutingTable)
                {
                    //Console.WriteLine(Name + " - dest: " + entry.Key + " cost: " + entry.Value.Item1.ToString() + " nexthop: " + entry.Value.Item2);
                    Write(entry.Key, entry.Value.Item1, entry.Value.Item2);
                }
            }
            else
            {
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
        private void Write(double time)
        {
            Console.WriteLine(Name + " - " + time);
            WriteToFile(Name + " - " + time);
        }
        private void Write(string dest, int cost, string nexthop)
        {
            Console.WriteLine(Name + " - dest: " + dest + " cost: " + cost + " nexthop: " + nexthop);
            WriteToFile(Name + " - dest: " + dest + " cost: " + cost + " nexthop: " + nexthop);
        }
        private void WriteToFile(string message)
        {
            using (System.IO.StreamWriter file = new System.IO.StreamWriter("./output/OUTPUT" + Name + ".txt", true))
            {
                file.WriteLine(message);
            }
        }
    }
}