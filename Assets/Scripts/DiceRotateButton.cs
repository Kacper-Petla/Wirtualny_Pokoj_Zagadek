using UnityEngine;

public class DiceRotateButton : MonoBehaviour, IInteractable
{
    public enum Direction { Up, Down, Left, Right }

    [SerializeField] private DiceController targetDice;
    [SerializeField] private Direction rotateDirection;

    public void Interact()
    {
        if (targetDice == null) return;

        Vector3 axis = Vector3.zero;
        float angle = 90f;

        switch (rotateDirection)
        {
            case Direction.Up:
                axis = Vector3.right;
                angle = -90f;
                break;
            case Direction.Down:
                axis = Vector3.right;
                angle = 90f;
                break;
            case Direction.Left:
                axis = Vector3.forward;
                angle = -90f;
                break;
            case Direction.Right:
                axis = Vector3.forward;
                angle = 90f;
                break;
        }

        targetDice.RotateDice(axis, angle);
    }
}