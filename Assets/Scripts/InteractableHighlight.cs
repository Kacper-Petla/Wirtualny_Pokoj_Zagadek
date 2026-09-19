using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Outline))]
public class InteractableHighlight : NetworkBehaviour
{
    private Outline outlineComponent;

    private readonly NetworkVariable<bool> isHighlightedNet = new(
        value: false,
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        outlineComponent = GetComponent<Outline>();
        if (outlineComponent != null)
        {
            outlineComponent.enabled = false;
        }
    }

    public override void OnNetworkSpawn()
    {
        isHighlightedNet.OnValueChanged += OnHighlightChanged;
        ApplyHighlight(isHighlightedNet.Value);
    }

    public override void OnNetworkDespawn()
    {
        isHighlightedNet.OnValueChanged -= OnHighlightChanged;
    }

    public void SetHighlight(bool enable)
    {
        if (IsServer)
        {
            isHighlightedNet.Value = enable;
        }
        else
        {
            RequestHighlightServerRpc(enable);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestHighlightServerRpc(bool enable)
    {
        isHighlightedNet.Value = enable;
    }

    private void OnHighlightChanged(bool previousValue, bool newValue)
    {
        ApplyHighlight(newValue);
    }

    private void ApplyHighlight(bool enable)
    {
        if (outlineComponent != null)
        {
            outlineComponent.enabled = enable;
        }
    }

    private void OnDisable()
    {
        if (IsServer && isHighlightedNet != null)
        {
            isHighlightedNet.Value = false;
        }
        if (outlineComponent != null)
        {
            outlineComponent.enabled = false;
        }
    }
}