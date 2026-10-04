using UnityEngine;

public class RouletteCheck : MonoBehaviour, IInteractable
{
    [SerializeField] private RouletteManager rouletteManager;

    public void Interact()
    {
        if (rouletteManager != null)
        {
            rouletteManager.Verify();
        }
    }
}