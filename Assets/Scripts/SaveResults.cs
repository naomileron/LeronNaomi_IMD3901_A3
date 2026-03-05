using Unity.Netcode;
using UnityEngine;

public class SaveResults : NetworkBehaviour
{
    public static SaveResults Instance { get; private set; }

    public NetworkVariable<int> BlueScore = new(0);
    public NetworkVariable<int> GreenScore = new(0);
    public NetworkVariable<bool> HasResults = new(false);

    private void Awake()
    {
        // Persist across scene loads
        DontDestroyOnLoad(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        Instance = this;
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // Server writes once at end of match
    public void SetResultsServer(int blue, int green)
    {
        if (!IsServer)
        {
            return;
        }

        BlueScore.Value = blue;
        GreenScore.Value = green;
        HasResults.Value = true;
    }
}