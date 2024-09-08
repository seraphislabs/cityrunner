using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using Newtonsoft.Json;

public class NetworkSocketManager : MonoBehaviour
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

    // Send an RPC to the server
    public void SendRpc(RpcRequest rpcRequest)
    {
        if (client == null || !client.Connected) return;

        try
        {
            // Serialize the RPC request to JSON using Newtonsoft.Json
            string jsonRpc = JsonConvert.SerializeObject(rpcRequest);
            byte[] data = Encoding.ASCII.GetBytes(jsonRpc);
            stream.Write(data, 0, data.Length);
            Debug.Log($"Sent RPC: {jsonRpc}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending data: {e.Message}");
        }
    }

    // Continuously receive messages (RPC responses or other data) from the server
    public void StartReceiving(Action<RpcResponse> onMessageReceived)
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
                    if (stream.DataAvailable)
                    {
                        string message = Receive();
                        if (!string.IsNullOrEmpty(message))
                        {
                            // Deserialize the JSON response into an RpcResponse object using Newtonsoft.Json
                            var rpcResponse = JsonConvert.DeserializeObject<RpcResponse>(message);

                            // Invoke the callback with the deserialized response
                            onMessageReceived?.Invoke(rpcResponse);
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