using UnityEngine;
using Unity.Netcode;
using Unity.VisualScripting;

public class MonitorBehaviour : NetworkBehaviour
{
    [SerializeField] private PatientBehaviour patient;
    [SerializeField] private Renderer monitorRenderer;

    [SerializeField] private Material[] monitorMaterials; 

    private int currIndex = -1;

    private void Awake()
    {
       if (patient == null)
        {
            patient = GetComponentInParent<PatientBehaviour>();
        }

       if (monitorRenderer == null)
        {
            monitorRenderer = GetComponentInChildren<Renderer>();
        }
    }

    public override void OnNetworkSpawn()
    {
        if (patient == null || monitorRenderer == null)
        {
            return;
        }

        SetMaterial(0);

        if (IsClient)
        {
            patient.Saved.OnValueChanged += OnPatientStateChanged;
            patient.Resolved.OnValueChanged += OnPatientStateChanged;
        }
        
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        if (patient == null)
        {
            return;
        }

        patient.Saved.OnValueChanged -= OnPatientStateChanged;
        patient.Resolved.OnValueChanged -= OnPatientStateChanged;
    }

    void OnPatientStateChanged(bool oldVal, bool newVal)
    {
        UpdateMonitorMaterial();
    }

    void UpdateMonitorMaterial()
    {
        bool resolved = patient.Resolved.Value;
        bool saved = patient.Saved.Value;

        //default index
        int targetIndex = 0;

        if (resolved && saved)
        {
            targetIndex = 1;
        }
        if (resolved && !saved)
        {
            targetIndex = 2;
        }

        SetMaterial(targetIndex);
    }

    void SetMaterial(int index)
    {
        //check if input is valid
        if (monitorMaterials == null || monitorMaterials.Length == 0)
        {
            return;
        }
        if (index < 0 || index  >= monitorMaterials.Length)
        {
            return;
        }

        monitorRenderer.material = monitorMaterials[index];
        currIndex = index;
    }
}
