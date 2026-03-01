using Unity.Netcode;
using UnityEngine;

public class AnnouncementSystem : NetworkBehaviour
{
    private static AnnouncementSystem instance;

    [SerializeField] private AudioSource source;

    //arrays of audio clips for each hospital
    [SerializeField] private AudioClip[] hOneRoomClips = new AudioClip[6];
    [SerializeField] private AudioClip[] hTwoRoomClips = new AudioClip[6];

    // Reference to the player's hospital info
    private PlayerHospital localPlayerHospital;

    //temp for testing sanity
    //[SerializeField] private bool muteIfNotFocused = true;

    private void Awake()
    {
        // Prevent duplicates
        if (instance != null && instance != this)
        {
            Debug.LogWarning("[AnnouncementSystem] Duplicate found -> destroying this one.");
            Destroy(gameObject);
            return;
        }
        instance = this;

        if (source == null)
        {
            source = GetComponent<AudioSource>();
        }
    }

    private void Start()
    {
        CacheLocalPlayer();
    }

    //Find the player's PlayerHospital component. The client only cares about their own player
    private void CacheLocalPlayer()
    {
        if (NetworkManager.Singleton == null)
        {
            return;
        }

        var localPlayer = NetworkManager.Singleton.LocalClient?.PlayerObject;

        if (localPlayer != null)
        {
            localPlayerHospital = localPlayer.GetComponent<PlayerHospital>();
        }
    }

    //Called by server. Plays the announcement for hospital X, Room Y
    [ClientRpc]
    public void PlayRoomAnnouncementClientRpc(HospitalType hospital, int roomNumber, ClientRpcParams rpcParams = default)
    {
        Debug.Log($"[ClientRpc] Received announcement: {hospital} Room {roomNumber}");

        //Know which hospital the player belongs to
        if (localPlayerHospital == null)
        {
            CacheLocalPlayer();
        }

        if (localPlayerHospital == null)
        {
            Debug.LogWarning("[AnnouncementSystem] localPlayerHospital is STILL null (can’t check hospital yet).");
            return;
        }
        else
        {
            Debug.Log($"[AnnouncementSystem] Local player hospital = {localPlayerHospital.Hospital.Value}");
        }

        Debug.Log($"[AnnouncementSystem] Comparing local={localPlayerHospital.Hospital.Value} vs announcement={hospital}");

        //Only play the sound if it matches that player's hospital (prevents other players from hearing other hospital's announcements)
        if (localPlayerHospital.Hospital.Value != hospital)
        {
            Debug.Log("[AnnouncementSystem] Not this player's hospital — ignoring announcement.");
            return;
        }

        // Convert room number to array index (starts at 0)
        int index = roomNumber - 1;

        if (index < 0 || index >= 6)
        {
            Debug.LogWarning("[AnnouncementSystem] Room index out of range.");
            return;
        }

        Debug.Log($"[AnnouncementSystem] LocalClientId={NetworkManager.Singleton.LocalClientId}, Focused={Application.isFocused}");

        //Select the desired hospital audio array
        AudioClip clip =
            (hospital == HospitalType.Blue)
            ? hOneRoomClips[index]
            : hTwoRoomClips[index];

        Debug.Log($"[AnnouncementSystem] Using {(hospital == HospitalType.Blue ? "hOneRoomClips (Hospital One)" : "hTwoRoomClips (Hospital Two)")}");

        Debug.Log($"[AnnouncementSystem] Selected clip = {(clip != null ? clip.name : "NULL")}");

        //if clip does not exist, return
        if (clip == null)
        {
            return;
        }

        Debug.Log($"[AnnouncementSystem] Playing clip. sourceNull={source == null} volume={(source != null ? source.volume : 0f)}");

        //if (muteIfNotFocused && !Application.isFocused)
        //return;

        StartCoroutine(PlayAnnouncementNextFrame(clip));
    }

    private System.Collections.IEnumerator PlayAnnouncementNextFrame(AudioClip clip)
    {
        yield return null; // wait 1 frame
        source.PlayOneShot(clip);
    }
}