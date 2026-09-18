using UnityEngine;

public interface IInteractable
{
    void Interact();
    void Interact(RaycastPointer pointer, RaycastHit hit) => Interact();
}