using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class DiceNote : NetworkBehaviour, IInteractable
{
    [SerializeField] private TMP_Text noteTextDisplay;

    private readonly NetworkVariable<FixedString512Bytes> syncedCombinations = new(
        string.Empty,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<bool> isNoteVisible = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        syncedCombinations.OnValueChanged += (oldVal, newVal) => UpdateText(newVal.ToString());
        isNoteVisible.OnValueChanged += (oldVal, newVal) => SetVisibility(newVal);

        UpdateText(syncedCombinations.Value.ToString());
        SetVisibility(isNoteVisible.Value);
    }

    public void SetCombinations(string text)
    {
        if (!IsServer) return;
        syncedCombinations.Value = text;
    }

    public void Interact()
    {
        isNoteVisible.Value = !isNoteVisible.Value;
    }

    private void UpdateText(string text)
    {
        if (noteTextDisplay != null)
        {
            noteTextDisplay.text = text;
        }
    }

    private void SetVisibility(bool visible)
    {
        if (noteTextDisplay != null)
        {
            noteTextDisplay.gameObject.SetActive(visible);
        }
    }
}