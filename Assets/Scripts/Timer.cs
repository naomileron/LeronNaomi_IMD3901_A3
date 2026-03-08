using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Timer : NetworkBehaviour
{
    public static Timer Instance {  get; private set; }

    [SerializeField] private float matchDurationSecs = 120.0f;

    [SerializeField] private string resultsScene = "Results";
    [SerializeField] private SaveResults savedResultsPrefab;

    private NetworkVariable<bool> running = new(false);
    private NetworkVariable<double> startTimeServer = new(0);
    private NetworkVariable<bool> ended = new(false);

    public override void OnNetworkSpawn()
    {
        Instance = this;
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this) Instance = null;
    }

    public float GetRemainingSecs()
    {
        if (!running.Value)
        {
            return matchDurationSecs;
        }

        double now = NetworkManager.Singleton.ServerTime.Time;
        double elapsed = now - startTimeServer.Value;

        return matchDurationSecs - (float)elapsed;
    }

    public void StartMatchTimerServer()
    {
        if (!IsServer)
        {
            return;
        }
        if (running.Value)
        {
            return;
        }

        running.Value = true;
        startTimeServer.Value = NetworkManager.Singleton.ServerTime.Time;
    }

    private void Update()
    {
        if (!IsServer)
        {
            return;
        }
        if (!running.Value)
        {
            return;
        }
        if (ended.Value)
        {
            return;
        }
        if (GetRemainingSecs() <= 0.0f)
        {
            EndMatchServer();
        }
    }

    private void EndMatchServer()
    {
        if (!IsServer) return;
        if (ended.Value) return;

        ended.Value = true;
        running.Value = false;

        int blueScore = 0;
        int greenScore = 0;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var playerObj = client.PlayerObject;
            if (playerObj == null) continue;

            var ph = playerObj.GetComponent<PlayerHospital>();
            var ss = playerObj.GetComponent<ScoreSystem>();
            if (ph == null || ss == null) continue;

            if (ph.Hospital.Value == HospitalType.Blue) blueScore = ss.Score.Value;
            if (ph.Hospital.Value == HospitalType.Green) greenScore = ss.Score.Value;
        }

        var modeConfig = FindFirstObjectByType<ConfigureGameMode>();

        GameModeType gameMode = GameModeType.Competitive;
        ScoreMode scoreMode = ScoreMode.Individual;

        if (modeConfig != null)
        {
            gameMode = modeConfig.gameMode;
            scoreMode = modeConfig.scoreMode;
        }

        var existing = SaveResults.Instance;
        if (existing == null)
        {
            if (savedResultsPrefab == null)
            {
                //Debug.LogError("[Timer] savedResultsPrefab is NOT assigned in inspector.");
                return;
            }

            var state = Instantiate(savedResultsPrefab);
            state.NetworkObject.Spawn(true);
            existing = state;
        }

        existing.SetResultsServer(blueScore, greenScore, gameMode, scoreMode);

        //load results scene for all players
        NetworkManager.SceneManager.LoadScene(resultsScene, LoadSceneMode.Single);
    }
}

