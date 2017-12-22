using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;

namespace MultiClientServer
{
    class Server
    {
        public Server(int port)
        {           
            // Luister op de opgegeven poort naar verbindingen
            TcpListener server = new TcpListener(IPAddress.Any, port);
            server.Start();

            // Start een aparte thread op die verbindingen aanneemt
            new Thread(() => AcceptLoop(server)).Start();

            // Start een thread die de console input leest en afhandelt
            new Thread(() => ConsoleInputHandler()).Start();
        }

        private void AcceptLoop(TcpListener handle)
        {
            while (true)
            {
                TcpClient client = handle.AcceptTcpClient();
                StreamReader clientIn = new StreamReader(client.GetStream());
                StreamWriter clientOut = new StreamWriter(client.GetStream());
                clientOut.AutoFlush = true;

                // De server weet niet wat de poort is van de client die verbinding maakt, de client geeft dus als onderdeel van het protocol als eerst een bericht met zijn poort
                int zijnPoort = int.Parse(clientIn.ReadLine().Split()[1]);

                // Schrijf naar de console dat de huidige port verbonden is met de ander
                Console.WriteLine("Verbonden: " + zijnPoort);

                // Zet de nieuwe verbinding in de verbindingslijst
                //Program.neighbours.Add(zijnPoort, new Connection(clientIn, clientOut));
                Program.AddConnectionToDictionary(zijnPoort, new Connection(clientIn, clientOut));

                // Stuurt alle bekende ndis informatie naar de net verbonden buur 
                lock (Program.allNodesLock)
                {
                    foreach (int i in Program.allNodes)
                    {
                        string message = "MyDist " + i + " " + Program.distanceToPort[i] + " " + Program.myPort;
                        Program.neighbours[zijnPoort].Write.WriteLine(message);
                    }
                }
                

                //Program.neighbours[zijnPoort].Write.WriteLine("RT " + Program.myPort);
            }
        }

        private void ConsoleInputHandler()
        {
            while (true)
            {
                string input = Console.ReadLine();

                if (input.StartsWith("R"))
                {
                    // Print routing table
                    Console.WriteLine(Program.myPort + " 0 local");
                    foreach (KeyValuePair<int, int> kv in Program.distanceToPort)
                    {
                        if (kv.Key != Program.myPort)
                        {
                            Console.WriteLine(kv.Key + " " + kv.Value + " " + Program.preferredNeighbours[kv.Key]);
                        }
                    }
                }
                
                else if (input.StartsWith("B"))
                {
                    Program.HandleMessage(input);
                }
                else if (input.StartsWith("C"))
                {
                    Program.HandleConnect(input, true);
                }
                else if (input.StartsWith("D"))
                {
                    Program.HandleDisconnect(input, true);
                }
                
            }
        }
    }
}
