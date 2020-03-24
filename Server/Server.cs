using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Collections.Generic;
using Microsoft.Office.Interop.Word;

namespace Server
{
    public class WorkersManager
    {
        int max_value;
        int curr_value;
        int chunk_value;
        int[] chunks;
        Dictionary<Socket, int> workers = new Dictionary<Socket, int>();
        object obj_lock = new object();
        public event Action decrChunk_;
        public WorkersManager(int max, int chunk)
        {
            max_value = max;
            curr_value = max;
            chunk_value = chunk;
            int count_chunks = -(int)((-max_value)/chunk);
            chunks = new int[count_chunks];
            int i = 0;
            for (; i < count_chunks - 1; i++)
                chunks[i] = chunk;
            chunks[i] = max_value - (chunk * i);
        }
        private int getFreeChunk()
        {
            for(int i = 0; i < chunks.Length; i++)
            {
                if (!workers.ContainsValue(i) && chunks[i]!=0)
                {
                    return i;
                }
            }
            return -1;
        }
        public Tuple<int,int> getTask(Socket socket)
        {
            lock (obj_lock)
            {
                int chunk_ = getFreeChunk();
                if (chunk_ == -1)
                    return null;
                if (workers.ContainsKey(socket) && chunks[workers[socket]] != 0)
                    return null;
                workers.Add(socket, chunk_);
                return new Tuple<int, int>(chunk_ * chunk_value, chunks[chunk_]);
            }
        }
        public void deleteWorker(Socket socket)
        {
            lock (obj_lock)
                workers.Remove(socket);
        }
        public void deleteAll()
        {
            lock (obj_lock)
                workers.Clear();
        }
        public bool decrChunk(Socket socket)
        {
            if (workers.ContainsKey(socket) && chunks[workers[socket]] > 0)
            {
                chunks[workers[socket]]--;
                if (curr_value <= 0)
                    return false;
                curr_value--;
                decrChunk_.Invoke();
                return true;
            }
            return false;
        }
    }
    public class StateObject
    {
        // Client  socket.
        public ManualResetEvent resetEvent = null;
        public Socket workSocket = null;
        // Size of receive buffer.  
        public const int BufferSize = 1024;
        // Receive buffer.  
        public byte[] buffer = new byte[BufferSize];
        public StringBuilder sb = new StringBuilder();
    }

    public class AsynchronousSocketListener
    {
        // Thread signal.  
        public static ManualResetEvent allDone = new ManualResetEvent(false);
        WorkersManager manager;

        public AsynchronousSocketListener( WorkersManager m)
        {
            manager = m;
        }
        public static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }
        public void StartListening(CancellationToken token)
        {
            
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Parse(GetLocalIPAddress()), 11000);
  
            Socket listener = new Socket(AddressFamily.InterNetwork,
                SocketType.Stream, ProtocolType.Tcp);

            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(100);

                while (!token.IsCancellationRequested)
                {
                    // Set the event to nonsignaled state.  
                    allDone.Reset();

                    // Start an asynchronous socket to listen for connections.  
                    ConsoleWrapp.WriteLine("Waiting for a connection...");
                    listener.BeginAccept(
                        new AsyncCallback(AcceptCallback),
                        listener);

                    // Wait until a connection is made before continuing.  
                    allDone.WaitOne();
                }

            }
            catch (Exception e)
            {
                ConsoleWrapp.WriteLine(e.ToString());
            }

            ConsoleWrapp.WriteLine("\nPress ENTER to continue...");
            ConsoleWrapp.ReadLine();

        }

        public void AcceptCallback(IAsyncResult ar)
        {
            // Signal the main thread to continue.  
            allDone.Set();

            // Get the socket that handles the client request.  
            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);

            // Create the state object.

            StateObject state = new StateObject();
            state.workSocket = handler;

            ManualResetEvent receiveDone = new ManualResetEvent(false);
            state.resetEvent = receiveDone;
            //wait end of EndReceive method
            if (!BeginReceiveWithTimeout(state.buffer, 0, StateObject.BufferSize, new AsyncCallback(ReadCallback), state, handler, receiveDone, new TimeSpan(0, 0, 0, 10)))
            {
                SyncSend(handler, "close");
                handler.Shutdown(SocketShutdown.Both);
                ConsoleWrapp.WriteLine($"Time out - Close conn with {((IPEndPoint)handler.RemoteEndPoint).Address}");
                handler.Close();
            }
        }
        static bool BeginReceiveWithTimeout(byte [] bytes, int offset, int size, AsyncCallback callback, object state, Socket s, ManualResetEvent resetEvent, TimeSpan time)
        {
            resetEvent.Reset();
            s.BeginReceive(bytes, 0, StateObject.BufferSize, 0,
               callback, state);
            if (!resetEvent.WaitOne(time))
            {
                return false;
            }
            return true;
        }
        public void ReadCallback(IAsyncResult ar)
        {
            String content = String.Empty;
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;
            // Read data from the client socket.
            try
            {
                string answ = "";
                int bytesRead = handler.EndReceive(ar);
                state.resetEvent.Set();
                if (bytesRead > 0)
                {
                    content = Encoding.ASCII.GetString(state.buffer, 0, bytesRead);
                    state.sb.Append(content);
                    ConsoleWrapp.WriteLine($"Read {content.Length} bytes from {((IPEndPoint)handler.RemoteEndPoint).Address}.");
                    //parse mess
                    answ = ParseMessage(content, handler);
                    if (answ == "")
                    {
                        SyncSend(handler, "close");
                        handler.Shutdown(SocketShutdown.Both);
                        handler.Close();
                        return;
                    }
                    if (answ!="ok")
                        AsyncSend(handler, answ);
                    //wait end of EndReceive method
                    if (!BeginReceiveWithTimeout(state.buffer, 0, StateObject.BufferSize, new AsyncCallback(ReadCallback), state, handler, state.resetEvent, new TimeSpan(0, 0, 0, 10)))
                    {
                        SyncSend(handler, "close");
                        handler.Shutdown(SocketShutdown.Both);
                        handler.Close();
                        ConsoleWrapp.WriteLine("");
                    }
                }
                else
                {
                    SyncSend(handler, "close");
                    handler.Shutdown(SocketShutdown.Both);
                    handler.Close();
                }
            }
            catch (Exception e)
            {
                ConsoleWrapp.WriteLine(e.Message.ToString());
            }
            
        }
        private static void SyncSend(Socket handler, string data)
        {
            try
            {
                handler.Send(Encoding.ASCII.GetBytes(data));
                ConsoleWrapp.WriteLine($"Sent to {((IPEndPoint)handler.RemoteEndPoint).Address}");
            }
            catch (Exception e)
            {
                ConsoleWrapp.WriteLine(e.ToString());
            }
        }
        private static void AsyncSend(Socket handler, string data)
        {
            // Convert the string data to byte data using ASCII encoding.  
            byte[] byteData = Encoding.ASCII.GetBytes(data);

            // Begin sending the data to the remote device.  
            handler.BeginSend(byteData, 0, byteData.Length, 0,
                new AsyncCallback(SendCallback), handler);
            ConsoleWrapp.WriteLine($"Async sent to {((IPEndPoint)handler.RemoteEndPoint).Address}");
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.  
                Socket handler = (Socket)ar.AsyncState;
                // Complete sending the data to the remote device.  
                int bytesSent = handler.EndSend(ar);
                ConsoleWrapp.WriteLine($"Sent {bytesSent} bytes to client.");
            }
            catch (Exception e)
            {
                ConsoleWrapp.WriteLine(e.ToString());
            }
        }
        private string ParseMessage(string message, Socket s)
        {
            string[] split_mess = message.Split("\r\n");
            string answ = "";
            if (split_mess[0]!= "")
            {
                switch (split_mess[0])
                {
                    case "Unsuit":
                        if (manager.decrChunk(s))
                            answ = "ok";
                        break;
                    case "GetTask":
                        Tuple<int, int> task = manager.getTask(s);
                        if (task == null)
                        {
                            manager.deleteWorker(s);
                            return "";
                        }
                        return $"Task\r\n{task.Item1},{task.Item2}";
                    case "Suit":
                        ConsoleWrapp.WriteLine($"Find, Answer: {split_mess[1]}");
                        manager.deleteAll();
                        break;
                    default:
                        break;
                }
            }
            return answ;
        }
    }

    class Server
    {
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
        public static void LocalWorker(WorkersManager manager, string path, CancellationTokenSource tokenSource)
        {
            Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Tuple<int, int> task;
            while ((task = manager.getTask(s)) != null)
            {
                for(int i = task.Item1; i < task.Item1 + task.Item2; i++)
                {
                    if (EnterPass(path, i))
                    {
                        ConsoleWrapp.WriteLine($"Find, Answer: {i}");
                        tokenSource.Cancel();
                        return;
                    }
                    else
                    {
                        manager.decrChunk(s);
                        ConsoleWrapp.WriteLine($"Local Bruteforce - {i}");
                    }
                }
            }
        }
        static void Main(string[] args)
        {
            int max_value = 10000;
            CancellationTokenSource tokenSource = new CancellationTokenSource();
            ProgressBar bar = new ProgressBar(max_value, Console.WindowWidth / 3, new Point(0, Console.WindowHeight - 1));
            WorkersManager manager = new WorkersManager(max_value, 100);
            manager.decrChunk_ += bar.staticDraw;
            AsynchronousSocketListener server = new AsynchronousSocketListener(manager);
            System.Threading.Tasks.Task.Run(()=>server.StartListening(tokenSource.Token));
            LocalWorker(manager, @"C:\Users\user\Desktop\task1.docx", tokenSource);
        }
    }
}
