using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Threading;

namespace MultiClientServer
{
    class Connection
    {
        public StreamReader Read;
        public StreamWriter Write;

        // Connection heeft 2 constructoren: deze constructor wordt gebruikt als wij CLIENT worden bij een andere SERVER
        public Connection(int port)
        {
            TcpClient client = new TcpClient("localhost", port);
            Read = new StreamReader(client.GetStream());
            Write = new StreamWriter(client.GetStream());
            Write.AutoFlush = true;
        
            // De server kan niet zien van welke poort wij client zijn, dit moeten we apart laten weten
            Write.WriteLine("Poort: " + Program.myPort);

            // Start het reader-loopje
            new Thread(ReaderThread).Start();
        }

        // Deze constructor wordt gebruikt als wij SERVER zijn en een CLIENT maakt met ons verbinding
        public Connection(StreamReader read, StreamWriter write)
        {
            Read = read; Write = write;

            // Start het reader-loopje
            new Thread(ReaderThread).Start();
        }

        // LET OP: Nadat er verbinding is gelegd, kun je vergeten wie er client/server is (en dat kun je aan het Connection-object dus ook niet zien!)

        // Deze loop leest wat er binnenkomt en print dit
        public void ReaderThread()
        {
            try
            {
                while (true)
                {
                    string input = Read.ReadLine();
                    string[] splittedInput = input.Split();

                    if (input.StartsWith("MyDist"))
                    {
                        // get the toPort, distance and fromPort values from the message
                        int toPort = int.Parse(splittedInput[1]);
                        int distance = int.Parse(splittedInput[2]);
                        int fromPort = int.Parse(splittedInput[3]);

                        Program.CheckIfPortIsKnown(toPort);
                        Program.CheckIfPortIsKnown(fromPort);

                        // set the distance to the ndis from the toport to the fromport and the other way around
                        Program.ndis[Tuple.Create(fromPort, toPort)] = distance;
                        Program.ndis[Tuple.Create(toPort, fromPort)] = distance;
                        // recompute
                        Program.Recompute(toPort);
                    }
                    else if (input.StartsWith("RoutingTable"))
                    {
                        int port = int.Parse(splittedInput[1]);
                        foreach (int i in Program.allNodes)
                        {
                            string message = "MyDist " + i + " " + Program.distanceToPort[i] + " " + Program.myPort;
                            Program.neighbours[port].Write.WriteLine(message);
                        }
                    }
                    
                    else if (input.StartsWith("B"))
                    {
                        Program.HandleMessage(input);
                    }
                    else if (input.StartsWith("C"))
                    {
                        Program.HandleConnect(input, false);
                    }
                    else if (input.StartsWith("D"))
                    {
                        Program.HandleDisconnect(input, false);
                    }
                    
                }
            }
            catch { } // Verbinding is kennelijk verbroken
        }
    }
}
