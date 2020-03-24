using System;
using System.Net;
using System.Text;
using System.Net.Sockets;

namespace Client
{
    class Program
    {
        static void Main(string[] args)
        {
            string ip = "192.168.1.72";
            IPEndPoint ipe = new IPEndPoint(IPAddress.Parse(ip), 58900);
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(ipe);
            if (!socket.Connected)
            {
                Console.WriteLine("Fail - Connection failed");
                return;
            }
            Console.WriteLine("Connected!");
            while (true)
            {
                string str = Console.ReadLine();
                Console.WriteLine(str);
                socket.Send(Encoding.ASCII.GetBytes(str), Encoding.ASCII.GetBytes(str).Length, 0);
            }
        }
    }
}
