using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Microsoft.Office.Interop.Word;

namespace Client
{
    class Program
    {
        static ConcurrentQueue<string> cq = new ConcurrentQueue<string>();
        public static string Dequeue()
        {
            string str = "";
            int t1 = DateTime.Now.Millisecond;
            while (!cq.TryDequeue(out str) && (DateTime.Now.Millisecond - t1 < 10000))
            {
                Thread.Sleep(0);
            }
            return str;
        }
        public static Tuple<int, int> getTask(Socket s)
        {
            Tuple<int, int> res = null;
            string request = "GetTask\r\n";
            try
            {
                s.Send(Encoding.ASCII.GetBytes(request));
                Console.WriteLine($"Sent to {((IPEndPoint)s.RemoteEndPoint).Address}: {request}");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            string[] split = Dequeue().Trim().Split("\r\n");
            if (split[0] == "Task")
            {
                res = new Tuple<int, int>(Convert.ToInt32(split[1].Split(",")[0]), Convert.ToInt32(split[1].Split(",")[0]));
            }
            return res;
        }
        public static bool decrChunk(Socket s)
        {
            Tuple<int, int> res = null;
            string request = "Unsuit\r\n";
            try
            {
                s.Send(Encoding.ASCII.GetBytes(request));
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return false;
            }
            return true;
        }
        public static bool sendAnsw(Socket s, int i)
        {
            Tuple<int, int> res = null;
            string request = $"Suit\r\n{i}";
            try
            {
                s.Send(Encoding.ASCII.GetBytes(request));
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return false;
            }
            return true;
        }
        static bool MessageHadler(string mess)
        {
            switch(mess.Trim().Split("\r\n")[0])
            {
                case "Task":
                case "Stop":
                    cq.Enqueue(mess);
                    break;
                case "close":
                default:
                    return false;
            }
            return true;
        }
        public static void LocalWorker(Socket s, string path)
        {
            Tuple<int, int> task;
            while ((task = getTask(s)) != null)
            {
                for (int i = task.Item1; i < task.Item1 + task.Item2; i++)
                {
                    if (EnterPass(path,i))
                    {
                        sendAnsw(s, i);
                        Console.WriteLine($"Find, Answer: {i}");
                        return;
                    }
                    else
                    {
                        if (!decrChunk(s))
                            return;
                        Console.WriteLine($"Local Bruteforce - {i}");
                    }
                }
            }
        }
        public static bool EnterPass(string path, int option)
        {
            Application word = new Application();
            word.DisplayAlerts = WdAlertLevel.wdAlertsNone;
            bool flag = true;
            try
            {
                word.Documents.OpenNoRepairDialog(path, ReadOnly: true, PasswordDocument: option.ToString());
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                flag = false;
            }
            finally
            {
                word.Quit();
            }
            return flag;
        }
        static void Main(string[] args)
        {
            string ip = "192.168.1.72";
            IPEndPoint ipe = new IPEndPoint(IPAddress.Parse(ip), 11000);
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(ipe);
            if (!socket.Connected)
            {
                Console.WriteLine("Fail - Connection failed");
                return;
            }
            Console.WriteLine("Info - Connected!");
            System.Threading.Tasks.Task.Run(()=> {
                byte[] buffer = new byte[1024];
                int bytes = 0;
                string answ ;
                do
                {
                    try
                    {
                        bytes = socket.Receive(buffer);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message.ToString());
                        break;
                    }
                    answ = Encoding.ASCII.GetString(buffer, 0, bytes);
                    Console.WriteLine($"Receive from {ip}: \n{answ}");
                } while (bytes > 0 && MessageHadler(answ));
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            });
            
            LocalWorker(socket, Console.ReadLine());
        }
    }
}