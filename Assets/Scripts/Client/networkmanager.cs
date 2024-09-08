using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class NetworkSocketManager
{
    private string ServerIp;
    private int Port;
    
    private TcpClient client;
    private NetworkStream stream;
    private bool isRunning = false;

    public NetworkSocketManager(string serverIp, int port)
    {
        ServerIp = serverIp;
        Port = port;

        client = new TcpClient(ServerIp, Port);
        stream = client.GetStream();
    }

    // Send a message to the server
    public void Send(string message)
    {
        byte[] data = Encoding.ASCII.GetBytes(message);
        stream.Write(data, 0, data.Length);
    }

    // Continuously check for messages from the server
    public void StartReceiving(Action<string> onMessageReceived)
    {
        isRunning = true;

        // Start a new thread for receiving messages
        new Thread(() =>
        {
            while (isRunning)
            {
                try
                {
                    if (stream.DataAvailable) // Check if there is data available
                    {
                        string message = Receive();
                        onMessageReceived?.Invoke(message); // Send the message to the callback
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error receiving data: {e.Message}");
                    Stop();
                }

                Thread.Sleep(10); // Small delay to prevent tight loop
            }
        }).Start();
    }

    // Receive a message from the server
    private string Receive()
    {
        byte[] buffer = new byte[1024];
        int bytesRead = stream.Read(buffer, 0, buffer.Length);
        return Encoding.ASCII.GetString(buffer, 0, bytesRead);
    }

    // Stop receiving messages and close the connection
    public void Stop()
    {
        isRunning = false;
        stream?.Close();
        client?.Close();
    }
}