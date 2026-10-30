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
    [SerializeField] private TMP_Text noteDisplayText;
    [SerializeField] private TMP_Text statusFeedbackText;

    [Header("Abacuses (Dok³adnie 8 sztuk w kolejnoœci!)")]
    [Tooltip("0: Œrednia, 1-4: (xi - avg)^2, 5: Suma kwadratów, 6: Wariancja, 7: Odchylenie (wewn¹trz pierwiastka)")]
    [SerializeField] private Abacus[] abacuses = new Abacus[8];

    [Header("Reward / Szuflada")]
    [SerializeField] private Transform drawerTransform;
    [SerializeField] private Vector3 drawerOpenOffsetLocal = new Vector3(0f, 0f, 0.45f);
    [SerializeField] private GameObject leftFigurineObject;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip stepSuccessSound;
    [SerializeField] private AudioClip errorResetSound;
    [SerializeField] private AudioClip puzzleCompletedSound;

    // Pula zestawów danych daj¹cych ³adne liczby ca³kowite
    // Przyk³ad: [4, 6, 14, 16] -> Suma=40, Avg=10 -> Ró¿nice^2: 36, 16, 16, 36 -> Suma=104 -> Var=26
    // Przyk³ad: [10, 14, 26, 30] -> Suma=80, Avg=20 -> Ró¿nice^2: 100(za du¿o dla 99) -> U¿ywamy wartoœci z ró¿nicami <= 9:
    private readonly PresetDataSet[] prebakedPool = new PresetDataSet[]
    {
        new PresetDataSet { a = 3,  b = 7,  c = 13, d = 17 }, // Avg=10. Kwadraty: (7)^2=49, (3)^2=9, (3)^2=9, (7)^2=49. Suma=116 -> Var=29
        new PresetDataSet { a = 4,  b = 8,  c = 12, d = 16 }, // Avg=10. Kwadraty: 36, 4, 4, 36. Suma=80 -> Var=20
        new PresetDataSet { a = 12, b = 14, c = 18, d = 20 }, // Avg=16. Kwadraty: 16, 4, 4, 16. Suma=40 -> Var=10
        new PresetDataSet { a = 2,  b = 6,  c = 10, d = 14 }, // Avg=8.  Kwadraty: 36, 4, 4, 36. Suma=80 -> Var=20
        new PresetDataSet { a = 5,  b = 9,  c = 11, d = 15 }  // Avg=10. Kwadraty: 25, 1, 1, 25. Suma=52 -> Var=13
    };

    // Zmienne sieciowe stanu
    public readonly NetworkVariable<int> CurrentStep = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<int> selectedDatasetIndex = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<FixedString512Bytes> syncedNoteText = new(
        string.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    // Oczekiwane 8 wyników cz¹stkowych
    private int[] targetSolutions = new int[8];
    private Vector3 drawerClosedPos;

    private void Awake()
    {
        if (drawerTransform != null) drawerClosedPos = drawerTransform.localPosition;
    }

    public override void OnNetworkSpawn()
    {
        syncedNoteText.OnValueChanged += (oldVal, newVal) =>
        {
            if (noteDisplayText != null) noteDisplayText.text = newVal.ToString();
        };

        CurrentStep.OnValueChanged += HandleStepChanged;

        if (noteDisplayText != null && !syncedNoteText.Value.IsEmpty)
            noteDisplayText.text = syncedNoteText.Value.ToString();

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
        CurrentStep.OnValueChanged -= HandleStepChanged;
    }

    private void InitPuzzleData()
    {
        int chosenIndex = UnityEngine.Random.Range(0, prebakedPool.Length);
        selectedDatasetIndex.Value = chosenIndex;

        RecalculateSolutions(chosenIndex);

        var data = prebakedPool[chosenIndex];
        syncedNoteText.Value = $"Aby uruchomiæ mechanizm awaryjnego zasilania, musisz zrozumieæ, jak stabilne by³y moje ma³e stawkowe eksperymenty.\n\n" +
                              $"Spójrz na historiê czterech identycznych turniejów z wygranymi:\n" +
                              $"<b>{data.a},  {data.b},  {data.c},  {data.d}</b>\n\n" +
                              $"WprowadŸ sekwencyjnie kroki do obliczenia odchylenia standardowego tych stawek.";
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
        int stdDevUnderRoot = variance; // Odchylenie wpisujemy wewn¹trz pierwiastka sqrt(sigma^2)

        targetSolutions[0] = avg;
        targetSolutions[1] = diff1Sq;
        targetSolutions[2] = diff2Sq;
        targetSolutions[3] = diff3Sq;
        targetSolutions[4] = diff4Sq;
        targetSolutions[5] = sumSquares;
        targetSolutions[6] = variance;
        targetSolutions[7] = stdDevUnderRoot;

        Debug.Log($"[Puzzle] Target Solutions: Avg={avg}, Diffs=[{diff1Sq},{diff2Sq},{diff3Sq},{diff4Sq}], Sum={sumSquares}, Var={variance}");
    }

    // --- KLIKNIÊCIE PRZYCISKU ZATWIERDZAJ¥CEGO NA STOLE ---

    public void OnSubmitButtonPressed()
    {
        SubmitStepServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitStepServerRpc()
    {
        int step = CurrentStep.Value;
        if (step >= 8) return; // Ju¿ ukoñczono

        Abacus currentAbacus = abacuses[step];
        if (currentAbacus == null) return;

        int playerValue = currentAbacus.CurrentValue;
        int expectedValue = targetSolutions[step];

        if (playerValue == expectedValue)
        {
            // Sukces kroku
            PlayAudioClientRpc(0);
            CurrentStep.Value = step + 1;

            if (CurrentStep.Value >= 8)
            {
                OnPuzzleCompleted();
            }
        }
        else
        {
            // B£¥D: Resetujemy bie¿¹ce liczyd³o do 0
            currentAbacus.ResetValue();
            PlayAudioClientRpc(1);
            Debug.Log($"[Puzzle] B³¹d w kroku {step}! Gracz wpisa³: {playerValue}, oczekiwano: {expectedValue}. Reset liczyd³a.");
        }
    }

    private void HandleStepChanged(int oldStep, int newStep)
    {
        UpdateAbacusLockStates(newStep);

        if (newStep >= 8)
        {
            OpenDrawer();
        }
    }

    private void UpdateAbacusLockStates(int currentActiveStep)
    {
        for (int i = 0; i < abacuses.Length; i++)
        {
            if (abacuses[i] == null) continue;

            // Tylko aktualnie rozwi¹zywane liczyd³o jest odblokowane do klikania
            abacuses[i].SetLocked(i != currentActiveStep);
        }
    }

    private void OnPuzzleCompleted()
    {
        PlayAudioClientRpc(2);
        Debug.Log("[Puzzle] Zagadka ukoñczona! Otwieranie szuflady...");
    }

    private void OpenDrawer()
    {
        if (drawerTransform != null)
        {
            drawerTransform.localPosition = drawerClosedPos + drawerOpenOffsetLocal;
        }

        if (leftFigurineObject != null)
        {
            if (leftFigurineObject.TryGetComponent<GrabbableObject>(out var grabbable))
            {
                grabbable.enabled = true;
            }
        }
    }

    [ClientRpc]
    private void PlayAudioClientRpc(int soundType)
    {
        if (audioSource == null) return;

        if (soundType == 0 && stepSuccessSound != null) audioSource.PlayOneShot(stepSuccessSound);
        else if (soundType == 1 && errorResetSound != null) audioSource.PlayOneShot(errorResetSound);
        else if (soundType == 2 && puzzleCompletedSound != null) audioSource.PlayOneShot(puzzleCompletedSound);
    }
}