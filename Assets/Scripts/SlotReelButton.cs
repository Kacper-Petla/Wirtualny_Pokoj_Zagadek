using UnityEngine;

public class SlotReelButton : MonoBehaviour, IInteractable
{
    public enum StepDirection
    {
        Up = 1,
        Down = -1
    }

    [SerializeField] private SlotReel targetReel;
    [SerializeField] private StepDirection direction = StepDirection.Up;

    public void Interact()
    {
        if (targetReel == null) return;

        targetReel.RotateStep((int)direction);
    }
}