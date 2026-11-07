using Unity.Netcode;
using UnityEngine;

public class PanicButton : NetworkBehaviour, IInteractable
{
    [SerializeField] private GrabbableObject[] targetObjects;

    public void Interact()
    {
        if (targetObjects == null || targetObjects.Length == 0) return;

        foreach (var obj in targetObjects)
        {
            if (obj != null)
            {
                obj.ResetToOrigin();
            }
        }
    }
}