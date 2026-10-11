using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class SlotLever : NetworkBehaviour, IInteractable
{
    [SerializeField] private SlotManager slotManager;
    [SerializeField] private int machineIndex;

    private readonly NetworkVariable<bool> isUnlocked = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public void SetUnlocked(bool unlocked)
    {
        if (IsServer) isUnlocked.Value = unlocked;
    }

    public void Interact()
    {
        PullLever();
    }

    private void PullLever()
    {
        if (isUnlocked.Value && slotManager != null)
        {
            slotManager.CheckSolution(machineIndex);
        }
    }
}