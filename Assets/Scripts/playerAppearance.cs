using Unity.Netcode;
using UnityEngine;

public class PlayerAppearance : NetworkBehaviour
{
    public enum Role : byte
    {
        Host = 0,
        Client = 1
    }

    [SerializeField] private Renderer bodyRenderer;
    [SerializeField] private Renderer handLRenderer;
    [SerializeField] private Renderer handRRenderer;

    [SerializeField] private Material hostBodyMaterial;
    [SerializeField] private Material hostHandMaterial;

    [SerializeField] private Material clientBodyMaterial;
    [SerializeField] private Material clientHandMaterial;

    private NetworkVariable<Role> role = new NetworkVariable<Role>(
        Role.Client,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        ApplyMaterials(role.Value);

        role.OnValueChanged += OnRoleChanged;

        // Server decides who is host vs client
        if (IsServer)
        {
            if (OwnerClientId == NetworkManager.ServerClientId)
                role.Value = Role.Host;
            else
                role.Value = Role.Client;
        }
    }

    public override void OnNetworkDespawn()
    {
        role.OnValueChanged -= OnRoleChanged;
    }

    private void OnRoleChanged(Role oldValue, Role newValue)
    {
        ApplyMaterials(newValue);
    }

    private void ApplyMaterials(Role r)
    {
        if (r == Role.Host)
        {
            if (bodyRenderer) 
            {
                bodyRenderer.sharedMaterial = hostBodyMaterial;
            }
            if (handLRenderer) 
            {
                handLRenderer.sharedMaterial = hostHandMaterial;
            }
            if (handRRenderer) 
            {
                handRRenderer.sharedMaterial = hostHandMaterial;
            }
        }
        else
        {
            if (bodyRenderer) 
            {
                bodyRenderer.sharedMaterial = clientBodyMaterial;
            }
            if (handLRenderer) 
            {
                handLRenderer.sharedMaterial = clientHandMaterial;
            }
            if (handRRenderer) 
            {
                handRRenderer.sharedMaterial = clientHandMaterial;
            }
        }
    }
}