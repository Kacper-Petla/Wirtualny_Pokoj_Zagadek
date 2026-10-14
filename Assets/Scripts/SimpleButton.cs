using UnityEngine;
using UnityEngine.Events;

public class SimpleButton : MonoBehaviour, IInteractable
{
    [SerializeField] private UnityEvent onClick;

    public void Interact()
    {
        onClick?.Invoke();
    }
}