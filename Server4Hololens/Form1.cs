using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.Threading.Tasks;

// Server = Listener
namespace Server4Hololens
{
    public partial class Form1 : Form
    {
        private bool IsServerRunning = false;
        private Socket handler;
        private TcpListener tcpListener;
        CancellationTokenSource tokenSource;
        CancellationToken token;
        public IPAddress serverAddress;
        public int serverPort;

        public delegate void OnSendResponseHandler(Socket handler);
        public event OnSendResponseHandler OnSendResponse;
        public void SendResponse(Socket handler)
        {
            Send(handler, textBox1.Text);
        }

        public Form1()
        {
            InitializeComponent();
            OnSendResponse += SendResponse;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (!IsServerRunning)
            {
                StartServerAsync();
                IsServerRunning = true;
                tokenSource = new CancellationTokenSource();
                token = tokenSource.Token;
                var task = Task.Run(() =>
                {
                    while (!token.IsCancellationRequested)
                    {
                        ListenAsync();
                    }
                }, token);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (IsServerRunning)
            {
                CloseServer();
                IsServerRunning = false;
                Console.WriteLine("Closed Server");
            }
        }

        //private IPEndPoint localEndPoint;
        private Socket socketListener;
        public static ManualResetEvent communicationCompleted = new ManualResetEvent(false);
        // State object for reading client data asynchronously  
        public class StateObject
        {  
            public Socket workSocket = null;
            public const int BufferSize = 1024; 
            public byte[] buffer = new byte[BufferSize]; 
            public StringBuilder sb = new StringBuilder();
        }

        private void SetServerEndpointInfo()
        {
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress ipAddress = ipHostInfo.AddressList[2];
            serverAddress = ipAddress;
            int port = 11000;
            serverPort = port;
            label2.Text = serverAddress.ToString() + " : " + serverPort.ToString();
        }

        private void StartServerAsync()
        {
            SetServerEndpointInfo();
            IPEndPoint localEndPoint = new IPEndPoint(serverAddress, serverPort);
            socketListener = new Socket(serverAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            socketListener.Bind(localEndPoint);
            socketListener.Listen(1);
            Console.WriteLine("Server has started");
        }

        private void ListenAsync()
        {
            communicationCompleted.Reset();
            Console.WriteLine("Waiting for a connection...");
            socketListener.BeginAccept(new AsyncCallback(AcceptConnectionHandler), socketListener);
            communicationCompleted.WaitOne();
        }

        private void AcceptConnectionHandler(IAsyncResult ar)
        {
            communicationCompleted.Set();
            Socket handler = socketListener.EndAccept(ar);
  
            StateObject state = new StateObject();
            state.workSocket = handler;
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadHandler), state);
        }

        private void ReadHandler(IAsyncResult ar)
        {
            String content = String.Empty;  
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;
 
            int bytesRead = handler.EndReceive(ar);
            if (bytesRead < 1024)
            {  
                state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));
                content = state.sb.ToString();
                Console.WriteLine("Read {0} bytes from socket. \n Data : {1}", content.Length, content);
                OnSendResponse(handler);
            }
            else
            {
                handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadHandler), state);
            }
            
        }

        private void Send(Socket socket, String data)
        { 
            byte[] byteData = Encoding.ASCII.GetBytes(data);
            socket.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), socket);
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            { 
                Socket handler = (Socket)ar.AsyncState; 
                int bytesSent = handler.EndSend(ar);
                Console.WriteLine("Sent {0} bytes to client.", bytesSent);
                handler.Shutdown(SocketShutdown.Both);
                handler.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private void CloseServer()
        {
            tokenSource.Cancel();
            tcpListener.Stop();
            label2.Text = "";
        }

        private void button3_Click(object sender, EventArgs e)
        {
            StartClient();
        }

        private void StartClient()
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
