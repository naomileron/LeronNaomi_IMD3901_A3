using Unity.Netcode;
using UnityEngine;

public class SaveResults : NetworkBehaviour
{
    public static SaveResults Instance { get; private set; }

    public NetworkVariable<int> BlueScore = new(0);
    public NetworkVariable<int> GreenScore = new(0);
    public NetworkVariable<bool> HasResults = new(false);

    public NetworkVariable<int> GameMode = new((int)GameModeType.Competitive);
    public NetworkVariable<int> ScoreDisplayMode = new((int)ScoreMode.Individual);

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        Instance = this;
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SetResultsServer(int blue, int green, GameModeType gameMode, ScoreMode scoreMode)
    {
        if (!IsServer) return;

        BlueScore.Value = blue;
        GreenScore.Value = green;
        GameMode.Value = (int)gameMode;
        ScoreDisplayMode.Value = (int)scoreMode;
        HasResults.Value = true;
    }
}