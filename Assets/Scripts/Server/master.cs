using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;

public class Client
{
    public int Id { get; set; }
    public TcpClient TcpClient { get; set; }
    public NetworkStream Stream { get; set; }
    public bool IsConnected { get; set; }

    public Client(int id, TcpClient tcpClient)
    {
        Id = id;
        TcpClient = tcpClient;
        Stream = tcpClient.GetStream();
        IsConnected = true;
    }

    public void Close()
    {
        IsConnected = false;
        Stream.Close();
        TcpClient.Close();
    }
}

public class AsyncNetworkSocketServer
{
    private TcpListener server;
    private bool isRunning;
    private List<Client> clients = new List<Client>(); // List to store connected clients
    private int clientIdCounter = 0; // To assign unique IDs to each client

    public AsyncNetworkSocketServer(string ipAddress, int port)
    {
        // Initialize the TCP listener with the given IP address and port
        server = new TcpListener(IPAddress.Parse(ipAddress), port);
    }

    public async Task StartAsync()
    {
        isRunning = true;
        server.Start();
        Console.WriteLine("Server started, waiting for connections...");

        // Continuously accept incoming connections asynchronously
        while (isRunning)
        {
            try
            {
                // Accept a new client connection asynchronously
                TcpClient tcpClient = await server.AcceptTcpClientAsync();
                Console.WriteLine("Client connected!");

                // Assign a unique ID to the client and add it to the client list
                Client client = new Client(clientIdCounter++, tcpClient);
                clients.Add(client);
                Console.WriteLine($"Client {client.Id} added. With IP: {tcpClient.Client.RemoteEndPoint}");

                // Handle the client connection asynchronously
                _ = HandleClientAsync(client);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: {e.Message}");
            }
        }
    }

    private async Task HandleClientAsync(Client client)
    {
        byte[] buffer = new byte[1024];

        try
        {
            while (client.IsConnected)
            {
                // Check for incoming data
                int bytesRead = await client.Stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    Console.WriteLine($"Client {client.Id} disconnected.");
                    break; // Client has disconnected
                }

                // Parse the incoming message (assuming JSON format)
                string receivedData = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                Console.WriteLine($"Received from Client {client.Id}: {receivedData}");

                // Process the command and get the response
                string response = ProcessRpcCommand(receivedData);

                // Send response back to the client
                byte[] responseData = Encoding.ASCII.GetBytes(response);
                await client.Stream.WriteAsync(responseData, 0, responseData.Length);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error handling client {client.Id}: {e.Message}");
        }
        finally
        {
            // Close the client connection and remove from list
            client.Close();
            clients.Remove(client);
            Console.WriteLine($"Client {client.Id} removed.");
        }
    }

    private string ProcessRpcCommand(string jsonData)
    {
        try
        {
            // Deserialize the incoming JSON data into an RpcRequest object
            var rpcRequest = JsonSerializer.Deserialize<RpcRequest>(jsonData);

            switch (rpcRequest.Command.ToLower())
            {
                case "add":
                    // Get the parameters for the "add" command
                    var addParams = JsonSerializer.Deserialize<Dictionary<string, int>>(rpcRequest.Parameters.ToString());
                    int a = addParams["a"];
                    int b = addParams["b"];
                    int sum = a + b;
                    return JsonSerializer.Serialize(new { result = sum });

                case "greet":
                    // Get the name for the "greet" command
                    var greetParams = JsonSerializer.Deserialize<Dictionary<string, string>>(rpcRequest.Parameters.ToString());
                    string name = greetParams["name"];
                    return JsonSerializer.Serialize(new { message = $"Hello, {name}!" });

                default:
                    return JsonSerializer.Serialize(new { error = "Unknown command" });
            }
        }
        catch (Exception e)
        {
            return JsonSerializer.Serialize(new { error = "Invalid request", details = e.Message });
        }
    }

    // Optional: Method to show the status of all connected clients
    public void ShowClientStatus()
    {
        Console.WriteLine("Connected Clients:");
        foreach (var client in clients)
        {
            Console.WriteLine($"Client {client.Id}: Connected = {client.IsConnected}");
        }
    }

    public void Stop()
    {
        isRunning = false;
        foreach (var client in clients)
        {
            client.Close(); // Close all client connections when stopping the server
        }
        server.Stop();
        Console.WriteLine("Server stopped.");
    }
}

// Program entry point
public class TcpServer
{
    public static async Task Main(string[] args)
    {
        // Create and start the server
        AsyncNetworkSocketServer server = new AsyncNetworkSocketServer("0.0.0.0", 5000);
        await server.StartAsync(); // Await the server's asynchronous start

        // Wait for the user to stop the server or display client status
        Console.WriteLine("Press ENTER to show connected clients or type 'stop' to stop the server...");
        string input;
        while ((input = await Console.In.ReadLineAsync()) != "stop") // Use ReadLineAsync to avoid blocking
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
