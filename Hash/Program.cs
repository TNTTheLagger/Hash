using System;

namespace Hash
{
    // Example usage:
    class Program
    {
        static void Main(string[] args)
        {
            // Create a new node with a shared secret and port
            Node node = new Node("mySharedSecret", 12345);

            // Connect to an existing node in the network
            Console.Write("Connect to IP:  ");
            String ipToConnect = Console.ReadLine();
            node.Connect(ipToConnect);

            // Store a variable in the network
            node.Store("myVar", "Hello, world!");

            // Get the value of a variable from the network
            string value = node.Get("myVar");
            Console.WriteLine("Value: " + value);
        }
    }
}