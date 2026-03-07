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

    private void Awake()
    {
        // Prevent duplicates
        if (instance != null && instance != this)
        {
            //Debug.LogWarning("[AnnouncementSystem] Duplicate found -> destroying this one.");
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
        
        //Know which hospital the player belongs to
        if (localPlayerHospital == null)
        {
            CacheLocalPlayer();
        }

        //Prevent errors from null assignments
        if (localPlayerHospital == null)
        {
            return;
        }

        //Only play the sound if it matches that player's hospital (prevents other players from hearing other hospital's announcements)
        if (localPlayerHospital.Hospital.Value != hospital)
        {
            //Debug.Log("[AnnouncementSystem] Not this player's hospital — ignoring announcement.");
            return;
        }

        // Convert room number to array index (starts at 0)
        int index = roomNumber - 1;

        if (index < 0 || index >= 6)
        {
            //Debug.LogWarning("[AnnouncementSystem] Room index out of range.");
            return;
        }

        //Debug.Log($"[AnnouncementSystem] LocalClientId={NetworkManager.Singleton.LocalClientId}, Focused={Application.isFocused}");

        //Select the desired hospital audio array
        AudioClip clip = (hospital == HospitalType.Blue) ? hOneRoomClips[index]: hTwoRoomClips[index];

        //if clip does not exist, return
        if (clip == null)
        {
            return;
        }

        StartCoroutine(PlayAnnouncementNextFrame(clip));
    }

    private System.Collections.IEnumerator PlayAnnouncementNextFrame(AudioClip clip)
    {
        yield return null; // wait 1 frame
        source.PlayOneShot(clip);
    }
}