using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class Client
{
    public int Id { get; set; }
    public Socket Socket { get; set; }
    public byte[] Buffer { get; set; } = new byte[1024];
    public bool IsConnected { get; set; }
    public string ipAddress { get; set; }
    public Guid SessionId { get; set; }
    public DateTime LastHeartbeat { get; set; }

    public Client(int id, Socket socket)
    {
        Id = id;
        Socket = socket;
        IsConnected = true;
        SessionId = Guid.NewGuid();
        LastHeartbeat = DateTime.Now;
    }

    public void Close()
    {
        IsConnected = false;
        Socket.Shutdown(SocketShutdown.Both);
        Socket.Close();
    }
}

public class EventDrivenSocketServer
{
    private Socket serverSocket;
    private bool isRunning;
    private List<Client> clients = new List<Client>();
    private int clientIdCounter = 0;
    private Queue<int> availableIds = new Queue<int>();
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
        Console.WriteLine("Server started, waiting for connections...");
        StartAccept(null);

        // Start checking for client heartbeats in a background thread
        Thread heartbeatThread = new Thread(CheckClientHeartbeats);
        heartbeatThread.Start();
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
        ProcessAccept(e);
    }

    private void ProcessAccept(SocketAsyncEventArgs e)
    {
        Console.WriteLine("Client connected!");

        int clientId = availableIds.Count > 0 ? availableIds.Dequeue() : clientIdCounter++;
        Client client = new Client(clientId, e.AcceptSocket);
        client.ipAddress = e.AcceptSocket.RemoteEndPoint.ToString();

        clients.Add(client);
        Console.WriteLine($"Client {client.Id} added. With IP: {client.ipAddress}");

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

        if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
        {
            string receivedData = Encoding.ASCII.GetString(e.Buffer, e.Offset, e.BytesTransferred);
            Console.WriteLine($"Received from Client {client.Id}: {receivedData}");

            string response = ProcessRpcCommand(receivedData, client);

            StartSend(client, response);
        }
        else if (e.SocketError == SocketError.ConnectionReset || e.BytesTransferred == 0 || IsSocketDisconnected(client.Socket))
        {
            Console.WriteLine($"Client {client.Id} (Session {client.SessionId}) forcefully disconnected.");
            client.Close();
            clients.Remove(client);
            availableIds.Enqueue(client.Id); // Reuse ID
        }
        else
        {
            Console.WriteLine($"Error with Client {client.Id}: {e.SocketError}");
            client.Close();
            clients.Remove(client);
            availableIds.Enqueue(client.Id); // Reuse ID
        }
    }

    private bool IsSocketDisconnected(Socket socket)
    {
        try
        {
            return socket.Poll(1000, SelectMode.SelectRead) && socket.Available == 0;
        }
        catch (SocketException)
        {
            return true;
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
            Console.WriteLine($"Error sending data to Client {client.Id}: {e.SocketError}");
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
            foreach (var client in clients.ToArray())
            {
                if ((now - client.LastHeartbeat).TotalSeconds > heartbeatTimeout)
                {
                    Console.WriteLine($"Client {client.Id} did not send heartbeat, disconnecting.");
                    client.Close();
                    clients.Remove(client);
                }
            }
            Thread.Sleep(5000); // Check every 5 seconds
        }
    }

    public void Stop()
    {
        isRunning = false;

        foreach (var client in clients)
        {
            client.Close();
        }

        serverSocket.Close();
        Console.WriteLine("Server stopped.");
    }

    public bool IsRunning => isRunning;

    public void ShowClientStatus()
    {
        Console.WriteLine("Connected Clients:");
        foreach (var client in clients)
        {
            Console.WriteLine($"Client {client.Id}: Connected = {client.IsConnected}");
        }
    }
}

public class TcpServer
{
    public static void Main(string[] args)
    {
        EventDrivenSocketServer server = new EventDrivenSocketServer("0.0.0.0", 5000);
        server.Start();

        Console.WriteLine("Press ENTER to show connected clients or type 'stop' to stop the server...");
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
