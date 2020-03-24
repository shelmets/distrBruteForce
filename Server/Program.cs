using System;
using System.Net;
using System.Text;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Server
{
    class Program
    {
        static void Main(string[] args)
        {
            IPEndPoint ipe = new IPEndPoint(IPAddress.Any, 58900);
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(ipe);

            socket.Listen(1);

            Console.WriteLine("Done - Server started");
            Task.Run(()=> {
                while (true)
                {
                    Socket socket_ = socket.Accept();
                    int bytes = 0;
                    byte[] data = new byte[256];
                    do
                    {
                        bytes = socket_.Receive(data);
                        Console.WriteLine($"Info - Received mess from {((IPEndPoint)(socket_.RemoteEndPoint)).Address.ToString()}:" + Encoding.ASCII.GetString(data, 0, bytes));
                    } while (socket_.Available > 0);

                    socket_.Shutdown(SocketShutdown.Send);
                    socket.Close();
                }
            });
            while(Console.ReadKey().Key != ConsoleKey.Enter)
            {}

        }
    }
}
