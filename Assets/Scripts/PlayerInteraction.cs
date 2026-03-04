using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : NetworkBehaviour
{
    [SerializeField] private CrosshairBehaviour targeting;

    [SerializeField] private AudioSource buttonClick;
    [SerializeField] private AudioSource bedLower;

    private void Reset()
    {
        if (targeting == null)
            targeting = GetComponent<CrosshairBehaviour>();
    }

    private void Update()
    {
        //if (Time.frameCount % 120 == 0)
        //    Debug.Log($"[PlayerInteraction] Update running. IsOwner={IsOwner}", this);

        if (!IsOwner) return;
        //Debug.Log($"[PlayerInteraction] targeting={(targeting ? "OK" : "NULL")}", this);
        if (targeting == null) return;

        // No keyboard available (rare, but safe)
        if (Keyboard.current == null) return;

        // Press E to lower bed (only if aiming at bed button)
        if (Keyboard.current.eKey.wasPressedThisFrame && targeting.IsAimingAtBedButton())
        {
            //Debug.Log("[PlayerInteraction] E pressed while aiming at bed button");
            buttonClick.Play();
            targeting.CurrentPatient.LowerBedServerRpc();
            bedLower.Play();
        }

        // Press Space to perform one compression (only if aiming at patient)
        if (Keyboard.current.spaceKey.wasPressedThisFrame && targeting.IsAimingAtPatient())
        {
            targeting.CurrentPatient.CompressionServerRpc();
        }
    }
}