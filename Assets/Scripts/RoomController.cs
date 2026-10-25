using System;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public struct RoomHintPage
{
    public string hintTitle;

    public Sprite hintImage;

    [TextArea(3, 8)]
    public string content;

    public float unlockDelaySeconds;

    public bool HasImage => hintImage != null;
}

public class RoomController : MonoBehaviour, IRoomController
{

    [SerializeField] private Transform roomAnchor;

    [SerializeField] private Transform notebookSocket;
    [SerializeField] private Transform calculatorSocket;

    [SerializeField] private RoomHintPage[] hintPages;

    [SerializeField] private UnityEvent onRoomEntered;

    public Transform RoomAnchor => roomAnchor != null ? roomAnchor : transform;
    public Transform NotebookSocket => notebookSocket;
    public Transform CalculatorSocket => calculatorSocket;
    public RoomHintPage[] HintPages => hintPages;

    public void OnRoomEntered()
    {
        Debug.Log($"[{gameObject.name}] Wejœcie do pokoju.");
        onRoomEntered?.Invoke();
    }
}