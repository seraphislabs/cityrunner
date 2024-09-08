using System;
using UnityEngine;
using UnityEngine.UIElements;

public class Lobby : MonoBehaviour
{
    private NetworkSocketManager networkSocketManager;
    private Label messageLabel; // Assuming you're using UI Toolkit and have a Label to display messages

    void Start()
    {
        // Ensure the MainThreadDispatcher is added to the scene
        if (GameObject.FindAnyObjectByType<MainThreadDispatcher>() == null)
        {
            GameObject dispatcherObj = new GameObject("MainThreadDispatcher");
            dispatcherObj.AddComponent<MainThreadDispatcher>();
        }

        // Initialize the network socket manager
        try
        {
            networkSocketManager = new NetworkSocketManager("67.205.148.170", 5000);

            // Subscribe to the OnMessageReceived event
            networkSocketManager.OnMessageReceived += OnMessageReceived;

            // Example: Send an RPC request when the game starts with a timeout and callback
            var rpcRequest = new RpcRequest
            {
                Command = "greet",
                Parameters = new { auth = false }
            };

            networkSocketManager.SendRpc(rpcRequest, response =>
            {
                if (!string.IsNullOrEmpty(response.Error))
                {
                    Debug.Log($"Greet RPC Error: {response.Error}");
                    if (messageLabel != null)
                    {
                        messageLabel.text = $"Greet RPC Error: {response.Error}";
                    }
                }
                else
                {
                    Debug.Log($"Greet RPC Result: {response.Result}");
                    if (messageLabel != null)
                    {
                        messageLabel.text = $"Server Response: {response.Result}";
                    }
                }
            }, 10f);  // Timeout of 10 seconds
        }
        catch (Exception e)
        {
            Debug.LogError($"Error connecting to server: {e.Message}");
            if (messageLabel != null)
            {
                messageLabel.text = "Failed to connect to server.";
            }
        }
    }

    // This will be called whenever a message is received from the server
    void OnMessageReceived(RpcResponse response)
    {
        Debug.Log("Received RPC response from server.");

        /*// Schedule the UI update to happen on the main thread
        MainThreadDispatcher.Enqueue(() =>
        {
            if (!string.IsNullOrEmpty(response.Error))
            {
                Debug.Log($"RPC Error: {response.Error}");
                if (messageLabel != null)
                {
                    messageLabel.text = $"RPC Error: {response.Error}";
                }
            }
            else
            {
                Debug.Log($"RPC Result: {response.Result}");
                if (messageLabel != null)
                {
                    messageLabel.text = $"Server Response: {response.Result}";
                }
            }
        });*/
    }

    void Update()
    {
        if (networkSocketManager != null)
        {
            // Call the Tick() method on the network manager to handle updates like heartbeats
            networkSocketManager.Tick();
        }

        // Optional: You can send more messages to the server here (e.g., when pressing the space key)
        if (Input.GetKeyDown(KeyCode.Space))
        {
            var rpcRequest = new RpcRequest
            {
                Command = "add",
                Parameters = new { a = 5, b = 10 }
            };

            networkSocketManager.SendRpc(rpcRequest, response =>
            {
                if (!string.IsNullOrEmpty(response.Error))
                {
                    Debug.Log($"Add RPC Error: {response.Error}");
                }
                else
                {
                    Debug.Log($"Add RPC Result: {response.Result}");
                }
            }, 5f);  // Timeout of 5 seconds
        }
    }

    private void OnApplicationQuit()
    {
        // Stop the network manager when the application quits
        if (networkSocketManager != null)
        {
            networkSocketManager.CloseConnection();
        }
    }
}
