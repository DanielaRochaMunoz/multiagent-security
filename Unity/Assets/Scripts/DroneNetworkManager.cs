using UnityEngine;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections;
using System.Collections.Concurrent;

public class DroneNetworkClient : MonoBehaviour
{
    private TcpClient tcpClient;
    private NetworkStream networkStream;
    private Thread clientThread;
    private bool isRunning = false;
    private ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();
    private readonly object lockObject = new object();
    
    [Header("Network Settings")]
    public string serverIP = "127.0.0.1";
    public int serverPort = 5555;
    public float reconnectDelay = 5f;
    
    private DroneController droneController;

    void Start()
    {
        droneController = GetComponent<DroneController>();
        if (droneController == null)
        {
            Debug.LogError("DroneController component not found!");
            return;
        }
        
        ConnectToServer();
    }

    void ConnectToServer()
    {
        try
        {
            tcpClient = new TcpClient();
            tcpClient.Connect(serverIP, serverPort);
            networkStream = tcpClient.GetStream();
            
            // Send identification message
            string identMessage = "{\"client_type\": \"unity_drone\"}";
            byte[] identData = Encoding.UTF8.GetBytes(identMessage);
            networkStream.Write(identData, 0, identData.Length);
            
            isRunning = true;
            clientThread = new Thread(ReceiveData);
            clientThread.IsBackground = true;
            clientThread.Start();
            
            Debug.Log("Connected to server successfully!");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to connect to server: {e.Message}");
            StartCoroutine(ReconnectCoroutine());
        }
    }

    IEnumerator ReconnectCoroutine()
    {
        Debug.Log($"Attempting to reconnect in {reconnectDelay} seconds...");
        yield return new WaitForSeconds(reconnectDelay);
        ConnectToServer();
    }

    void ReceiveData()
    {
        byte[] buffer = new byte[4096];
        
        while (isRunning && tcpClient != null && tcpClient.Connected)
        {
            try
            {
                int bytesRead = networkStream.Read(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    messageQueue.Enqueue(message);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error receiving data: {e.Message}");
                break;
            }
        }
        
        // If we break out of the loop, connection is lost
        isRunning = false;
        Debug.Log("Connection to server lost.");
    }

    void Update()
    {
        // Process any received messages
        while (messageQueue.TryDequeue(out string message))
        {
            ProcessMessage(message);
        }
    }

    void ProcessMessage(string message)
    {
        try
        {
            Debug.Log($"Received message: {message}");
            // Forward message to DroneController for processing
            if (droneController != null)
            {
                droneController.ProcessReceivedMessage(message);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error processing message: {e.Message}");
        }
    }

    void OnDestroy()
    {
        isRunning = false;
        
        if (networkStream != null)
        {
            networkStream.Close();
            networkStream = null;
        }
        
        if (tcpClient != null)
        {
            tcpClient.Close();
            tcpClient = null;
        }
        
        if (clientThread != null && clientThread.IsAlive)
        {
            clientThread.Join(1000); // Wait up to 1 second for thread to finish
            clientThread = null;
        }
    }
}