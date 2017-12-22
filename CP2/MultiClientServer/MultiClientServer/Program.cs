using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MultiClientServer
{
    class Program
    {
        // Variabelen
        public static int myPort;                                                                               // port number
        public static Dictionary<int, Connection> neighbours = new Dictionary<int, Connection>();               // connecties met buren 
        public static Dictionary<int, int> preferredNeighbours = new Dictionary<int, int>();                    // key = eindbestemming en value = via welke buur het snelste is
        public static Dictionary<int, int> distanceToPort = new Dictionary<int, int>();                         // key = eindbestemming en value = afstand van huidige tot eindbestemming
        public static Dictionary<Tuple<int, int>, int> ndis = new Dictionary<Tuple<int, int>, int>();           // key = tuple van ports waartussen we de afstand willen weten, minstens een van deze ports is een neighbour, value = afstand tussen deze twee ports
        public static HashSet<int> allNodes = new HashSet<int>();                                               // Alle bekende nodes in het netwerk

        // Locks
        public static Object recomputeLock = new Object();
        public static Object myDistLock = new Object();
        public static Object allNodesLock = new Object();
        public static Object neighbourLock = new Object();
        public static Object ndisLock = new Object();

        static void Main(string[] args)
        {
            // haalt het poortnummer op van de huidige port
            myPort = int.Parse(args[0]);
            // Geef console het poortnummer als titel
            Console.Title = "NetChange " + myPort;
            // maak een nieuwe server aan van de huidige port
            new Server(myPort);
            // zet de afstand naar de huidige poort op 0
            distanceToPort[myPort] = 0;
            // zet de huidige port als beste buur als de eindbestemming de huidige poort is
            preferredNeighbours[myPort] = myPort;
            // Voeg de huidige port toe aan de lijst van nodes
            allNodes.Add(myPort);

            // maak connecties aan met directe buren
            for (int i = 1; i < args.Length; i++)
            {
                // haal portnummer op
                int port = int.Parse(args[i]);
                // voeg hem toe aan de dictionary van de buren
                //if (port < myPort)
               // {
               //     AddConnectionToDictionary(port, new Connection(port));
               // }
                AddConnectionToDictionary(port, new Connection(port));
            }
            Init();
        }

        // voeg een port toe aan de burendictionary als deze nog niet bestaat
        public static void AddConnectionToDictionary(int port, Connection connection)
        {
            if (!neighbours.ContainsKey(port))
            {
                neighbours.Add(port, connection);
            }
        }

        // methode voor het verkrijgen van de beste buur bij een gegeven eindbestemming
        private static Tuple<int, int> GetClosestNeighbour(int port)
        {
            // initialize the closest distance on the size of the network and the best neighbour on -1 (undefined)
            int closest = MaxNetworkSize();
            int bestNeighbour = -1;
            // loop through the neighbours and get the distances from this neighbour to the port from the ndis

            lock (neighbourLock)
            {
                foreach (KeyValuePair<int, Connection> kv in neighbours)
                {
                    lock (ndisLock)
                    {
                        int distance = MaxNetworkSize();
                        distance = ndis[Tuple.Create(kv.Key, port)];

                        // if this distance is the smallest, update the closest and bestneighbur values
                        if (distance <= closest)
                        {
                            bestNeighbour = kv.Key;
                            closest = distance;
                        }
                    }
                }
            }

            // return the found best neighbour
            return Tuple.Create(bestNeighbour, closest);
        }

        public static void Init()
        {
            lock (ndisLock)
            {
                // loop through all the known nodes and create all combinations in the ndis dictionary with the current maxnetworksize
                foreach (int i in allNodes)
                {
                    foreach (int j in allNodes)
                    {
                        if (j != i)
                        {
                            ndis[Tuple.Create(i, j)] = MaxNetworkSize();
                            ndis[Tuple.Create(j, i)] = MaxNetworkSize();
                        }
                    }
                }
            }

            // create a mydist message from this port to itself with distance 0 and send it to all the neighbours
            string message = "MyDist " + myPort + " 0 " + myPort;

            foreach (KeyValuePair<int, Connection> neighbour in neighbours)
            {
                neighbour.Value.Write.WriteLine(message);
            }
        }

        // herberekent afstanden van het netwerk
        public static void Recompute(int port)
        {
            lock (recomputeLock)
            {
                int oldDistance = distanceToPort[port];

                // checkt of de meegegeven port de huidige port is en zet daarvan de afstand op 0
                if (port == myPort)
                {
                    distanceToPort[port] = 0;
                    preferredNeighbours[port] = port;
                }
                // bereken de afstand en tel daar 1 bij op
                else
                {
                    Tuple<int, int> tuple = GetClosestNeighbour(port);
                    int bestNeighbour = tuple.Item1;
                    int distance = tuple.Item2 + 1;
                    preferredNeighbours[port] = bestNeighbour;
                    distanceToPort[port] = distance; //ndis[Tuple.Create(bestNeighbour, port)] + 1;
                }

                // Als de afstand veranderd is, stuur dan een bericht naar ale buren
                if (distanceToPort[port] != oldDistance)
                {
                    Console.WriteLine("Afstand naar " + port + " is nu " + distanceToPort[port] + " via " + preferredNeighbours[port]);

                    string message = "MyDist " + port + " " + distanceToPort[port] + " " + myPort;

                    foreach (KeyValuePair<int, Connection> neighbour in neighbours)
                    {
                        neighbour.Value.Write.WriteLine(message);
                    }

                }
            }            
        }

        // returns the size of the network, the max distance a node can be from another one
        public static int MaxNetworkSize()
        {

            return allNodes.Count + 1;

        }

        // method that gets called for handling messages between ports
        public static void HandleMessage(string input)
        {
            // split the input in three parts, the B, the port and the message
            string[] portAndMessage = input.Split(new char[] { ' ' }, 3);
            int port = int.Parse(portAndMessage[1]);
            string message = portAndMessage[2];


            // if we don't know the destination node, write this to the console
            if (!allNodes.Contains(port))
            {
                Console.WriteLine("Poort " + port + " is niet bekend");
            }
            else if (port == myPort)
            {
                // if the message is sent to us, print it on the console
                Console.WriteLine(message);
            }
            else
            {
                int prefN = preferredNeighbours[port];
                // write to the console that we sent the message to the preferred neighbour
                Console.WriteLine("Bericht voor " + port + " doorgestuurd naar " + prefN);
                // send the message to the preferred neighbour
                neighbours[prefN].Write.WriteLine("B " + port + " " + message);
            }

        }

        public static void HandleConnect(string input, bool fromConsole)
        {
            // get the port we want to connect to
            int port = int.Parse(input.Split()[1]);
            // add this port to the neighbour dictionary
            AddConnectionToDictionary(port, new Connection(port));
            // add it to allnodes
            allNodes.Add(port);
            // set the distance to this port to 1
            distanceToPort[port] = 1;
            // and the preferred neighbour to itself
            preferredNeighbours[port] = port;
            // write a c myport message to the port we want to connect to if this method is called from the console, so for the first time. The neighbour doesn't need to send a C to us
            if (fromConsole)
            {
                neighbours[port].Write.WriteLine("C " + myPort);
            }
            // create a mydist message to this new neighbour
            string message = "MyDist " + port + " " + distanceToPort[port] + " " + myPort;
            // write the mydist to all the neighbours

            foreach (KeyValuePair<int, Connection> n in neighbours)
            {
                n.Value.Write.WriteLine(message);
            }

        }

        public static void HandleDisconnect(string input, bool fromConsole)
        {
            int port = int.Parse(input.Split()[1]);

            if (fromConsole)
            {
                neighbours[port].Write.WriteLine("D " + myPort);
            }

            neighbours.Remove(port);

            foreach (KeyValuePair<int, Connection> neighbour in neighbours)
            {
                neighbour.Value.Write.WriteLine("RT " + myPort);
            }

        }

        public static void CheckIfPortIsKnown(int port)
        {
            lock (Program.allNodesLock)
            {
                if (!allNodes.Contains(port))
                {
                    allNodes.Add(port);
                    distanceToPort[port] = MaxNetworkSize();
                    preferredNeighbours[port] = -1;

                    foreach (int i in allNodes)
                    {
                        ndis[Tuple.Create(port, /*n.Key*/i)] = MaxNetworkSize();
                        ndis[Tuple.Create(/*n.Key*/i, port)] = MaxNetworkSize();
                    }
                }
            }
        }
    }
}
