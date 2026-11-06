using UnityEngine;

public class SafeButton : MonoBehaviour, IInteractable
{
    public enum ActionType
    {
        TensUp,
        TensDown,
        UnitsUp,
        UnitsDown,
        Submit
    }

    [SerializeField] private SafeManager safeManager;
    [SerializeField] private ActionType actionType;

    public void Interact()
    {
        if (safeManager == null) return;

        switch (actionType)
        {
            case ActionType.TensUp:
                safeManager.ChangeValue(10);
                break;
            case ActionType.TensDown:
                safeManager.ChangeValue(-10);
                break;
            case ActionType.UnitsUp:
                safeManager.ChangeValue(1);
                break;
            case ActionType.UnitsDown:
                safeManager.ChangeValue(-1);
                break;
            case ActionType.Submit:
                safeManager.SubmitCode();
                break;
        }
    }
}