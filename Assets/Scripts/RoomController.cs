using System;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public struct RoomHintPage
{
    [Tooltip("Tytu³ podpowiedzi (wyœwietlany u góry)")]
    public string hintTitle;

    [Tooltip("Jeœli przypisany, notatnik ukryje tekst i wyœwietli ten obraz na ca³ej stronie")]
    public Sprite hintImage;

    [TextArea(3, 8)]
    [Tooltip("Tekst podpowiedzi (u¿ywany tylko, gdy pole hintImage jest puste)")]
    public string content;

    [Tooltip("Po ilu sekundach od wejœcia do pokoju strona ma siê odblokowaæ (0 = od razu)")]
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