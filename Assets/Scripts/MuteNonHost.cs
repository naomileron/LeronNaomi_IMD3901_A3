using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class MuteNonHostAudio : MonoBehaviour
{
    [SerializeField] private bool hostOnlyAudio = true;

    private IEnumerator Start()
    {
        if (!hostOnlyAudio) yield break;

        // Wait until NetworkManager exists AND the network session has started
        while (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            yield return null;

        bool isHost = NetworkManager.Singleton.IsHost;

        // Host audible, client silent
        AudioListener.pause = !isHost;
        AudioListener.volume = isHost ? 1f : 0f;

        //Debug.Log($"[MuteNonHostAudio] IsListening={NetworkManager.Singleton.IsListening} IsHost={isHost} -> pause={AudioListener.pause}, volume={AudioListener.volume}");
    }
}