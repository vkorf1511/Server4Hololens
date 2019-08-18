using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;


namespace HololensServer
{
    public class Server
    {
        private bool IsServerRunning = false;
        private bool IsListening = false;
        CancellationTokenSource tokenSource;
        CancellationToken token;
        public IPAddress serverAddress;
        public int serverPort;
  
        public delegate void OnSendResponseHandler(string response);
        public event OnSendResponseHandler OnSendResponse;
        public void SendResponse(string response)
        {
            Send(response);
        }

        protected delegate void OnReadRequestHandler();
        protected event OnReadRequestHandler OnReadRequest;
        protected void ReadRequest()
        {
            ReadMessage();
        }

        private string remoteEndpoint = "";
        protected delegate void OnClientConnectedHandler(string remoteEndpoint);
        protected event OnClientConnectedHandler OnClientConnected;
        protected delegate void UpdateTextHandler();
        protected virtual void ClientConnected(string re)
        {
            remoteEndpoint = re;
        }

        protected delegate void OnResetServerHandler();
        protected event OnResetServerHandler OnResetServer;

        public Server()
        {
            InitializeServer();
            OnReadRequest += ReadRequest;
            OnSendResponse += SendResponse;
            OnClientConnected += ClientConnected;
            OnResetServer += ResetServer;
        }

        private void InitializeServer()
        {
            SetServerEndpointInfo();
            IPEndPoint localEndPoint = new IPEndPoint(serverAddress, serverPort);
            socketListener = new Socket(serverAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            socketListener.Bind(localEndPoint);
            socketListener.Listen(1);
            Console.WriteLine("Server has started");
        }

        public void StartServer()
        {
            if (!IsServerRunning)
            {
                IsServerRunning = true;
                tokenSource = new CancellationTokenSource();
                token = tokenSource.Token;
                var listeningTask = Task.Run(() =>
                {
                    ListenAsync(); //waits for client to connect and creates working socket.
                }).ContinueWith((t) =>
                {
                    while (true)
                    {
                        if (!IsListening)
                            ReadRequest();
                    }
                });
            }
        }

        public void CloseServer()
        {
            if (IsServerRunning)
            {
                IsServerRunning = false;
                if (workingConnection != null)
                {
                    tokenSource.Cancel();
                    workingConnection.workingSocket.Shutdown(SocketShutdown.Both);
                    workingConnection.workingSocket.Close();
                    workingConnection = null;
                }
                Console.WriteLine("Closed Server");
            }
        }

        public void ResetServer()
        {
            IsListening = false;
            CloseServer();
            StartServer();
        }

        private Socket socketListener;
        public static ManualResetEvent clientConnected = new ManualResetEvent(false);
        private class StateObject
        {
            public Socket workingSocket = null;
            public const int BufferSize = 1024;
            public byte[] buffer = new byte[BufferSize];
            public StringBuilder sb = new StringBuilder();
        }

        private StateObject workingConnection;

        private void SetServerEndpointInfo()
        {
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress ipAddress = ipHostInfo.AddressList[2];
            serverAddress = ipAddress;
            int port = 11000;
            serverPort = port;
        }

        private void ListenAsync()
        {
            clientConnected.Reset();
            Console.WriteLine("Waiting for a connection...");
            socketListener.BeginAccept(new AsyncCallback(AcceptConnectionHandler), null);
            clientConnected.WaitOne();
        }

        private void AcceptConnectionHandler(IAsyncResult ar)
        {
            clientConnected.Set();
            workingConnection = new StateObject();
            workingConnection.workingSocket = socketListener.EndAccept(ar);
            OnClientConnected(workingConnection.workingSocket.RemoteEndPoint.ToString());
            Console.WriteLine("Client Connected!");
        }

        public void ReadMessage()
        {
            try
            {
                if (workingConnection != null)
                {
                    IsListening = true;
                    workingConnection.workingSocket.BeginReceive(workingConnection.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadHandler), null);
                }
            }
            catch
            {
                OnResetServer();
            }
        }

        private void ReadHandler(IAsyncResult ar)
        {
            try
            {
                string content = string.Empty;
                int bytesRead = workingConnection.workingSocket.EndReceive(ar);
                workingConnection.sb.Append(Encoding.ASCII.GetString(workingConnection.buffer, 0, bytesRead));
                content = workingConnection.sb.ToString();
                Console.WriteLine("Read {0} bytes from socket. \n Data : {1}", content.Length, content);
                Array.Clear(workingConnection.buffer, 0, StateObject.BufferSize);
                workingConnection.sb.Clear();
                IsListening = false;
                if (content.Length == 0)
                    OnResetServer();
            }
            catch
            {
                Console.WriteLine("Connection ended prematurely");
                OnResetServer();
            }
        }

        public void Send(string data)
        {
            try
            {
                byte[] byteData = Encoding.ASCII.GetBytes(data);
                workingConnection.workingSocket.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendHandler), null);
            }
            catch
            {
                if (workingConnection != null)
                {
                    Console.WriteLine("Client ended the communication");
                    ResetServer();
                }
            }
        }

        private void SendHandler(IAsyncResult ar)
        {
            try
            {
                int bytesSent = workingConnection.workingSocket.EndSend(ar);
                Console.WriteLine("Sent {0} bytes to client.", bytesSent);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        public void StartClient()
        {
            try
            {
                // Create a TcpClient.
                // Note, for this client to work you need to have a TcpServer 
                // connected to the same address as specified by the server, port
                // combination.
                TcpClient client = new TcpClient(serverAddress.ToString(), serverPort);

                // Translate the passed message into ASCII and store it as a Byte array.
                string message = "Send me something";
                Byte[] data = System.Text.Encoding.ASCII.GetBytes(message);
                NetworkStream stream = client.GetStream();  // Get a client stream for reading and writing.
                stream.Write(data, 0, data.Length); // Send the message to the connected TcpServer. 

                Console.WriteLine("Sent: {0}", message);

                // Receive the TcpServer.response.       
                data = new Byte[256]; // Buffer to store the response bytes. 
                String responseData = String.Empty;  // String to store the response ASCII representation.

                // Read the first batch of the TcpServer response bytes.
                Int32 bytes = stream.Read(data, 0, data.Length);
                responseData = System.Text.Encoding.ASCII.GetString(data, 0, bytes);
                Console.WriteLine("Received: {0}", responseData);

                // Close everything.
                stream.Close();
                client.Close();
            }
            catch (ArgumentNullException e)
            {
                Console.WriteLine("ArgumentNullException: {0}", e);
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
            }

            Console.WriteLine("\n Communication finished. Press Enter to continue...");
        }
    }
}
