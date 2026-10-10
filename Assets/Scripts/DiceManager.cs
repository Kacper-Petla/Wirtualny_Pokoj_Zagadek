using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using TMPro;

public class DiceManager : NetworkBehaviour
{
    private const int CombinationsCount = 10;

    [SerializeField] private TMP_Text riddleDisplay;
    [SerializeField] private TMP_Text noteTextDisplay;
    [SerializeField] private GameObject notePanelRoot;
    [SerializeField] private DiceController[] activeDice;

    [SerializeField] private PuzzlePiece scenePuzzlePiece;


    private string introDialogue = $"<mark=#1E1E1Eff><color=white>Mam wra¿enie, ¿e te koœci nie s¹ równo wywa¿one. Spisa³am w notatniku {CombinationsCount} kombinacji, jakie mi wypad³y, ale nie wiem, jaka jest dominanta ich sumy.</color></mark>";
    private string successDialogue = "<mark=#1E1E1Eff><color=green>Niewiarygodne! To dok³adnie ta liczba. Trzymaj to w nagrodê.</color></mark>";
    private string failDialogue = "<mark=#1E1E1Eff><color=red>Coœ siê nie zgadza... Suma u³o¿onych koœci to nie dominanta z moich zapisków.</color></mark>";
    private string alreadySolvedDialogue = "<mark=#1E1E1Eff><color=white>Dziêkujê za pomoc, zagadka zosta³a ju¿ rozwi¹zana.</color></mark>";

    private readonly NetworkVariable<int> targetModeSum = new(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<bool> IsSolved = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<bool> Explained = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<FixedString512Bytes> syncedDialogue = new(
        string.Empty,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<FixedString512Bytes> syncedCombinations = new(
        string.Empty,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<bool> isNoteVisible = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        syncedCombinations.OnValueChanged += (oldVal, newVal) => RefreshDisplay();
        isNoteVisible.OnValueChanged += (oldVal, newVal) => SetVisibility(newVal);
        syncedDialogue.OnValueChanged += OnDialogueChanged;
        IsSolved.OnValueChanged += OnSolvedChanged;

        ApplyRewardState(IsSolved.Value);

        SetVisibility(isNoteVisible.Value);

        if (!syncedDialogue.Value.IsEmpty && riddleDisplay != null)
        {
            riddleDisplay.text = syncedDialogue.Value.ToString();
        }

        if (IsServer)
        {
            if (targetModeSum.Value == -1)
            {
                GeneratePuzzleData();
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        syncedCombinations.OnValueChanged -= (oldVal, newVal) => RefreshDisplay();
        isNoteVisible.OnValueChanged -= (oldVal, newVal) => SetVisibility(newVal);
        syncedDialogue.OnValueChanged -= OnDialogueChanged;
        IsSolved.OnValueChanged -= OnSolvedChanged;
    }

    private void OnDialogueChanged(FixedString512Bytes oldVal, FixedString512Bytes newVal)
    {
        if (riddleDisplay != null && !newVal.IsEmpty)
        {
            riddleDisplay.text = newVal.Value.ToString();
        }
    }

    private void OnSolvedChanged(bool oldVal, bool newVal)
    {
        ApplyRewardState(newVal);
    }

    private void ApplyRewardState(bool revealed)
    {
        if (scenePuzzlePiece == null) return;

        var renderers = scenePuzzlePiece.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers) r.enabled = revealed;

        var colliders = scenePuzzlePiece.GetComponentsInChildren<Collider>(true);
        foreach (var c in colliders) c.enabled = revealed;

        if (scenePuzzlePiece.TryGetComponent<GrabbableObject>(out var grabbable))
        {
            grabbable.enabled = revealed;
        }

        if (scenePuzzlePiece.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = !revealed;
        }
    }

    public void DisplayRiddle()
    {
        if (IsSolved.Value)
        {
            syncedDialogue.Value = alreadySolvedDialogue;
            return;
        }

        if (!Explained.Value)
        {
            syncedDialogue.Value = introDialogue;
            Explained.Value = true;
            return;
        }

        VerifyDicePlacement();
    }

    public void DisplayNote()
    {
        isNoteVisible.Value = !isNoteVisible.Value;
    }

    private void GeneratePuzzleData()
    {
        if (!IsServer) return;

        // 1. Losujemy docelow¹ dominantê z pe³nego zakresu sum dwóch koœci (2 - 12)
        int modeValue = UnityEngine.Random.Range(2, 13);
        targetModeSum.Value = modeValue;

        // 2. Ustalamy czêstoœæ dominanty (np. 3 lub 4 wyst¹pienia na 10 rzutów)
        int modeFrequency = UnityEngine.Random.Range(3, 5);

        List<int> sums = new List<int>(CombinationsCount);
        for (int i = 0; i < modeFrequency; i++)
        {
            sums.Add(modeValue);
        }

        // 3. Wype³niamy pozosta³e pozycje sumami (pilnuj¹c, by ¿adna nie osi¹gnê³a modeFrequency)
        Dictionary<int, int> otherFrequencies = new Dictionary<int, int>();
        int remainingCount = CombinationsCount - modeFrequency;

        while (remainingCount > 0)
        {
            int candidateSum = UnityEngine.Random.Range(2, 13);

            // Nie dodajemy ponownie dominanty
            if (candidateSum == modeValue) continue;

            otherFrequencies.TryGetValue(candidateSum, out int currentCount);

            // Ka¿da inna suma mo¿e wyst¹piæ maksymalnie (modeFrequency - 1) razy
            if (currentCount < modeFrequency - 1)
            {
                sums.Add(candidateSum);
                otherFrequencies[candidateSum] = currentCount + 1;
                remainingCount--;
            }
        }

        // 4. Tasowanie sum, by dominanta by³a rozrzucona po ca³ej kartce
        for (int i = sums.Count - 1; i > 0; i--)
        {
            int randomIndex = UnityEngine.Random.Range(0, i + 1);
            (sums[i], sums[randomIndex]) = (sums[randomIndex], sums[i]);
        }

        // 5. Rozbicie sum na fizyczne œcianki koœci (d1 + d2) i budowa tekstu
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<b>Zapiski rzutów:</b>");

        for (int i = 0; i < sums.Count; i++)
        {
            var (d1, d2) = SplitSumToDice(sums[i]);
            sb.AppendLine($"{i + 1}.\t({d1} + {d2})");
        }

        syncedCombinations.Value = sb.ToString();
        RefreshDisplay();

        Debug.Log($"[GhostPuzzle] Wygenerowano deterministyczn¹ dominantê: {modeValue} (wystêpuje {modeFrequency} razy)");
    }

    private (int d1, int d2) SplitSumToDice(int sum)
    {
        int minD1 = Mathf.Max(1, sum - 6);
        int maxD1 = Mathf.Min(6, sum - 1);

        int d1 = UnityEngine.Random.Range(minD1, maxD1 + 1);
        int d2 = sum - d1;

        return (d1, d2);
    }

    private void SetVisibility(bool visible)
    {
        if (notePanelRoot != null)
        {
            notePanelRoot.SetActive(visible);
        }
        else if (noteTextDisplay != null)
        {
            noteTextDisplay.gameObject.SetActive(visible);
        }
    }

    private void RefreshDisplay()
    {
        if (noteTextDisplay != null && !syncedCombinations.Value.IsEmpty)
        {
            noteTextDisplay.text = syncedCombinations.Value.ToString();
        }
    }

    private void VerifyDicePlacement()
    {
        if (activeDice == null || activeDice.Length == 0) return;

        int currentSum = 0;
        foreach (var die in activeDice)
        {
            if (die != null)
            {
                currentSum += die.CurrentTopValue.Value;
            }
        }

        Debug.Log($"[GhostPuzzle] Suma koœci gracza: {currentSum} | Oczekiwana dominanta: {targetModeSum.Value}");

        if (currentSum == targetModeSum.Value)
        {
            IsSolved.Value = true;
            syncedDialogue.Value = successDialogue;
        }
        else
        {
            syncedDialogue.Value = failDialogue;
            Explained.Value = false;
        }
    }
}