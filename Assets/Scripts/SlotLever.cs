using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class SlotLever : NetworkBehaviour, IInteractable
{
    [SerializeField] private SlotMachineManager machineManager;

    public void Interact()
    {
        PullLever();
    }

    private void PullLever()
    {
        if (machineManager != null)
        {
            machineManager.CheckSolution();
        }
    }
}