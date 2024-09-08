using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;

public class Client
{
    public int Id { get; set; }
    public Socket Socket { get; set; }
    public byte[] Buffer { get; set; } = new byte[1024];
    public bool IsConnected { get; set; }
    public string ipAddress { get; set; }
    public Guid SessionId { get; set; }
    public DateTime LastHeartbeat { get; set; }
    public bool handshakeCompleted { get; set; }

    public Client(int id, Socket socket)
    {
        Id = id;
        Socket = socket;
        IsConnected = true;
        SessionId = Guid.NewGuid();
        LastHeartbeat = DateTime.Now;
        handshakeCompleted = false;
    }

    public void Close()
    {
        IsConnected = false;
        try
        {
            Socket.Shutdown(SocketShutdown.Both);
        }
        catch (SocketException) { }  // Handle cases where the socket is already closed.
        Socket.Close();
    }
}

public class EventDrivenSocketServer
{
    private Socket serverSocket;
    private bool isRunning;
    private ConcurrentDictionary<int, Client> clients = new ConcurrentDictionary<int, Client>();
    private int clientIdCounter = 0;
    private ConcurrentQueue<int> availableIds = new ConcurrentQueue<int>(); // Queue for available IDs
    private int heartbeatTimeout = 15; // Timeout for heartbeats in seconds

    public EventDrivenSocketServer(string ipAddress, int port)
    {
        serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        serverSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
        serverSocket.Bind(new IPEndPoint(IPAddress.Parse(ipAddress), port));
        serverSocket.Listen(100); // Backlog of 100
    }

    public void Start()
    {
        isRunning = true;
        Console.WriteLine("-----------------------------------");
        Console.WriteLine("**** City Runner Master Server ****");
        Console.WriteLine("****    Llama Game Factory     ****");
        Console.WriteLine("-----------------------------------");
        Console.WriteLine("Commands: stop");
        Console.WriteLine("-----------------------------------");
        Console.WriteLine("|Server| Server Initialized.");
        StartAccept(null);

        // Start checking for client heartbeats in a background thread
        Thread heartbeatThread = new Thread(CheckClientHeartbeats);
        heartbeatThread.Start();
    }

    private void RemoveClient(Client client, string reason) {
        Console.WriteLine($"|Client| Client[{client.Id}] disconnected: {reason}");
        client.Close();
        if (clients.TryRemove(client.Id, out _))
        {
            availableIds.Enqueue(client.Id); // Reuse ID
        }
    }

    private void StartAccept(SocketAsyncEventArgs acceptEventArg)
    {
        if (!isRunning) return;

        if (acceptEventArg == null)
        {
            acceptEventArg = new SocketAsyncEventArgs();
            acceptEventArg.Completed += OnAcceptCompleted;
        }
        else
        {
            acceptEventArg.AcceptSocket = null;
        }

        bool willRaiseEvent = serverSocket.AcceptAsync(acceptEventArg);
        if (!willRaiseEvent)
        {
            ProcessAccept(acceptEventArg);
        }
    }

    private void OnAcceptCompleted(object sender, SocketAsyncEventArgs e)
    {
        if (isRunning)
        {
            ProcessAccept(e);
        }
    }

    private void ProcessAccept(SocketAsyncEventArgs e)
    {
        if (!isRunning) return;

        // Get an available client ID or generate a new one
        int clientId;
        if (!availableIds.TryDequeue(out clientId))
        {
            clientId = Interlocked.Increment(ref clientIdCounter);
        }

        Client client = new Client(clientId, e.AcceptSocket);
        client.ipAddress = e.AcceptSocket.RemoteEndPoint.ToString();

        clients.TryAdd(client.Id, client);
        Console.WriteLine($"|Client| Client[{client.Id}] Connected | IP: {client.ipAddress}");

        StartReceive(client);
        StartAccept(e);
    }

    private void StartReceive(Client client)
    {
        SocketAsyncEventArgs receiveEventArgs = new SocketAsyncEventArgs();
        receiveEventArgs.SetBuffer(client.Buffer, 0, client.Buffer.Length);
        receiveEventArgs.UserToken = client;
        receiveEventArgs.Completed += OnReceiveCompleted;

        bool willRaiseEvent = client.Socket.ReceiveAsync(receiveEventArgs);
        if (!willRaiseEvent)
        {
            ProcessReceive(receiveEventArgs);
        }
    }

    private void OnReceiveCompleted(object sender, SocketAsyncEventArgs e)
    {
        ProcessReceive(e);
    }

    private void ProcessReceive(SocketAsyncEventArgs e)
    {
        Client client = e.UserToken as Client;

        if (client == null || client.Socket == null || IsSocketDisconnected(client.Socket))
        {
            // Socket is already disconnected, avoid further actions
            return;
        }

        if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
        {
            string receivedData = Encoding.ASCII.GetString(e.Buffer, e.Offset, e.BytesTransferred);

            string response = ProcessRpcCommand(receivedData, client);

            StartSend(client, response);
        }
        else if (e.SocketError == SocketError.ConnectionReset || e.BytesTransferred == 0 || IsSocketDisconnected(client.Socket))
        {
            RemoveClient(client, "Connection reset by client.");
        }
        else
        {
            RemoveClient(client, "Socket Error: " + e.SocketError);
        }
    }

    private bool IsSocketDisconnected(Socket socket)
    {
        try
        {
            return socket.Poll(1000, SelectMode.SelectRead) && socket.Available == 0;
        }
        catch (ObjectDisposedException)
        {
            // If the socket is already disposed, we consider it disconnected
            return true;
        }
        catch (SocketException)
        {
            return true; // Assume socket is disconnected if an exception occurs
        }
    }

    private void StartSend(Client client, string data)
    {
        byte[] byteData = Encoding.ASCII.GetBytes(data);
        SocketAsyncEventArgs sendEventArgs = new SocketAsyncEventArgs();
        sendEventArgs.SetBuffer(byteData, 0, byteData.Length);
        sendEventArgs.UserToken = client;
        sendEventArgs.Completed += OnSendCompleted;

        bool willRaiseEvent = client.Socket.SendAsync(sendEventArgs);
        if (!willRaiseEvent)
        {
            ProcessSend(sendEventArgs);
        }
    }

    private void OnSendCompleted(object sender, SocketAsyncEventArgs e)
    {
        ProcessSend(e);
    }

    private void ProcessSend(SocketAsyncEventArgs e)
    {
        Client client = e.UserToken as Client;

        if (e.SocketError == SocketError.Success)
        {
            StartReceive(client);
        }
        else
        {
            //Console.WriteLine($"Error sending data to Client {client.Id}: {e.SocketError}");
        }
    }

    // The RpcCommand processor that handles greet, heartbeat, and other commands
    private string ProcessRpcCommand(string jsonData, Client client)
    {
        try
        {
            JObject jsonRequest = JObject.Parse(jsonData);
            string requestId = jsonRequest["RequestId"]?.ToString();
            string command = jsonRequest["Command"]?.ToString();

            if (command == "greet")
            {
                string auth = jsonRequest["Parameters"]?["auth"]?.ToString();
                client.handshakeCompleted = true;
                Console.WriteLine($"|Client| Client[{client.Id}] Handshake Completed | IP: {client.ipAddress}");

                RpcResponse response = new RpcResponse
                {
                    Result = auth == "false" ? "false" : "true",
                    Error = null,
                    RequestId = requestId,
                    Parameters = new
                    {
                        ClientId = client.Id,
                        IpAddress = client.ipAddress,
                        SessionId = client.SessionId
                    }
                };

                return JsonConvert.SerializeObject(response);
            }
            else if (command == "heartbeat")
            {
                client.LastHeartbeat = DateTime.Now; // Update heartbeat timestamp
                RpcResponse response = new RpcResponse
                {
                    Result = "ok",
                    Error = null,
                    RequestId = requestId,
                    Parameters = null
                };
                return JsonConvert.SerializeObject(response);
            }
            else
            {
                if (!client.handshakeCompleted)
                {
                    clients.TryRemove(client.Id, out _);
                    client.Close();
                    availableIds.Enqueue(client.Id); // Reuse ID
                }
                RpcResponse unknownCommandResponse = new RpcResponse
                {
                    Result = null,
                    Error = $"Unknown command: {command}",
                    RequestId = requestId,
                    Parameters = null
                };
                return JsonConvert.SerializeObject(unknownCommandResponse);
            }
        }
        catch (JsonException ex)
        {
            RpcResponse errorResponse = new RpcResponse
            {
                Result = null,
                Error = $"Invalid JSON format: {ex.Message}",
                RequestId = null,
                Parameters = null
            };

            return JsonConvert.SerializeObject(errorResponse);
        }
    }

    private void CheckClientHeartbeats()
    {
        while (isRunning)
        {
            DateTime now = DateTime.Now;

            // Partition clients for parallel processing
            int numChunks = Environment.ProcessorCount;  // Number of threads based on available CPU cores
            int chunkSize = clients.Count / numChunks + 1;
            
            List<Task> tasks = new List<Task>();

            var clientList = clients.Values.ToList();  // Snapshot of clients at the moment

            for (int i = 0; i < numChunks; i++)
            {
                var chunk = clientList.Skip(i * chunkSize).Take(chunkSize).ToList();
                tasks.Add(Task.Run(() =>
                {
                    foreach (var client in chunk)
                    {
                        if ((now - client.LastHeartbeat).TotalSeconds > heartbeatTimeout)
                        {
                            // TODO: Generic Disconnect
                            RemoveClient(client, "Timeout");
                        }
                    }
                }));
            }

            Task.WaitAll(tasks.ToArray());  // Wait for all tasks to finish
            Thread.Sleep(5000);  // Check every 5 seconds
        }
    }

    public void Stop()
    {
        isRunning = false;

        foreach (var client in clients.Values)
        {
            client.Close();
        }

        serverSocket.Close();
        Console.WriteLine("|Server| Server stopped.");
    }

    public bool IsRunning => isRunning;

    public void ShowClientStatus()
    {
        Console.WriteLine("Connected Clients:");
        foreach (var client in clients.Values)
        {
            Console.WriteLine($"Client [{client.Id}] | IP: {client.ipAddress})");
        }
    }
}

public class TcpServer
{
    public static void Main(string[] args)
    {
        EventDrivenSocketServer server = new EventDrivenSocketServer("0.0.0.0", 5000);
        server.Start();
        string input = "";  // Initialize the input variable to avoid the unassigned variable error
        while (server.IsRunning && (input = Console.ReadLine()) != "stop")
        {
            if (string.IsNullOrEmpty(input))
            {
                server.ShowClientStatus(); // Show client status on ENTER
            }
        }

        // Stop the server
        server.Stop();
    }
}