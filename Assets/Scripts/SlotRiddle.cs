using UnityEngine;

public class SlotRiddle : MonoBehaviour, IInteractable
{
    [SerializeField] private SlotManager slotManager;

    public void Interact()
    {
        if (slotManager != null)
        {
            slotManager.InteractGhost();
        }
    }
}
