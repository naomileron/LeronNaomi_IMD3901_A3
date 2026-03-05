using UnityEngine;
using Unity.Netcode;

public class Timer : NetworkBehaviour
{
    public static Timer Instance {  get; private set; }

    [SerializeField] private float matchDurationSecs = 120.0f;

    private NetworkVariable<bool> running = new(false);
    private NetworkVariable<double> startTimeServer = new(0);

    //private NetworkVariable<bool> finished = new(false);

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

}