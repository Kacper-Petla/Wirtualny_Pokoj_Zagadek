using UnityEngine;

public class PhoneButton : MonoBehaviour, IInteractable
{
    public enum PhoneAction
    {
        DigitUp,
        DigitDown,
        SelectNextDigit,
        CallSubmit
    }

    [SerializeField] private PhoneManager phoneManager;
    [SerializeField] private PhoneAction action;

    public void Interact()
    {
        if (phoneManager == null) return;

        switch (action)
        {
            case PhoneAction.DigitUp:
                phoneManager.ChangeActiveDigit(1);
                break;
            case PhoneAction.DigitDown:
                phoneManager.ChangeActiveDigit(-1);
                break;
            case PhoneAction.SelectNextDigit:
                phoneManager.NextDigitPosition();
                break;
            case PhoneAction.CallSubmit:
                phoneManager.SubmitCall();
                break;
        }
    }
}