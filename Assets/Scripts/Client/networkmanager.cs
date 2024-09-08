using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using Newtonsoft.Json;

public class NetworkSocketManager
{
    private string ServerIp;
    private int Port;

    private Socket clientSocket;
    private bool isRunning;

    private byte[] receiveBuffer = new byte[1024]; // Buffer for receiving data
    private SocketAsyncEventArgs sendEventArgs;    // Event args for sending data
    private SocketAsyncEventArgs receiveEventArgs; // Event args for receiving data

    public event Action<RpcResponse> OnMessageReceived; // Event for handling received messages

    // Constructor to initialize the NetworkSocketManager with server IP and port
    public NetworkSocketManager(string serverIp, int port)
    {
        ServerIp = serverIp;
        Port = port;

        try
        {
            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            clientSocket.Connect(new IPEndPoint(IPAddress.Parse(ServerIp), Port));
            Debug.Log("Connected to the server successfully.");

            // Set up the event args for receiving data
            receiveEventArgs = new SocketAsyncEventArgs();
            receiveEventArgs.SetBuffer(receiveBuffer, 0, receiveBuffer.Length);
            receiveEventArgs.Completed += OnReceiveCompleted;

            // Start receiving data from the server
            StartReceive();
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to connect to the server: {e.Message}");
        }
    }

    // Start receiving data from the server
    private void StartReceive()
    {
        if (clientSocket == null || !clientSocket.Connected)
        {
            Debug.LogError("Socket is not connected.");
            return;
        }

        bool willRaiseEvent = clientSocket.ReceiveAsync(receiveEventArgs);
        if (!willRaiseEvent)
        {
            // Process the receive synchronously if no event is raised
            ProcessReceive(receiveEventArgs);
        }
    }

    // Callback when data is received
    private void OnReceiveCompleted(object sender, SocketAsyncEventArgs e)
    {
        ProcessReceive(e);
    }

    // Process received data
    private void ProcessReceive(SocketAsyncEventArgs e)
    {
        if (e.SocketError == SocketError.Success && e.BytesTransferred > 0)
        {
            string receivedData = Encoding.ASCII.GetString(e.Buffer, e.Offset, e.BytesTransferred);
            Debug.Log($"Received from server: {receivedData}");

            try
            {
                // Deserialize the JSON response into an RpcResponse object using Newtonsoft.Json
                var rpcResponse = JsonConvert.DeserializeObject<RpcResponse>(receivedData);

                if (rpcResponse != null)
                {
                    // Invoke the OnMessageReceived event with the RpcResponse
                    OnMessageReceived?.Invoke(rpcResponse);
                }
                else
                {
                    Debug.LogError("Received empty or invalid RPC response.");
                }
            }
            catch (JsonException ex)
            {
                Debug.LogError($"Error deserializing RPC response: {ex.Message}");
            }

            // Continue receiving data
            StartReceive();
        }
        else
        {
            Debug.LogError("Server disconnected or error occurred.");
            CloseConnection();
        }
    }

    // Send an RPC to the server
    public void SendRpc(RpcRequest rpcRequest)
    {
        if (clientSocket == null || !clientSocket.Connected) return;

        try
        {
            // Serialize the RPC request to JSON using Newtonsoft.Json
            string jsonRpc = JsonConvert.SerializeObject(rpcRequest);
            byte[] data = Encoding.ASCII.GetBytes(jsonRpc);

            sendEventArgs = new SocketAsyncEventArgs();
            sendEventArgs.SetBuffer(data, 0, data.Length);
            sendEventArgs.Completed += OnSendCompleted;

            bool willRaiseEvent = clientSocket.SendAsync(sendEventArgs);
            if (!willRaiseEvent)
            {
                // Process the send synchronously if no event is raised
                ProcessSend(sendEventArgs);
            }

            Debug.Log($"Sent RPC: {jsonRpc}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending data: {e.Message}");
        }
    }

    // Callback when data is sent
    private void OnSendCompleted(object sender, SocketAsyncEventArgs e)
    {
        ProcessSend(e);
    }

    // Process sent data
    private void ProcessSend(SocketAsyncEventArgs e)
    {
        if (e.SocketError == SocketError.Success)
        {
            Debug.Log("Data sent successfully.");
        }
        else
        {
            Debug.LogError("Error sending data.");
            CloseConnection();
        }
    }

    // Close the connection and clean up resources
    public void CloseConnection()
    {
        isRunning = false;

        if (clientSocket != null)
        {
            clientSocket.Shutdown(SocketShutdown.Both);
            clientSocket.Close();
            clientSocket = null;
        }

        Debug.Log("Connection closed.");
    }
}
