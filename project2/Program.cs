﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Collections;

//Authors: Keon Sadatian (11169575) & Erik Lystad (11338869)
//Course: CptS455
//Instructor: Dr. Carl Hauser
//Assignment: Project 2

namespace project2
{
    class Program
    {
        public static string data = null;

        Dictionary<string, Router> m_neighborTable = new Dictionary<string, Router>(); // router name and port number

        static void Main(string[] args)
        {
            StartRouters(args);
        }
        public static void StartRouters(string[] args)
        {
            string dir, routerName;
            bool poisonReverse = false;

            if (args[0].Contains("-p"))
            {
                poisonReverse = true;
                dir = args[1];
                routerName = args[2];
            }
            else
            {
                dir = args[0];
                routerName = args[1];
            }

            Router router = new Router(routerName, "localhost", 0, 0, poisonReverse);
            LoadInfo(dir, router);
            router.Start();
            //Task task = Task.Factory.StartNew(() => router.Start());
        }

        private static void LoadInfo(string dir, Router router)
        {
            string[] lines = System.IO.File.ReadAllLines(dir + "/" + "routers");
            string[] parts;
            int m_commandPartIndex = 2;
            int m_updatePartIndex = 3;
            foreach (string line in lines)
            {
                parts = line.Split(' ');
                if (string.Compare(parts[0], "#") == 0) continue;
                if (string.Compare(parts[0], router.Name) == 0)
                {
                    router.CommandPort = int.Parse(parts[m_commandPartIndex]);
                    router.UpdatePort = int.Parse(parts[m_updatePartIndex]);
                    router.Directory = dir;
                }
                else
                {
                    router.m_Neighbors.Add(parts[0], new Tuple<int, int>(64, int.Parse(parts[3])));
                    router.m_RoutingTable.Add(parts[0], new Tuple<int, string>(64, null));
                }
            }
        }
    }
}
