using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
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
    public Guid SessionId { get; set; }  // Add a unique session identifier

    public Client(int id, Socket socket)
    {
        Id = id;
        Socket = socket;
        IsConnected = true;
        SessionId = Guid.NewGuid();  // Generate a new session ID
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
    private List<Client> clients = new List<Client>(); // List to store connected clients
    private int clientIdCounter = 0; // To assign unique IDs to each client
    private Queue<int> availableIds = new Queue<int>(); // To reuse IDs of disconnected clients

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

        // Start accepting clients asynchronously
        StartAccept(null);
    }

    private void StartAccept(SocketAsyncEventArgs acceptEventArg)
    {
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

        // Assign a unique ID to the client from the available IDs or create a new one
        int clientId = availableIds.Count > 0 ? availableIds.Dequeue() : clientIdCounter++;
        Client client = new Client(clientId, e.AcceptSocket);
        client.ipAddress = e.AcceptSocket.RemoteEndPoint.ToString();

        clients.Add(client);
        Console.WriteLine($"Client {client.Id} added. With IP: {client.ipAddress}");

        // Start receiving data from the client
        StartReceive(client);

        // Accept the next client
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
        Client client = e.UserToken as Client; // Retrieve the client from UserToken

        if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
        {
            string receivedData = Encoding.ASCII.GetString(e.Buffer, e.Offset, e.BytesTransferred);
            Console.WriteLine($"Received from Client {client.Id}: {receivedData}");

            // Process the command and get the response, passing the client as context
            string response = ProcessRpcCommand(receivedData, client);

            // Send response back to the client
            StartSend(client, response);
        }
        else if (e.SocketError == SocketError.ConnectionReset || e.BytesTransferred == 0)
        {
            // Handle disconnection
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
            // Start receiving data from the client again
            StartReceive(client);
        }
        else
        {
            Console.WriteLine($"Error sending data to Client {client.Id}: {e.SocketError}");
        }
    }

    private string ProcessRpcCommand(string jsonData, Client client)
    {
        try
        {
            // Parse the incoming JSON to a JObject for dynamic handling
            JObject jsonRequest = JObject.Parse(jsonData);

            // Extract the "RequestId" field
            string requestId = jsonRequest["RequestId"]?.ToString();

            // Extract the "Command" field
            string command = jsonRequest["Command"]?.ToString();

            // Check if the command is "greet"
            if (command == "greet")
            {
                // Extract the parameters (assuming there is an "auth" field in the parameters)
                string auth = jsonRequest["Parameters"]?["auth"]?.ToString();

                if (!string.IsNullOrEmpty(auth))
                {
                    // Create the RpcResponse and include client information like IP, ID, and SessionId
                    RpcResponse response = new RpcResponse
                    {
                        Result = auth == "false" ? "false" : "true",
                        Error = null,  // No error
                        RequestId = requestId,  // Include the RequestId in the response
                        Parameters = new
                        {
                            ClientId = client.Id,
                            IpAddress = client.ipAddress,
                            SessionId = client.SessionId
                        }
                    };

                    // Serialize the RpcResponse back to JSON
                    return JsonConvert.SerializeObject(response);
                }
                else
                {
                    // If the auth parameter is missing or empty, return an error response
                    RpcResponse errorResponse = new RpcResponse
                    {
                        Result = null,
                        Error = "Missing 'auth' parameter in 'greet' command",
                        RequestId = requestId,  // Include the RequestId in the error response
                        Parameters = null
                    };

                    return JsonConvert.SerializeObject(errorResponse);
                }
            }
            else
            {
                // Handle unknown commands with an error response
                RpcResponse unknownCommandResponse = new RpcResponse
                {
                    Result = null,
                    Error = $"Unknown command: {command}",
                    RequestId = requestId,  // Include the RequestId in the error response
                    Parameters = null
                };

                return JsonConvert.SerializeObject(unknownCommandResponse);
            }
        }
        catch (JsonException ex)
        {
            // Handle JSON parsing errors and return an error response
            RpcResponse errorResponse = new RpcResponse
            {
                Result = null,
                Error = $"Invalid JSON format: {ex.Message}",
                RequestId = null,  // No valid RequestId if JSON is invalid
                Parameters = null
            };

            return JsonConvert.SerializeObject(errorResponse);
        }
    }

    public void Stop()
    {
        isRunning = false;
        foreach (var client in clients)
        {
            client.Close(); // Close all client connections when stopping the server
        }
        serverSocket.Close();
        Console.WriteLine("Server stopped.");
    }

    public void ShowClientStatus()
    {
        Console.WriteLine("Connected Clients:");
        foreach (var client in clients)
        {
            Console.WriteLine($"Client {client.Id}: Connected = {client.IsConnected}");
        }
    }
}

// Program entry point
public class TcpServer
{
    public static void Main(string[] args)
    {
        EventDrivenSocketServer server = new EventDrivenSocketServer("0.0.0.0", 5000);
        server.Start();

        Console.WriteLine("Press ENTER to show connected clients or type 'stop' to stop the server...");
        string input;
        while ((input = Console.ReadLine()) != "stop")
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
