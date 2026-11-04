using UnityEngine;

public class KeypadButton : MonoBehaviour, IInteractable
{
    public enum ButtonType
    {
        Digit0 = 0, Digit1 = 1, Digit2 = 2, Digit3 = 3, Digit4 = 4,
        Digit5 = 5, Digit6 = 6, Digit7 = 7, Digit8 = 8, Digit9 = 9,
        Clear = 10,
        Submit = 11
    }

    [SerializeField] private KeypadManager manager;
    [SerializeField] private ButtonType buttonType;

    public void Interact()
    {
        if (manager == null) return;

        if (buttonType == ButtonType.Clear)
        {
            manager.OnKeypadClearPressed();
        }
        else if (buttonType == ButtonType.Submit)
        {
            manager.OnKeypadSubmitPressed();
        }
        else
        {
            int digit = (int)buttonType;
            manager.OnKeypadDigitPressed(digit);
        }
    }
}