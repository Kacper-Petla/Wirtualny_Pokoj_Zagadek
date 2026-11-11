using System;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class SafeManager : NetworkBehaviour
{
    [Serializable]
    public struct BayesDataSet
    {
        public int regularPercent;        // x% (np. 60)
        public int regularWinChance;       // y% (np. 10)
        public int riggedWinChance;        // z% (np. 60)
        public int expectedResultPercent;  // Wynik P(Podkrêcony|Wygrana) w % (np. 80)
    }

    [SerializeField] private TMP_Text statusMessageText;
    [SerializeField] private TMP_Text tensDigitText;
    [SerializeField] private TMP_Text unitsDigitText;

    [SerializeField] private TMP_Text ghostDialogueText;

    [SerializeField] private Transform safeDoorTransform;
    [SerializeField] private Vector3 doorOpenEulerAngles = new Vector3(0f, -90f, 0f);
    [SerializeField] private GameObject keyItem;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip buttonClickSound;
    [SerializeField] private AudioClip errorLockSound;
    [SerializeField] private AudioClip safeUnlockSound;

    // Gotowe zestawy danych z idealnymi liczbami ca³kowitymi
    private readonly BayesDataSet[] datasets = new BayesDataSet[]
    {
        new BayesDataSet { regularPercent = 60, regularWinChance = 10, riggedWinChance = 60, expectedResultPercent = 80 },
        new BayesDataSet { regularPercent = 75, regularWinChance = 20, riggedWinChance = 60, expectedResultPercent = 50 },
        new BayesDataSet { regularPercent = 80, regularWinChance = 10, riggedWinChance = 60, expectedResultPercent = 60 },
        new BayesDataSet { regularPercent = 50, regularWinChance = 20, riggedWinChance = 60, expectedResultPercent = 75 },
        new BayesDataSet { regularPercent = 70, regularWinChance = 10, riggedWinChance = 70, expectedResultPercent = 75 }
    };

    // Stany sieciowe
    public readonly NetworkVariable<int> CurrentInputValue = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    public readonly NetworkVariable<bool> IsSafeOpen = new(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<int> selectedDatasetIndex = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<FixedString512Bytes> syncedGhostText = new(
        string.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<FixedString32Bytes> syncedStatusMessage = new(
        string.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private Quaternion doorClosedRotation;

    private void Awake()
    {
        if (safeDoorTransform != null) doorClosedRotation = safeDoorTransform.localRotation;
    }

    public override void OnNetworkSpawn()
    {
        CurrentInputValue.OnValueChanged += (o, n) => UpdateSafeScreen();
        syncedStatusMessage.OnValueChanged += (o, n) => UpdateSafeScreen();
        IsSafeOpen.OnValueChanged += HandleSafeOpenChanged;
        syncedGhostText.OnValueChanged += (o, n) => { if (ghostDialogueText != null) ghostDialogueText.text = n.ToString(); };

        if (ghostDialogueText != null && !syncedGhostText.Value.IsEmpty)
            ghostDialogueText.text = syncedGhostText.Value.ToString();

        ApplyRewardState(IsSafeOpen.Value);

        if (IsServer)
        {
            InitPuzzle();
        }

        UpdateSafeScreen();
        if (IsSafeOpen.Value) OpenDoorVisuals();
    }

    private void InitPuzzle()
    {
        int chosenIndex = UnityEngine.Random.Range(0, datasets.Length);
        selectedDatasetIndex.Value = chosenIndex;

        BayesDataSet data = datasets[chosenIndex];
        int riggedPercent = 100 - data.regularPercent;

        syncedGhostText.Value = $"W³aœciciel zdradzi³ mi, ¿e s¹ tu dwa typy automatów:\n\n" +
                               $"• <b>„Zwyk³e”</b>: stanowi¹ <b>{data.regularPercent}%</b> wszystkich i daj¹ <b>{data.regularWinChance}%</b> szansy na wygran¹.\n" +
                               $"• <b>„Podkrêcone”</b>: stanowi¹ pozosta³e <b>{riggedPercent}%</b> i maj¹ a¿ <b>{data.riggedWinChance}%</b> szansy.\n\n" +
                               $"W³aœnie pad³a wygrana na jednym z nich. Jakie jest prawdopodobieñstwo, ¿e ten automat jest <b>podkrêcony</b>?\n" +
                               $"WprowadŸ wynik w procentach na sejfie.";

        Debug.Log($"[Sejf] Kod: {data.expectedResultPercent}");
    }

    public void ChangeValue(int delta)
    {
        if (IsSafeOpen.Value) return;

        PlayAudio(0); // Click

        // Wyczyszczenie komunikatu b³êdu/sukcesu po klikniêciu przycisków cyfr
        if (!syncedStatusMessage.Value.IsEmpty)
        {
            syncedStatusMessage.Value = string.Empty;
        }

        int newVal = CurrentInputValue.Value + delta;

        // Zapêtlenie w przedziale 0 - 99%
        if (newVal < 0) newVal = 99;
        if (newVal > 99) newVal = 0;

        CurrentInputValue.Value = newVal;
    }

    public void SubmitCode()
    {
        if (IsSafeOpen.Value) return;

        BayesDataSet currentData = datasets[selectedDatasetIndex.Value];

        if (CurrentInputValue.Value == currentData.expectedResultPercent)
        {
            // SUKCES: Otwarcie sejfu
            syncedStatusMessage.Value = "DOBRY KOD";
            IsSafeOpen.Value = true;
            PlayAudio(2); // Safe unlock
            Debug.Log("[BayesSafe] Kod poprawny! Sejf otwarty.");
        }
        else
        {
            // B£¥D: Bez opóŸnienia, natychmiastowy komunikat
            syncedStatusMessage.Value = "Z£Y KOD";
            PlayAudio(1); // Error
        }
    }

    private void UpdateSafeScreen()
    {
        // 1. Aktualizacja cyfr (dziesi¹tki i jednoœci)
        int tens = CurrentInputValue.Value / 10;
        int units = CurrentInputValue.Value % 10;

        if (tensDigitText != null) tensDigitText.text = tens.ToString();
        if (unitsDigitText != null) unitsDigitText.text = units.ToString();

        // 2. Aktualizacja komunikatu statusu
        if (statusMessageText != null)
        {
            if (IsSafeOpen.Value)
            {
                statusMessageText.text = "<color=green>OTWARTY</color>";
            }
            else if (!syncedStatusMessage.Value.IsEmpty)
            {
                string msg = syncedStatusMessage.Value.ToString();
                statusMessageText.text = (msg == "DOBRY KOD")
                    ? "<color=green>DOBRY KOD</color>"
                    : "<color=red>Z£Y KOD</color>";
            }
            else
            {
                statusMessageText.text = string.Empty;
            }
        }
    }

    private void HandleSafeOpenChanged(bool oldVal, bool newVal)
    {
        if (newVal)
        {
            OpenDoorVisuals();
        }
    }

    private void OpenDoorVisuals()
    {
        UpdateSafeScreen();

        if (safeDoorTransform != null)
        {
            safeDoorTransform.localRotation = doorClosedRotation * Quaternion.Euler(doorOpenEulerAngles);
        }

        ApplyRewardState(true);
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

    private void PlayAudio(int soundId)
    {
        if (audioSource == null) return;
        if (soundId == 0 && buttonClickSound != null) audioSource.PlayOneShot(buttonClickSound);
        else if (soundId == 1 && errorLockSound != null) audioSource.PlayOneShot(errorLockSound);
        else if (soundId == 2 && safeUnlockSound != null) audioSource.PlayOneShot(safeUnlockSound);
    }
}