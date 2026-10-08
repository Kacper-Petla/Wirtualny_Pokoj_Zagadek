using UnityEngine;

public class RouletteRiddle: MonoBehaviour, IInteractable
{
    [SerializeField] private RouletteManager rouletteManager;

    public void Interact()
    {
        if (rouletteManager != null)
        {
            rouletteManager.DisplayRiddle();
        }
    }
}
