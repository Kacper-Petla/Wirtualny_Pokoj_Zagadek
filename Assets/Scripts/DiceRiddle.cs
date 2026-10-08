using UnityEngine;

public class DiceRiddle : MonoBehaviour, IInteractable
{
    [SerializeField] private DiceManager diceManager;

    public void Interact()
    {
        if (diceManager != null)
        {
            diceManager.DisplayRiddle();
        }
    }
}
