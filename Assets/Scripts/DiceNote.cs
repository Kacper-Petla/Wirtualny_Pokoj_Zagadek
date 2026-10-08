using UnityEngine;

public class DiceNote : MonoBehaviour, IInteractable
{
    [SerializeField] private DiceManager diceManager;

    public void Interact()
    {
        if (diceManager != null)
        {
            diceManager.DisplayNote();
        }
    }
}