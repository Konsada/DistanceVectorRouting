﻿using System;
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
        int m_infinity = 64;
        // table to send packets to |Destination|Cost|NextHop|
        public Dictionary<string, int> m_RoutingTable = new Dictionary<string, int>();

        public Dictionary<string, int> m_Neighbors = new Dictionary<string, int>();

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
            set
            {
                m_command.Bind(new IPEndPoint(Dns.GetHostAddresses(Host)[1], value));
            }
        }
        public int UpdatePort
        {
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
            UdpClient neighborClient = new UdpClient(11000);
            IPEndPoint sender = new IPEndPoint(IPAddress.Any, 11000);
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
                    updatePort.Bind(new IPEndPoint(ipAddress, 11000));

                    commandPort = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    commandPort.Bind(new IPEndPoint(ipAddress, 11001));

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
                                ProcessMessage(msg);
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

        private void ProcessMessage(string msg)
        {
            string[] parts = msg.Split(' ');
            switch (parts[0])
            {
                case "U":
                    UpdateRouter(parts);
                    break;
                case "L":
                    LinkCost(parts);
                    break;
                default:
                    Console.WriteLine("Cannot recognize update message command");
                    break;
            }
        }

        private void LinkCost(string[] parts)
        {
            // if link cost changed, then change the dictionary value
            if (m_Neighbors[parts[1]] != int.Parse(parts[2]))
            {
                m_Neighbors[parts[1]] = int.Parse(parts[2]);
            }
                
        }

        private void UpdateRouter(string[] parts)
        {
            throw new NotImplementedException();
        }

        private void ReadConfig()
        {
            string[] lines = System.IO.File.ReadAllLines(Directory + "/" + Name + ".cfg");

            foreach (string line in lines)
            {
                string[] parts = line.Split(' ');
                m_Neighbors.Add(parts[0], int.Parse(parts[1]));
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
    }
}
