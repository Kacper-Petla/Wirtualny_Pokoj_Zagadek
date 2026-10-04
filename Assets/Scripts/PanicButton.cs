using Unity.Netcode;
using UnityEngine;

public class PanicButton : NetworkBehaviour, IInteractable
{
    [SerializeField] private GrabbableObject targetChip;



    public void Interact()
    {
        if (targetChip != null)
        {
            targetChip.ResetToOrigin();
            return;
        }
    }
}