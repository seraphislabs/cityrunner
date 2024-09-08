using System;
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
    private bool isRunning;

    public NetworkSocketManager(string serverIp, int port)
    {
        ServerIp = serverIp;
        Port = port;
        
        try
        {
            client = new TcpClient(ServerIp, Port);
            stream = client.GetStream();
            Debug.Log("Connected to the server successfully.");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to connect to the server: {e.Message}");
        }
    }

    // Send a message to the server
    public void Send(string message)
    {
        if (client == null || !client.Connected) return;

        try
        {
            byte[] data = Encoding.ASCII.GetBytes(message);
            stream.Write(data, 0, data.Length);
            Debug.Log($"Sent: {message}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending data: {e.Message}");
        }
    }

    // Continuously receive messages from the server
    public void StartReceiving(Action<string> onMessageReceived)
    {
        if (client == null || !client.Connected)
        {
            Debug.LogError("Client is not connected.");
            return;
        }

        isRunning = true;

        new Thread(() =>
        {
            while (isRunning)
            {
                try
                {
                    if (stream.DataAvailable) // Check if there is data available
                    {
                        string message = Receive();
                        if (!string.IsNullOrEmpty(message))
                        {
                            onMessageReceived?.Invoke(message); // Call the callback with the received message
                        }
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
        if (client == null || !client.Connected) return null;

        try
        {
            byte[] buffer = new byte[1024];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            return Encoding.ASCII.GetString(buffer, 0, bytesRead);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error during message reception: {e.Message}");
            Stop();
            return null;
        }
    }

    // Stop receiving messages and close the connection
    public void Stop()
    {
        isRunning = false;

        if (stream != null)
        {
            stream.Close();
            stream = null;
        }

        if (client != null)
        {
            client.Close();
            client = null;
        }

        Debug.Log("Connection closed.");
    }
}
