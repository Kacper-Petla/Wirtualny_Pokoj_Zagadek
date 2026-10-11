using System;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class SlotManager : NetworkBehaviour
{
    public enum PuzzleStage
    {
        WaitingForGhost,
        Machine1_Active,
        Machine2_Active,
        Completed
    }

    [Header("Displays")]
    [SerializeField] private TMP_Text ghostDialogueDisplay;
    [SerializeField] private TMP_Text board1Display;     // 5 liczb do średniej arytmetycznej
    [SerializeField] private TMP_Text board2Display;     // Wartości wygranych kombinacji
    [SerializeField] private TMP_Text noteTextDisplay;   // Kartka z częstościami z 1. automatu

    [Header("Machines & Levers")]
    [SerializeField] private SlotMachine machine1;
    [SerializeField] private SlotLever lever1;
    [SerializeField] private SlotMachine machine2;
    [SerializeField] private SlotLever lever2;

    [Header("Rewards (Scene Objects)")]
    [Tooltip("Kartka wypadająca z 1. automatu")]
    [SerializeField] private GrabbableObject rewardNote;
    [Tooltip("Klucz wypadający z 2. automatu")]
    [SerializeField] private GrabbableObject rewardKey;

    public readonly NetworkVariable<PuzzleStage> CurrentStage = new(
        PuzzleStage.WaitingForGhost,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<FixedString512Bytes> syncedGhostText = new(
        string.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );
    private readonly NetworkVariable<FixedString512Bytes> syncedBoard1Text = new(
        string.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );
    private readonly NetworkVariable<FixedString512Bytes> syncedBoard2Text = new(
        string.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );
    private readonly NetworkVariable<FixedString512Bytes> syncedNoteText = new(
        string.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private int[] targetCode1 = new int[3];
    private int[] targetCode2 = new int[3];

    public override void OnNetworkSpawn()
    {
        syncedGhostText.OnValueChanged += (o, n) => { if (ghostDialogueDisplay != null) ghostDialogueDisplay.text = n.ToString(); };
        syncedBoard1Text.OnValueChanged += (o, n) => { if (board1Display != null) board1Display.text = n.ToString(); };
        syncedBoard2Text.OnValueChanged += (o, n) => { if (board2Display != null) board2Display.text = n.ToString(); };
        syncedNoteText.OnValueChanged += (o, n) => { if (noteTextDisplay != null) noteTextDisplay.text = n.ToString(); };
        CurrentStage.OnValueChanged += OnStageChanged;

        ApplyStageVisuals(CurrentStage.Value);

        if (IsServer && CurrentStage.Value == PuzzleStage.WaitingForGhost)
        {
            GeneratePuzzlesData();
        }
    }

    private void OnStageChanged(PuzzleStage oldStage, PuzzleStage newStage)
    {
        ApplyStageVisuals(newStage);
    }

    private void ApplyStageVisuals(PuzzleStage stage)
    {
        SetItemActive(rewardNote, stage >= PuzzleStage.Machine2_Active);
        SetItemActive(rewardKey, stage == PuzzleStage.Completed);
    }

    private void SetItemActive(GrabbableObject item, bool active)
    {
        if (item == null) return;
        var renderers = item.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers) r.enabled = active;

        var colliders = item.GetComponentsInChildren<Collider>(true);
        foreach (var c in colliders) c.enabled = active;

        item.enabled = active;

        if (IsServer && item.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = !active;
            rb.useGravity = active;
        }
    }

    public void InteractGhost()
    {
        if (CurrentStage.Value == PuzzleStage.WaitingForGhost)
        {
            CurrentStage.Value = PuzzleStage.Machine1_Active;
            lever1.SetUnlocked(true);
            syncedGhostText.Value = "<mark=#1E1E1Eff><color=white>Oblicz średnią arytmetyczną z 5 liczb na tabliczce i ustaw ją na pierwszym automacie [XX,X]. Pociągnij za dźwignię!</color></mark>";
        }
        else if (CurrentStage.Value == PuzzleStage.Machine1_Active)
        {
            syncedGhostText.Value = "<mark=#1E1E1Eff><color=white>Ustaw na bębnach średnią arytmetyczną z tabliczki i pociągnij za dźwignię.</color></mark>";
        }
        else if (CurrentStage.Value == PuzzleStage.Machine2_Active)
        {
            syncedGhostText.Value = "<mark=#1E1E1Eff><color=white>Użyj mnożników z kartki i wygranych z tabliczki, by obliczyć średnią ważoną na drugim automacie!</color></mark>";
        }
        else
        {
            syncedGhostText.Value = "<mark=#1E1E1Eff><color=green>Świetna robota! Klucz do drzwi został wydany.</color></mark>";
        }
    }

    public void CheckSolution(int machineIndex)
    {
        if (!IsServer) return;

        if (machineIndex == 0 && CurrentStage.Value == PuzzleStage.Machine1_Active)
        {
            if (machine1.VerifyCode())
            {
                CurrentStage.Value = PuzzleStage.Machine2_Active;
                lever1.SetUnlocked(false);
                lever2.SetUnlocked(true);
                syncedGhostText.Value = "<mark=#1E1E1Eff><color=green>Brawo! Pierwszy automat wydał kartkę. Oblicz teraz średnią ważoną dla drugiego automatu.</color></mark>";
            }
            else
            {
                syncedGhostText.Value = "<mark=#1E1E1Eff><color=red>Zła kombinacja na pierwszym automacie. Przelicz średnią ponownie.</color></mark>";
            }
        }
        else if (machineIndex == 1 && CurrentStage.Value == PuzzleStage.Machine2_Active)
        {
            if (machine2.VerifyCode())
            {
                CurrentStage.Value = PuzzleStage.Completed;
                lever2.SetUnlocked(false);
                syncedGhostText.Value = "<mark=#1E1E1Eff><color=green>Doskonale! Drugi automat wydał klucz do drzwi!</color></mark>";
            }
            else
            {
                syncedGhostText.Value = "<mark=#1E1E1Eff><color=red>Błędna średnia ważona. Sprawdź zapiski na kartce i tabliczkę.</color></mark>";
            }
        }
    }

    private void GeneratePuzzlesData()
    {
        // 1. ZAGADKA 1: Średnia arytmetyczna 5 liczb
        // Losujemy średnią od 12.0 do 45.8 z krokiem 0.2 (ułamek dziesiętny: .0, .2, .4, .6, .8)
        int step = UnityEngine.Random.Range(60, 230); // 60 * 0.2 = 12.0, 229 * 0.2 = 45.8
        int sum = step; // Suma 5 liczb całkowitych = step
        float mean1 = sum / 5.0f;

        // Rozbijamy sumę na 5 liczb wokół średniej
        int baseVal = sum / 5;
        int rem = sum % 5;
        int[] vals = new int[5];
        for (int i = 0; i < 5; i++) vals[i] = baseVal + (i < rem ? 1 : 0);

        // Lekkie rozproszenie liczb, by nie były identyczne
        vals[0] -= 4; vals[1] += 4;
        vals[2] -= 2; vals[3] += 2;

        syncedBoard1Text.Value = $"Wartości: {vals[0]}, {vals[1]}, {vals[2]}, {vals[3]}, {vals[4]}";
        targetCode1 = ExtractDigits(mean1);
        machine1.SetupReelOptions(targetCode1);

        // 2. ZAGADKA 2: Średnia ważona (Wartość oczekiwana)
        // 3 kombinacje: Wisienki (C), Dzwonki (B), Diamenty (D)
        // Wagi sumują się do 10: w1 + w2 + w3 = 10
        int w1 = 5;
        int w2 = 3;
        int w3 = 2;

        int val1 = UnityEngine.Random.Range(2, 6) * 5;  // 10, 15, 20...
        int val2 = UnityEngine.Random.Range(4, 9) * 5;
        int val3 = UnityEngine.Random.Range(8, 15) * 5;

        float mean2 = (w1 * val1 + w2 * val2 + w3 * val3) / 10.0f;

        syncedBoard2Text.Value = $"Wygrane:\n🍒 Wisienki: {val1}\n🔔 Dzwonki: {val2}\n💎 Diamenty: {val3}";
        syncedNoteText.Value = $"Zapiski częstości (wagi / 10):\n🍒 Szansa: {w1}\n🔔 Szansa: {w2}\n💎 Szansa: {w3}";

        targetCode2 = ExtractDigits(mean2);
        machine2.SetupReelOptions(targetCode2);

        Debug.Log($"[DualSlot] Maszyna 1 cel: {mean1:F1} (Kod: {targetCode1[0]}{targetCode1[1]},{targetCode1[2]})");
        Debug.Log($"[DualSlot] Maszyna 2 cel: {mean2:F1} (Kod: {targetCode2[0]}{targetCode2[1]},{targetCode2[2]})");
    }

    private int[] ExtractDigits(float value)
    {
        int totalTenths = Mathf.RoundToInt(value * 10f); // np. 14.5 -> 145
        int d1 = (totalTenths / 100) % 10;               // 1
        int d2 = (totalTenths / 10) % 10;                // 4
        int d3 = totalTenths % 10;                       // 5
        return new int[] { d1, d2, d3 };
    }
}