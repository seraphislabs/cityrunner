using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class lobby : MonoBehaviour
{
    private NetworkSocketManager networkSocketManager;

    void Start()
    {
        // Initialize the network socket manager
        try {
        networkSocketManager = new NetworkSocketManager("67.205.148.170", 5000);

        // Start receiving messages
        networkSocketManager.StartReceiving(OnMessageReceived);

        }
        catch (Exception e)
        {
            //Debug.LogError($"Error connecting to server: {e.Message}");
        }
    }

    // This will be called whenever a message is received from the server
    void OnMessageReceived(string message)
    {
        // Unity APIs can only be accessed from the main thread
        // Use Unity's main thread to process the message
        Debug.Log("Message from server: " + message);
        // You can use Unity's API here, like updating the UI or interacting with game objects
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
        networkSocketManager.Stop();
    }
}