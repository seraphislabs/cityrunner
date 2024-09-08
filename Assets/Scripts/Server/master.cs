using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

public class AsyncNetworkSocketServer
{
    private TcpListener server;
    private bool isRunning;

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
                TcpClient client = await server.AcceptTcpClientAsync();
                Console.WriteLine("Client connected!");

                // Handle the client connection asynchronously
                _ = HandleClientAsync(client); // Fire-and-forget, run client in parallel
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: {e.Message}");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using (NetworkStream stream = client.GetStream())
        {
            byte[] buffer = new byte[1024];

            try
            {
                while (client.Connected)
                {
                    // Read data asynchronously
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break; // Client has disconnected

                    // Convert the received data to a string
                    string message = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    Console.WriteLine($"Received: {message}");

                    // Echo the message back to the client
                    byte[] response = Encoding.ASCII.GetBytes("Echo: " + message);
                    await stream.WriteAsync(response, 0, response.Length);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error handling client: {e.Message}");
            }
            finally
            {
                // Close the client connection
                client.Close();
                Console.WriteLine("Client disconnected");
            }
        }
    }

    public void Stop()
    {
        isRunning = false;
        server.Stop();
        Console.WriteLine("Server stopped.");
    }
}

class TcpServer
{
    public static async Task Main(string[] args)
    {
        // Create and start the server
        AsyncNetworkSocketServer server = new AsyncNetworkSocketServer("0.0.0.0", 5000);
        _ = server.StartAsync(); // Fire-and-forget, runs server asynchronously

        // Wait for the user to stop the server
        Console.WriteLine("Press ENTER to stop the server...");
        Console.ReadLine();
        
        // Stop the server
        server.Stop();
    }
}