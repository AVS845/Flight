using UnityEngine;
using Unity.Netcode;
using System.Collections;
using UnityEngine.Rendering;
using Unity.Netcode.Transports.UTP;

public class NetworkAutostart : MonoBehaviour
{
    [SerializeField] private string hostLanIpAddress = "192.168.1.133";
    [SerializeField] private ushort port = 7777;

    IEnumerator Start()
    {
        bool isHeadless = Application.isBatchMode 
    || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;

        yield return new WaitForEndOfFrame();
        if (isHeadless) StartLanServer();
        else JoinLanHost();
    }

    public void StartLanServer()
    {
        if (NetworkManager.Singleton.NetworkConfig.NetworkTransport is UnityTransport transport)
        {
            transport.SetConnectionData("0.0.0.0", port);
            
            NetworkManager.Singleton.StartServer();
        }
    }
    public void JoinLanHost()
    {
        if (NetworkManager.Singleton.NetworkConfig.NetworkTransport is UnityTransport transport)
        {
            transport.SetConnectionData(hostLanIpAddress, port);
            
            NetworkManager.Singleton.StartClient();
            Debug.Log($"Connecting to LAN Host at {hostLanIpAddress}:{port}...");
        }
    }
}
