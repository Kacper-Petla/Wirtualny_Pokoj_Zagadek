using Unity.Netcode;
using UnityEngine;

public class NetworkBootstrap : MonoBehaviour
{
    private void OnGUI()
    {
        // Sprawdzamy obecnoœæ NetworkManagera, aby unikn¹æ NullReferenceException
        if (NetworkManager.Singleton == null) return;

        GUILayout.BeginArea(new Rect(10, 10, 220, 160));

        bool isRunning = NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer;

        if (!isRunning)
        {
            if (GUILayout.Button("Start Host (Server + Player)"))
            {
                NetworkManager.Singleton.StartHost();
            }
            if (GUILayout.Button("Start Server Only"))
            {
                NetworkManager.Singleton.StartServer();
            }
            if (GUILayout.Button("Start Client (Slave)"))
            {
                NetworkManager.Singleton.StartClient();
            }
        }
        else
        {
            string status = "Client";
            if (NetworkManager.Singleton.IsHost) status = "Host";
            else if (NetworkManager.Singleton.IsServer) status = "Server Only";

            GUILayout.Label($"Running as: {status}");

            if (GUILayout.Button("Shutdown"))
            {
                NetworkManager.Singleton.Shutdown();
            }
        }

        GUILayout.EndArea();
    }
}