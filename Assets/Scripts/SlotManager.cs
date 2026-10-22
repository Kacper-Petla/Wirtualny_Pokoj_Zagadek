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
    [SerializeField] private TMP_Text board1Display;
    [SerializeField] private TMP_Text board2Display;

    [SerializeField] private GameObject wallNoteRoot;
    [SerializeField] private TMP_Text noteTextDisplay;


    [Header("Machines & Levers")]
    [SerializeField] private SlotMachine machine1;
    [SerializeField] private SlotLever lever1;
    [SerializeField] private SlotMachine machine2;
    [SerializeField] private SlotLever lever2;

    [Header("Rewards")]
    [SerializeField] private GameObject rewardNote;
    [SerializeField] private GameObject rewardKey;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip intro1Audio;
    [SerializeField] private AudioClip riddle1Audio;
    [SerializeField] private AudioClip intro2Audio;
    [SerializeField] private AudioClip riddle2Audio;
    [SerializeField] private AudioClip successAudio;
    [SerializeField] private AudioClip failAudio;
    [SerializeField] private AudioClip solvedAudio;
    [SerializeField] private AudioClip pickupAudio;

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

    private readonly NetworkVariable<bool> isNotePinnedToWall = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private int[] targetCode1 = new int[3];
    private int[] targetCode2 = new int[3];

    private string introDialogue1 = "<mark=#FFFFFF0F><color=white>Zauważyłem, że ten automat jest tak ustawiony, że jackpot to średnia z ostatnich 5 wygranych.</color></mark>";
    private string riddleDialogue1 = "<mark=#FFFFFF0F><color=white>Zauważyłem, że ten automat jest tak ustawiony, że jackpot to średnia z ostatnich 5 wygranych.</color></mark>";
    private string introDialogue2 = "<mark=#FFFFFF0F><color=green>Wszędzie szukałem tej kartki, spisałem na niej, ile razy wypadła dana kombinacja na automacie po drugiej stronie, gdyż posiada podobną wadę, że jackpot to średnia wygranych, ale z całego dnia.</color></mark>";
    private string riddleDialogue2 = "<mark=#FFFFFF0F><color=white>Użyj mnożników z kartki i wygranych z tabliczki, by obliczyć średnią ważoną na drugim automacie.</color></mark>";
    private string successDialogue = "<mark=#FFFFFF0F><color=green>Niewiarygodne! To klucz do drzwi wyjściowych.</color></mark>";
    private string failDialogue = "<mark=#FFFFFF0F><color=red>To chyba nie jest poprawna liczba.</color></mark>";
    private string alreadySolvedDialogue = "<mark=#FFFFFF0F><color=white>Dziękuję za pomoc, zagadka została już rozwiązana.</color></mark>";
    private string pickupDialogue = "<mark=#FFFFFF0F><color=white>Zapiski z automatu trafiły na ścianę! Teraz oblicz średnią ważoną.</color></mark>";

    public override void OnNetworkSpawn()
    {
        syncedGhostText.OnValueChanged += OnGhostTextChanged;
        syncedBoard1Text.OnValueChanged += OnBoard1TextChanged;
        syncedBoard2Text.OnValueChanged += OnBoard2TextChanged;
        syncedNoteText.OnValueChanged += OnNoteTextChanged;
        CurrentStage.OnValueChanged += OnStageChanged;
        isNotePinnedToWall.OnValueChanged += OnNotePinnedChanged;

        ApplyStageVisuals(CurrentStage.Value);
        ApplyNoteWallState(isNotePinnedToWall.Value);

        if (IsServer && CurrentStage.Value == PuzzleStage.WaitingForGhost)
        {
            GeneratePuzzlesData();
        }
    }

    public override void OnNetworkDespawn()
    {
        syncedGhostText.OnValueChanged -= OnGhostTextChanged;
        syncedBoard1Text.OnValueChanged -= OnBoard1TextChanged;
        syncedBoard2Text.OnValueChanged -= OnBoard2TextChanged;
        syncedNoteText.OnValueChanged -= OnNoteTextChanged;
        CurrentStage.OnValueChanged -= OnStageChanged;
        isNotePinnedToWall.OnValueChanged -= OnNotePinnedChanged;
    }

    private void OnGhostTextChanged(FixedString512Bytes oldVal, FixedString512Bytes newVal)
    {
        if (ghostDialogueDisplay != null) ghostDialogueDisplay.text = newVal.ToString();
    }

    private void OnBoard1TextChanged(FixedString512Bytes oldVal, FixedString512Bytes newVal)
    {
        if (board1Display != null) board1Display.text = newVal.ToString();
    }

    private void OnBoard2TextChanged(FixedString512Bytes oldVal, FixedString512Bytes newVal)
    {
        if (board2Display != null) board2Display.text = newVal.ToString();
    }

    private void OnNoteTextChanged(FixedString512Bytes oldVal, FixedString512Bytes newVal)
    {
        if (noteTextDisplay != null) noteTextDisplay.text = newVal.ToString();
    }

    private void OnStageChanged(PuzzleStage oldStage, PuzzleStage newStage)
    {
        ApplyStageVisuals(newStage);
    }

    private void OnNotePinnedChanged(bool oldVal, bool newVal)
    {
        ApplyNoteWallState(newVal);
    }

    private void ApplyStageVisuals(PuzzleStage stage)
    {
        bool showPhysicalNote = (stage == PuzzleStage.Machine2_Active) && !isNotePinnedToWall.Value;
        SetItemActive(rewardNote, showPhysicalNote);

        SetItemActive(rewardKey, stage == PuzzleStage.Completed);
    }

    public void PinNoteToWall()
    {
        if (!IsServer || isNotePinnedToWall.Value) return;

        isNotePinnedToWall.Value = true;
        syncedGhostText.Value = pickupDialogue;

        audioSource.clip = pickupAudio;
        audioSource.Play();
    }

    private void ApplyNoteWallState(bool pinned)
    {
        if (wallNoteRoot != null)
        {
            wallNoteRoot.SetActive(pinned);
        }

        if (noteTextDisplay != null)
        {
            noteTextDisplay.gameObject.SetActive(pinned);
        }

        if (pinned)
        {
            SetItemActive(rewardNote, false);
        }
    }

    private void SetItemActive(GameObject item, bool active)
    {
        if (item == null) return;
        var renderers = item.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers) r.enabled = active;

        var colliders = item.GetComponentsInChildren<Collider>(true);
        foreach (var c in colliders) c.enabled = active;

        if (item.TryGetComponent<GrabbableObject>(out var grabbable))
        {
            grabbable.enabled = active;
        }

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
            syncedGhostText.Value = introDialogue1;

            audioSource.clip = intro1Audio;
            audioSource.Play();
        }
        else if (CurrentStage.Value == PuzzleStage.Machine1_Active)
        {
            syncedGhostText.Value = riddleDialogue1;

            audioSource.clip = riddle1Audio;
            audioSource.Play();
        }
        else if (CurrentStage.Value == PuzzleStage.Machine2_Active)
        {
            syncedGhostText.Value = riddleDialogue2;

            audioSource.clip = riddle2Audio;
            audioSource.Play();
        }
        else
        {
            syncedGhostText.Value = alreadySolvedDialogue;

            audioSource.clip = solvedAudio;
            audioSource.Play();
        }
    }

    public void CheckSolution(int machineIndex)
    {
        if (!IsServer) return;

        if (machineIndex == 0 && CurrentStage.Value == PuzzleStage.Machine1_Active)
        {
            if (machine1 == null) return;
            int[] vals = machine1.GetCurrentValues();
            bool isCorrect = (vals[0] == targetCode1[0] && vals[1] == targetCode1[1] && vals[2] == targetCode1[2]);

            if (isCorrect)
            {
                CurrentStage.Value = PuzzleStage.Machine2_Active;
                lever1.SetUnlocked(false);
                lever2.SetUnlocked(true);
                syncedGhostText.Value = introDialogue2;

                audioSource.clip = intro2Audio;
                audioSource.Play();
            }
            else
            {
                syncedGhostText.Value = failDialogue;

                audioSource.clip = failAudio;
                audioSource.Play();
            }
        }
        else if (machineIndex == 1 && CurrentStage.Value == PuzzleStage.Machine2_Active)
        {
            if (machine2 == null) return;
            int[] vals = machine2.GetCurrentValues();
            bool isCorrect = (vals[0] == targetCode2[0] && vals[1] == targetCode2[1] && vals[2] == targetCode2[2]);

            if (isCorrect)
            {
                CurrentStage.Value = PuzzleStage.Completed;
                lever2.SetUnlocked(false);
                syncedGhostText.Value = successDialogue;

                audioSource.clip = successAudio;
                audioSource.Play();
            }
            else
            {
                syncedGhostText.Value = failDialogue;

                audioSource.clip = failAudio;
                audioSource.Play();
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

        syncedBoard1Text.Value = $"Ostatnie wygrane:\n {vals[0]}\n {vals[1]}\n {vals[2]}\n {vals[3]}\n {vals[4]}";
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

        syncedBoard2Text.Value = $"Nagrody:\n3x🍒 - {val1}\n3x🔔 - {val2}\n3x💎 - {val3}";
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