using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class BernoulliCard : NetworkBehaviour
{
    [SerializeField] private TMP_Text frontText;
    [SerializeField] private TMP_Text backText;
    [SerializeField] private GrabbableObject grabbable;

    public readonly NetworkVariable<FixedString64Bytes> CardLabel = new(
        string.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    public string Value => CardLabel.Value.ToString();

    private void Awake()
    {
        if (grabbable == null) grabbable = GetComponent<GrabbableObject>();
    }

    public override void OnNetworkSpawn()
    {
        CardLabel.OnValueChanged += HandleLabelChanged;

        if (!CardLabel.Value.IsEmpty)
        {
            UpdateVisuals(CardLabel.Value.ToString());
        }
    }

    public override void OnNetworkDespawn()
    {
        CardLabel.OnValueChanged -= HandleLabelChanged;
    }

    private void HandleLabelChanged(FixedString64Bytes oldVal, FixedString64Bytes newVal)
    {
        UpdateVisuals(newVal.ToString());
    }

    public void SetLabel(string label)
    {
        CardLabel.Value = label;
    }

    public void ResetToOrigin()
    {
        if (grabbable != null)
        {
            grabbable.ResetToOrigin();
        }
    }

    private void UpdateVisuals(string text)
    {
        if (frontText != null) frontText.text = text;
        if (backText != null) backText.text = text;
    }
}