using UnityEngine;
using UnityEngine.Events;

public class MachineButton : MonoBehaviour, IInteractable
{
    [SerializeField] private UnityEvent onClick;

    public void Interact()
    {
        onClick?.Invoke();
    }
}