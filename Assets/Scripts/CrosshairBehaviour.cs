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

    [SerializeField] private float raycastRate = 0.05f;
    private float nextRaycastTime;

    private bool handsShowingCpr;

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
        {
            handsShowingCpr = false;
            SetHands(false); // start with normal hands
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (Time.time >= nextRaycastTime)
        {
            nextRaycastTime = Time.time + raycastRate;

            RaycastForTargets();
            UpdateCprHandsTarget();
        }

        bool canShowCpr =
            IsAimingAtPatient() &&
            CurrentPatient != null &&
            CurrentPatient.BedLowered.Value &&
            !CurrentPatient.Resolved.Value;

        if (canShowCpr != handsShowingCpr)
        {
            handsShowingCpr = canShowCpr;
            SetHands(canShowCpr);
        }

        UpdateCrosshairColour();
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

        if (currentCprHands != null)
            currentCprHands.SetActive(false);

        lastPatient = CurrentPatient;
        currentCprHands = CurrentPatient.GetCprHandsObject();

        if (currentCprHands != null)
            currentCprHands.SetActive(false);
    }

    private void UpdateCrosshairColour()
    {
        if (crosshair == null) return;

        Color target;

        if (!InRange || HitType == HitType.None)
        {
            target = defaultColour;
        }
        else
        {
            bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
            target = isHost ? hostInRangeColour : clientInRangeColour;
        }

        if (crosshair.color != target)
            crosshair.color = target;
    }

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