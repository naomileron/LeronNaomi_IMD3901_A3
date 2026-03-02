using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public enum HitType
{
    None,
    BedButton,
    Patient
}

public class CrosshairBehaviour : NetworkBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private LayerMask interactMask = ~0; // everything by default

    [SerializeField] private Canvas uiCanvas;

    [SerializeField] private Image crosshair;
    [SerializeField] private Color defaultColour = Color.white;

    [SerializeField] private Color hostInRangeColour = Color.blue;
    [SerializeField] private Color clientInRangeColour = Color.green;

    public PatientBehviour CurrentPatient { get; private set; }
    public Collider CurrentCollider { get; private set; }
    public HitType HitType { get; private set; }
    public bool InRange { get; private set; }

    // debug state so we only log when something changes
    private string lastState = "";

    private void Reset()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        // NEW: auto-fill the canvas if possible
        if (uiCanvas == null)
            uiCanvas = GetComponentInChildren<Canvas>(true);

        if (crosshair == null)
            crosshair = GetComponentInChildren<Image>(true);
    }

    public override void OnNetworkSpawn()
    {
        // NEW: auto-fill if not assigned
        if (uiCanvas == null)
            uiCanvas = GetComponentInChildren<Canvas>(true);

        if (crosshair == null)
            crosshair = GetComponentInChildren<Image>(true);

        // Only the local player should have their UI visible in THIS window.
        if (uiCanvas != null)
            uiCanvas.enabled = IsOwner;
    }

    private void Update()
    {
        if (!IsOwner) return;

        RaycastForTargets();
        CrosshairColour();
        DebugStateChanged();
    }

    private void RaycastForTargets()
    {
        CurrentPatient = null;
        CurrentCollider = null;
        HitType = HitType.None;
        InRange = false;

        if (playerCamera == null) return;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactMask, QueryTriggerInteraction.Ignore))
            return;

        InRange = true;
        CurrentCollider = hit.collider;

        if (CurrentCollider == null) return;

        // Try parent lookup
        CurrentPatient = CurrentCollider.GetComponentInParent<PatientBehviour>();

        // Fallback for nested prefabs
        if (CurrentPatient == null)
        {
            Transform root = CurrentCollider.transform.root;
            if (root != null)
                CurrentPatient = root.GetComponentInChildren<PatientBehviour>();
        }

        if (CurrentPatient == null)
        {
            HitType = HitType.None;
            return;
        }

        // Determine what was hit
        if (CurrentPatient.IsBedButton(CurrentCollider) || CurrentCollider.CompareTag("bedButton"))
        {
            HitType = HitType.BedButton;
        }
        else if (CurrentPatient.IsPatient(CurrentCollider) || CurrentCollider.CompareTag("patient"))
        {
            HitType = HitType.Patient;
        }
        else
        {
            HitType = HitType.None;
        }
    }

    private void CrosshairColour()
    {
        if (crosshair == null) return;

        if (!InRange || HitType == HitType.None)
        {
            crosshair.color = defaultColour;
            return;
        }

        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
        crosshair.color = isHost ? hostInRangeColour : clientInRangeColour;
    }

    private void DebugStateChanged()
    {
        // Build a compact state string
        string camName = playerCamera != null ? playerCamera.name : "NULL_CAM";
        string crossName = crosshair != null ? crosshair.name : "NULL_CROSSHAIR";
        string hitName = CurrentCollider != null ? CurrentCollider.name : "NONE";
        string hitTag = CurrentCollider != null ? CurrentCollider.tag : "NONE";
        string patientName = CurrentPatient != null ? CurrentPatient.name : "NONE";

        string state =
            $"owner={IsOwner} cam={camName} cross={crossName} " +
            $"inRange={InRange} hitType={HitType} hit={hitName} tag={hitTag} patient={patientName}";

        if (state != lastState)
        {
            lastState = state;
            Debug.Log("[CrosshairDebug] " + state);
        }
    }

    public bool IsAimingAtBedButton()
        => InRange && HitType == HitType.BedButton && CurrentPatient != null;

    public bool IsAimingAtPatient()
        => InRange && HitType == HitType.Patient && CurrentPatient != null;
}