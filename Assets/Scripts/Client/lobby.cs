using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class Lobby : MonoBehaviour
{
    private NetworkSocketManager networkSocketManager;
    private Label messageLabel; // Assuming you're using UI Toolkit and have a Label to display messages

    void Start()
    {
        // Initialize the UI element (messageLabel) from the UI Document

        // Initialize the network socket manager
        try
        {
            networkSocketManager = new NetworkSocketManager("67.205.148.170", 5000);

            // Start receiving messages
            networkSocketManager.StartReceiving(OnMessageReceived);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error connecting to server: {e.Message}");
            messageLabel.text = "Failed to connect to server.";
        }
    }

    // This will be called whenever a message is received from the server
    void OnMessageReceived(string message)
    {
        // Unity APIs can only be accessed from the main thread
        // Use Unity's main thread to process the message
        Debug.Log("Message from server: " + message);
    }

    void Update()
    {
        // Optional: You can send messages to the server here
        if (Input.GetKeyDown(KeyCode.Space))
        {
            networkSocketManager.Send("Hello Server!");
        }
    }

    private void OnApplicationQuit()
    {
        // Stop the network manager when the application quits
        if (networkSocketManager != null)
        {
            networkSocketManager.Stop();
        }
    }
}