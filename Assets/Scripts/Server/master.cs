using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

class TcpServer
{
    private const int Port = 5000; // Port number to listen on

    public static void Main()
    {
        TcpListener server = null;
        try
        {
            // Set the server IP address to listen on all interfaces (0.0.0.0)
            IPAddress localAddr = IPAddress.Any;

            // Create a TCP listener to listen on the specified port
            server = new TcpListener(localAddr, Port);

            // Start the server
            server.Start();
            Console.WriteLine($"Server started on port {Port}...");

            while (true)
            {
                // Accept an incoming client connection
                Console.WriteLine("Waiting for a connection...");
                TcpClient client = server.AcceptTcpClient();
                Console.WriteLine("Client connected!");

                // Handle the client connection in a separate function
                HandleClient(client);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error: {e.Message}");
        }
        finally
        {
            // Stop the server if necessary
            server?.Stop();
        }
    }

    private static void HandleClient(TcpClient client)
    {
        NetworkStream stream = null;
        try
        {
            // Get the network stream for reading and writing
            stream = client.GetStream();

            // Buffer for reading data
            byte[] buffer = new byte[1024];
            int bytesRead;

            // Read the data sent by the client
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0)
            {
                // Convert the received data to a string
                string message = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                Console.WriteLine($"Received: {message}");

                // Send a response back to the client (echo the message)s
                byte[] response = Encoding.ASCII.GetBytes("Echo: " + message);
                stream.Write(response, 0, response.Length);
                Console.WriteLine($"Sent: Echo: {message}");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error handling client: {e.Message}");
        }
        finally
        {
            // Close the connection
            stream?.Close();
            client.Close();
        }
    }
}