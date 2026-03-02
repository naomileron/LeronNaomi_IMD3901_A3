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
    [SerializeField] private LayerMask interactMask = ~0;

    [SerializeField] private Canvas uiCanvas;
    [SerializeField] private Image crosshair;

    [SerializeField] private Color defaultColour = Color.white;
    [SerializeField] private Color hostInRangeColour = Color.blue;
    [SerializeField] private Color clientInRangeColour = Color.green;

    [SerializeField] private GameObject regularHands;

    private GameObject currentCprHands;
    private PatientBehaviour lastPatient;

    public PatientBehaviour CurrentPatient { get; private set; }
    public Collider CurrentCollider { get; private set; }
    public HitType HitType { get; private set; }
    public bool InRange { get; private set; }

    //private string lastState = "";

    private void Reset()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        if (uiCanvas == null)
            uiCanvas = GetComponentInChildren<Canvas>(true);

        if (crosshair == null)
            crosshair = GetComponentInChildren<Image>(true);
    }

    public override void OnNetworkSpawn()
    {
        if (uiCanvas != null)
            uiCanvas.enabled = IsOwner;

        if (IsOwner)
            SetHands(false); // start with normal hands
    }

    private void Update()
    {
        if (!IsOwner) return;

        RaycastForTargets();
        UpdateCprHandsTarget();

        bool canShowCpr =
            IsAimingAtPatient() &&
            CurrentPatient != null &&
            CurrentPatient.BedLowered.Value &&
            !CurrentPatient.Resolved.Value;

        SetHands(canShowCpr);

        UpdateCrosshairColour();
        //DebugStateChanged();
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

        CurrentPatient = CurrentCollider.GetComponentInParent<PatientBehaviour>();

        if (CurrentPatient == null)
        {
            Transform root = CurrentCollider.transform.root;
            if (root != null)
                CurrentPatient = root.GetComponentInChildren<PatientBehaviour>();
        }

        if (CurrentPatient == null)
        {
            HitType = HitType.None;
            return;
        }

        if (CurrentPatient.IsBedButton(CurrentCollider) || CurrentCollider.CompareTag("bedButton"))
            HitType = HitType.BedButton;
        else if (CurrentPatient.IsPatient(CurrentCollider) || CurrentCollider.CompareTag("patient"))
            HitType = HitType.Patient;
        else
            HitType = HitType.None;
    }

    private void UpdateCprHandsTarget()
    {
        // If you are not currently looking at a patient at all,
        // force-hide any CPR hands we previously grabbed.
        if (CurrentPatient == null)
        {
            if (currentCprHands != null)
                currentCprHands.SetActive(false);

            lastPatient = null;
            currentCprHands = null;
            return;
        }

        if (CurrentPatient == lastPatient)
            return;

        // hide old patient's hands
        if (currentCprHands != null)
            currentCprHands.SetActive(false);

        lastPatient = CurrentPatient;
        currentCprHands = CurrentPatient.GetCprHandsObject();

        // IMPORTANT: always start hidden until conditions allow showing
        if (currentCprHands != null)
            currentCprHands.SetActive(false);
    }

    private void UpdateCrosshairColour()
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

    //private void DebugStateChanged()
    //{
    //    string camName = playerCamera != null ? playerCamera.name : "NULL_CAM";
    //    string crossName = crosshair != null ? crosshair.name : "NULL_CROSS";
    //    string hitName = CurrentCollider != null ? CurrentCollider.name : "NONE";
    //    string hitTag = CurrentCollider != null ? CurrentCollider.tag : "NONE";
    //    string patientName = CurrentPatient != null ? CurrentPatient.name : "NONE";

    //    string state =
    //        $"owner={IsOwner} cam={camName} cross={crossName} " +
    //        $"inRange={InRange} hitType={HitType} hit={hitName} " +
    //        $"tag={hitTag} patient={patientName}";

    //    if (state != lastState)
    //    {
    //        lastState = state;
    //        Debug.Log("[CrosshairDebug] " + state);
    //    }
    //}

    public bool IsAimingAtBedButton()
        => InRange && HitType == HitType.BedButton && CurrentPatient != null;

    public bool IsAimingAtPatient()
        => InRange && HitType == HitType.Patient && CurrentPatient != null;

    
    private void SetHands(bool showCpr)
    {
        if (regularHands != null)
            regularHands.SetActive(!showCpr);

        if (currentCprHands != null)
            currentCprHands.SetActive(showCpr);
    }
}