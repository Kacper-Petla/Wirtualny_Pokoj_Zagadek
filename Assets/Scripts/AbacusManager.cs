using System;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class AbacusManager : NetworkBehaviour
{
    [Serializable]
    public struct PresetDataSet
    {
        public int a, b, c, d; // 4 wygrane
    }

    [Header("Displays & UI")]
    [Tooltip("Tablica 1: Treœæ g³ównej zagadki z wylosowanymi danymi")]
    [SerializeField] private TMP_Text noteDisplayText;
    [Tooltip("Tablica 2: Informacja zwrotna od ducha o poprawnoœci i aktualnym kroku")]
    [SerializeField] private TMP_Text statusFeedbackText;

    [Header("Abacuses (Dok³adnie 8 sztuk w kolejnoœci!)")]
    [Tooltip("0: Œrednia, 1-4: (xi - avg)^2, 5: Suma kwadratów, 6: Wariancja, 7: Odchylenie (wewn¹trz pierwiastka)")]
    [SerializeField] private Abacus[] abacuses = new Abacus[8];

    [SerializeField] private GameObject keyItem;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip ghostInteractSound;
    [SerializeField] private AudioClip stepSuccessSound;
    [SerializeField] private AudioClip errorResetSound;
    [SerializeField] private AudioClip puzzleCompletedSound;

    private readonly PresetDataSet[] prebakedPool = new PresetDataSet[]
    {
        new PresetDataSet { a = 3,  b = 7,  c = 13, d = 17 }, // Avg=10. Kwadraty: 49, 9, 9, 49. Suma=116 -> Var=29
        new PresetDataSet { a = 4,  b = 8,  c = 12, d = 16 }, // Avg=10. Kwadraty: 36, 4, 4, 36. Suma=80 -> Var=20
        new PresetDataSet { a = 12, b = 14, c = 18, d = 20 }, // Avg=16. Kwadraty: 16, 4, 4, 16. Suma=40 -> Var=10
        new PresetDataSet { a = 2,  b = 6,  c = 10, d = 14 }, // Avg=8.  Kwadraty: 36, 4, 4, 36. Suma=80 -> Var=20
        new PresetDataSet { a = 5,  b = 9,  c = 11, d = 15 }  // Avg=10. Kwadraty: 25, 1, 1, 25. Suma=52 -> Var=13
    };

    // Polecenia przejœcia do kolejnych liczyde³ po sukcesie
    private readonly string[] stepNextInstructions = new string[]
    {
        "Dobrze! Teraz oblicz kwadrat ró¿nicy pierwszej wygranej od œredniej.",
        "Œwietnie! Teraz oblicz kwadrat ró¿nicy drugiej wygranej od œredniej.",
        "Zgadza siê! Teraz oblicz kwadrat ró¿nicy trzeciej wygranej od œredniej.",
        "Doskonale! Oblicz kwadrat ró¿nicy czwartej wygranej od œredniej.",
        "Poprawnie! Zsumuj wszystkie cztery kwadraty na kolejnym liczydle.",
        "Wybornie! Oblicz wariancjê (podziel sumê przez 4).",
        "Kapitalnie! WprowadŸ wartoœæ do wnêtrza pierwiastka dla odchylenia.",
        "Uda³o ci siê! Trzymaj to w nagrodê."
    };

    // Sieciowe zmienne stanu
    public readonly NetworkVariable<int> CurrentStep = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    public readonly NetworkVariable<bool> IsRiddleStarted = new(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<int> selectedDatasetIndex = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<FixedString512Bytes> syncedNoteText = new(
        string.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<FixedString128Bytes> syncedFeedbackText = new(
        string.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    public readonly NetworkVariable<bool> IsSolved = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private int[] targetSolutions = new int[8];

    public override void OnNetworkSpawn()
    {
        syncedNoteText.OnValueChanged += OnNoteTextChanged;
        syncedFeedbackText.OnValueChanged += OnFeedbackTextChanged;
        CurrentStep.OnValueChanged += HandleStepChanged;
        IsRiddleStarted.OnValueChanged += HandleRiddleStartedChanged;
        IsSolved.OnValueChanged += OnSolvedChanged;

        ApplyRewardState(IsSolved.Value);

        // Inicjalizacja UI przy do³¹czeniu
        if (noteDisplayText != null && !syncedNoteText.Value.IsEmpty)
            noteDisplayText.text = syncedNoteText.Value.ToString();

        if (statusFeedbackText != null && !syncedFeedbackText.Value.IsEmpty)
            statusFeedbackText.text = syncedFeedbackText.Value.ToString();

        if (IsServer)
        {
            InitPuzzleData();
        }
        else
        {
            RecalculateSolutions(selectedDatasetIndex.Value);
        }

        UpdateAbacusLockStates(CurrentStep.Value);
    }

    public override void OnNetworkDespawn()
    {
        syncedNoteText.OnValueChanged -= OnNoteTextChanged;
        syncedFeedbackText.OnValueChanged -= OnFeedbackTextChanged;
        CurrentStep.OnValueChanged -= HandleStepChanged;
        IsRiddleStarted.OnValueChanged -= HandleRiddleStartedChanged;
        IsSolved.OnValueChanged -= OnSolvedChanged;
    }

    private void OnNoteTextChanged(FixedString512Bytes oldVal, FixedString512Bytes newVal)
    {
        if (noteDisplayText != null) noteDisplayText.text = newVal.ToString();
    }

    private void OnFeedbackTextChanged(FixedString128Bytes oldVal, FixedString128Bytes newVal)
    {
        if (statusFeedbackText != null) statusFeedbackText.text = newVal.ToString();
    }

    private void HandleRiddleStartedChanged(bool oldVal, bool newVal)
    {
        UpdateAbacusLockStates(CurrentStep.Value);
    }

    private void OnSolvedChanged(bool oldVal, bool newVal)
    {
        ApplyRewardState(newVal);
    }

    private void ApplyRewardState(bool revealed)
    {
        if (keyItem == null) return;

        var renderers = keyItem.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers) r.enabled = revealed;

        var colliders = keyItem.GetComponentsInChildren<Collider>(true);
        foreach (var c in colliders) c.enabled = revealed;

        if (keyItem.TryGetComponent<GrabbableObject>(out var grabbable))
        {
            grabbable.enabled = revealed;
        }

        if (IsServer && keyItem.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = !revealed;
            rb.useGravity = revealed;
        }
    }

    private void InitPuzzleData()
    {
        int chosenIndex = UnityEngine.Random.Range(0, prebakedPool.Length);
        selectedDatasetIndex.Value = chosenIndex;

        RecalculateSolutions(chosenIndex);
    }

    private void RecalculateSolutions(int datasetIdx)
    {
        var data = prebakedPool[datasetIdx];
        int avg = (data.a + data.b + data.c + data.d) / 4;

        int diff1Sq = (data.a - avg) * (data.a - avg);
        int diff2Sq = (data.b - avg) * (data.b - avg);
        int diff3Sq = (data.c - avg) * (data.c - avg);
        int diff4Sq = (data.d - avg) * (data.d - avg);

        int sumSquares = diff1Sq + diff2Sq + diff3Sq + diff4Sq;
        int variance = sumSquares / 4;
        int stdDevUnderRoot = variance;

        targetSolutions[0] = avg;
        targetSolutions[1] = diff1Sq;
        targetSolutions[2] = diff2Sq;
        targetSolutions[3] = diff3Sq;
        targetSolutions[4] = diff4Sq;
        targetSolutions[5] = sumSquares;
        targetSolutions[6] = variance;
        targetSolutions[7] = stdDevUnderRoot;
    }

    // =========================================================================
    // G£ÓWNA INTERAKCJA Z DUCHEM
    // =========================================================================

    public void InteractWithGhost()
    {
        // 1. Pierwsza interakcja: rozpoczêcie zagadki
        if (!IsRiddleStarted.Value)
        {
            var data = prebakedPool[selectedDatasetIndex.Value];
            syncedNoteText.Value = $"Aby uruchomiæ mechanizm awaryjnego zasilania, musisz zrozumieæ, jak stabilne by³y moje ma³e stawkowe eksperymenty.\n\n" +
                                  $"Spójrz na historiê czterech identycznych turniejów z wygranymi:\n" +
                                  $"<b>{data.a},  {data.b},  {data.c},  {data.d}</b>\n\n" +
                                  $"Zacznij od obliczenia œredniej.";

            syncedFeedbackText.Value = "<color=yellow>Ustaw wartoœæ œredniej na pierwszym liczydle i zg³oœ siê do mnie.</color>";

            IsRiddleStarted.Value = true;
            PlayAudioClientRpc(3); // DŸwiêk powitania ducha
            return;
        }

        // 2. Jeœli zagadka zosta³a ju¿ w pe³ni ukoñczona
        if (CurrentStep.Value >= 8)
        {
            syncedFeedbackText.Value = "<color=white>Zasilanie dzia³a, szuflada jest ju¿ otwarta.</color>";
            return;
        }

        // 3. Kolejne interakcje: sprawdzanie aktualnego liczyd³a
        CheckCurrentStepSolution();
    }

    private void CheckCurrentStepSolution()
    {
        int step = CurrentStep.Value;
        Abacus currentAbacus = abacuses[step];
        if (currentAbacus == null) return;

        int playerValue = currentAbacus.CurrentValue;
        int expectedValue = targetSolutions[step];

        if (playerValue == expectedValue)
        {
            // SUKCES ETAPU
            PlayAudioClientRpc(0);
            int nextStep = step + 1;
            CurrentStep.Value = nextStep;

            if (nextStep < 8)
            {
                syncedFeedbackText.Value = $"<color=green>{stepNextInstructions[step]}</color>";
            }
            else
            {
                IsSolved.Value = true;
                syncedFeedbackText.Value = $"<color=green>{stepNextInstructions[7]}</color>";
                OnPuzzleCompleted();
            }
        }
        else
        {
            // B£¥D ETAPU: Reset liczyd³a do 0
            currentAbacus.ResetValue();
            PlayAudioClientRpc(1);
            syncedFeedbackText.Value = $"<color=red>Niepoprawna wartoœæ ({playerValue}). Koraliki zosta³y zresetowane. Spróbuj ponownie.</color>";
        }
    }

    private void HandleStepChanged(int oldStep, int newStep)
    {
        UpdateAbacusLockStates(newStep);
    }

    private void UpdateAbacusLockStates(int currentActiveStep)
    {
        // Przed pierwsz¹ rozmow¹ z duchem wszystkie liczyd³a s¹ zablokowane
        bool allowedToPlay = IsRiddleStarted.Value && currentActiveStep < 8;

        for (int i = 0; i < abacuses.Length; i++)
        {
            if (abacuses[i] == null) continue;
            abacuses[i].SetLocked(!allowedToPlay || i != currentActiveStep);
        }
    }

    private void OnPuzzleCompleted()
    {
        PlayAudioClientRpc(2);
    }

    [ClientRpc]
    private void PlayAudioClientRpc(int soundType)
    {
        if (audioSource == null) return;

        if (soundType == 0 && stepSuccessSound != null) audioSource.PlayOneShot(stepSuccessSound);
        else if (soundType == 1 && errorResetSound != null) audioSource.PlayOneShot(errorResetSound);
        else if (soundType == 2 && puzzleCompletedSound != null) audioSource.PlayOneShot(puzzleCompletedSound);
        else if (soundType == 3 && ghostInteractSound != null) audioSource.PlayOneShot(ghostInteractSound);
    }
}