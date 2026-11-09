using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CardContainer : NetworkBehaviour
{
    [SerializeField] private TMP_Text currentSumDisplayText;

    private readonly HashSet<Card> activeCards = new();

    public readonly NetworkVariable<int> CurrentTotalSum = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    public override void OnNetworkSpawn()
    {
        CurrentTotalSum.OnValueChanged += (oldVal, newVal) => UpdateDisplay(newVal);
        UpdateDisplay(CurrentTotalSum.Value);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        var card = other.GetComponentInParent<Card>();
        if (card != null && activeCards.Add(card))
        {
            RecalculateSum();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;

        var card = other.GetComponentInParent<Card>();
        if (card != null && activeCards.Remove(card))
        {
            RecalculateSum();
        }
    }

    private void RecalculateSum()
    {
        int sum = 0;
        foreach (var c in activeCards)
        {
            if (c != null) sum += c.CardValue;
        }

        CurrentTotalSum.Value = sum;
    }

    private void UpdateDisplay(int sum)
    {
        if (currentSumDisplayText != null)
        {
            currentSumDisplayText.text = $"SUMA KART: <b>{sum}</b>";
        }
    }
}