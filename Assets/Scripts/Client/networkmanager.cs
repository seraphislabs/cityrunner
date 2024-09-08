using System;
using System.Collections.Generic;
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

    private float heartbeatInterval = 10f; // Send heartbeat every 5 seconds
    private float lastHeartbeatTime;

    // Dictionary to hold callbacks and their expiration time (timeout)
    private Dictionary<string, (Action<RpcResponse>, float)> responseCallbacks = new Dictionary<string, (Action<RpcResponse>, float)>();

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

            isRunning = true;

            // Set up the event args for receiving data
            receiveEventArgs = new SocketAsyncEventArgs();
            receiveEventArgs.SetBuffer(receiveBuffer, 0, receiveBuffer.Length);
            receiveEventArgs.Completed += OnReceiveCompleted;

            // Start receiving data from the server
            StartReceive();

            lastHeartbeatTime = Time.time;  // Initialize the last heartbeat time
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
                // Deserialize the JSON response into an RpcResponse object
                var rpcResponse = JsonConvert.DeserializeObject<RpcResponse>(receivedData);

                if (rpcResponse != null)
                {
                    // Handle the response based on the RequestId
                    if (rpcResponse.RequestId != null && responseCallbacks.ContainsKey(rpcResponse.RequestId))
                    {
                        // Invoke the callback and remove it from the dictionary
                        responseCallbacks[rpcResponse.RequestId].Item1?.Invoke(rpcResponse);
                        responseCallbacks.Remove(rpcResponse.RequestId);
                    }
                    else
                    {
                        // Invoke the general message received event
                        OnMessageReceived?.Invoke(rpcResponse);
                    }
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

    // Send an RPC to the server with a callback and timeout
    public void SendRpc(RpcRequest rpcRequest, Action<RpcResponse> callback = null, float timeout = 5f)
    {
        if (clientSocket == null || !clientSocket.Connected) return;

        try
        {
            // Generate a unique RequestId for this RPC
            rpcRequest.RequestId = Guid.NewGuid().ToString();

            // Serialize the RPC request to JSON using Newtonsoft.Json
            string jsonRpc = JsonConvert.SerializeObject(rpcRequest);
            byte[] data = Encoding.ASCII.GetBytes(jsonRpc);

            sendEventArgs = new SocketAsyncEventArgs();
            sendEventArgs.SetBuffer(data, 0, data.Length);
            sendEventArgs.Completed += OnSendCompleted;

            // If there's a callback, store it in the dictionary with a timeout
            if (callback != null)
            {
                responseCallbacks[rpcRequest.RequestId] = (callback, Time.time + timeout);
            }

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

    // Check for timeouts in the callbacks
    public void Tick()
    {
        List<string> expiredRequests = new List<string>();

        // Check for timeouts on requests
        foreach (var entry in responseCallbacks)
        {
            if (Time.time > entry.Value.Item2) // Timeout expired
            {
                Debug.LogWarning($"Request {entry.Key} timed out.");
                expiredRequests.Add(entry.Key);
            }
        }

        // Remove expired requests
        foreach (var requestId in expiredRequests)
        {
            responseCallbacks.Remove(requestId);
        }

        // Send heartbeats periodically
        if (Time.time - lastHeartbeatTime >= heartbeatInterval)
        {
            SendHeartbeat();
            lastHeartbeatTime = Time.time;  // Reset heartbeat time
        }
    }

    // Send a heartbeat message to the server
    private void SendHeartbeat()
    {
        var heartbeatRequest = new RpcRequest
        {
            Command = "heartbeat",
            RequestId = Guid.NewGuid().ToString(),
            Parameters = null
        };

        SendRpc(heartbeatRequest);
        Debug.Log("Sent heartbeat to server.");
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
