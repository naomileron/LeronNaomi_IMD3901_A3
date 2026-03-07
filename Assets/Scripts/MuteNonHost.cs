using System.Collections;
using Unity.Netcode;
using UnityEngine;

//for testing purposes
public class MuteNonHostAudio : MonoBehaviour
{
    [SerializeField] private bool hostOnlyAudio = true;

    private IEnumerator Start()
    {
        if (!hostOnlyAudio) yield break;

        while (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            yield return null;

        bool isHost = NetworkManager.Singleton.IsHost;

        AudioListener.pause = !isHost;
        AudioListener.volume = isHost ? 1f : 0f;

    }
}