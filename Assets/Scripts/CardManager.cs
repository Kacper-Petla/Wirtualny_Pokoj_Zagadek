using System;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class CardManager : NetworkBehaviour
{
    [Serializable]
    public struct ConditionalRiddleData
    {
        [TextArea(2, 4)]
        public string riddleDescription;
        public int expectedDenominator; // Oczekiwana suma kart (mianownik)
    }

    [Header("Displays")]
    [SerializeField] private TMP_Text riddleDisplayText;
    [SerializeField] private TMP_Text statusFeedbackText;

    [Header("Drop Zone Reference")]
    [SerializeField] private CardContainer cardDropZone;

    [Header("Reward")]
    [Tooltip("Przedmiot aktywuj¹cy kolejn¹ zagadkê (np. klucz, bateria, bezpiecznik)")]
    [SerializeField] private GameObject rewardItem;
    [SerializeField] private Transform rewardSpawnPoint;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip riddleVoiceClip;
    [SerializeField] private AudioClip successSound;
    [SerializeField] private AudioClip errorSound;

    private readonly ConditionalRiddleData[] prebakedRiddles = new ConditionalRiddleData[]
    {
        new ConditionalRiddleData
        {
            riddleDescription = "Wylosowa³em jedn¹ z 52 klasycznych kart. Zdradzê Ci sekret... to na pewno jest <b>figura</b> (Walet, Dama, Król).\n" +
                                "Jakie jest prawdopodobieñstwo, ¿e karta le¿¹ca przed Tob¹ to <b>Dama Kier</b>?\n\n" +
                                "U³ó¿ na stole karty pomocnicze sumuj¹ce siê do <b>mianownika</b> u³amka.",
            expectedDenominator = 12
        },
        new ConditionalRiddleData
        {
            riddleDescription = "Wylosowa³em jedn¹ z 52 klasycznych kart. Wiem na pewno, ¿e to <b>figura lub As</b> (Walet, Dama, Król, As).\n" +
                                "Jakie jest prawdopodobieñstwo, ¿e karta le¿¹ca przed Tob¹ to <b>As Pik</b>?\n\n" +
                                "U³ó¿ na stole karty pomocnicze sumuj¹ce siê do <b>mianownika</b> u³amka.",
            expectedDenominator = 16
        },
        new ConditionalRiddleData
        {
            riddleDescription = "Wylosowa³em jedn¹ z 52 klasycznych kart. Zdradzê Ci sekret... to na pewno karta <b>czerwona</b> (Kier lub Karo).\n" +
                                "Jakie jest prawdopodobieñstwo, ¿e karta le¿¹ca przed Tob¹ to <b>Król Karo</b>?\n\n" +
                                "U³ó¿ na stole karty pomocnicze sumuj¹ce siê do <b>mianownika</b> u³amka.",
            expectedDenominator = 26
        },
        new ConditionalRiddleData
        {
            riddleDescription = "Wylosowa³em jedn¹ z 52 klasycznych kart. To na pewno <b>czerwona figura</b> (Walet, Dama lub Król w kolorze Kier/Karo).\n" +
                                "Jakie jest prawdopodobieñstwo, ¿e karta le¿¹ca przed Tob¹ to <b>Walet Kier</b>?\n\n" +
                                "U³ó¿ na stole karty pomocnicze sumuj¹ce siê do <b>mianownika</b> u³amka.",
            expectedDenominator = 6
        },
        new ConditionalRiddleData
        {
            riddleDescription = "Wylosowa³em jedn¹ z 52 klasycznych kart. Zdradzê Ci sekret... to na pewno <b>jeden z Króli</b>.\n" +
                                "Jakie jest prawdopodobieñstwo, ¿e karta le¿¹ca przed Tob¹ to <b>Król Trefl</b>?\n\n" +
                                "U³ó¿ na stole karty pomocnicze sumuj¹ce siê do <b>mianownika</b> u³amka.",
            expectedDenominator = 4
        }
    };

    public readonly NetworkVariable<bool> IsPuzzleCompleted = new(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<int> selectedRiddleIndex = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<FixedString512Bytes> syncedRiddleText = new(
        string.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<FixedString64Bytes> syncedFeedbackText = new(
        string.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        syncedRiddleText.OnValueChanged += (o, n) => { if (riddleDisplayText != null) riddleDisplayText.text = n.ToString(); };
        syncedFeedbackText.OnValueChanged += (o, n) => { if (statusFeedbackText != null) statusFeedbackText.text = n.ToString(); };
        IsPuzzleCompleted.OnValueChanged += HandleCompletedChanged;

        if (riddleDisplayText != null && !syncedRiddleText.Value.IsEmpty)
            riddleDisplayText.text = syncedRiddleText.Value.ToString();

        if (statusFeedbackText != null && !syncedFeedbackText.Value.IsEmpty)
            statusFeedbackText.text = syncedFeedbackText.Value.ToString();

        if (IsServer)
        {
            InitPuzzle();
        }

        if (IsPuzzleCompleted.Value)
        {
            UnlockRewardVisuals();
        }
    }

    private void InitPuzzle()
    {
        int chosen = UnityEngine.Random.Range(0, prebakedRiddles.Length);
        selectedRiddleIndex.Value = chosen;

        syncedRiddleText.Value = prebakedRiddles[chosen].riddleDescription;
        syncedFeedbackText.Value = "<color=yellow>U³ó¿ odpowiedni¹ sumê kart i naciœnij dzwonek/przycisk.</color>";
    }

    // --- SPRAWDZENIE ODPOWIEDZI ---

    public void OnSubmitButtonPressed()
    {
        if (IsPuzzleCompleted.Value || cardDropZone == null) return;

        int currentSum = cardDropZone.CurrentTotalSum.Value;
        int expected = prebakedRiddles[selectedRiddleIndex.Value].expectedDenominator;

        if (currentSum == expected)
        {
            // SUKCES
            IsPuzzleCompleted.Value = true;
            syncedFeedbackText.Value = $"<color=green>POPRAWNY MIANOWNIK (1/{expected})! PRZEDMIOT ODBLOKOWANY.</color>";
            PlayAudio(1); // Sukces
            UnlockReward();
        }
        else
        {
            // B£¥D
            syncedFeedbackText.Value = $"<color=red>B£¥D: Suma to {currentSum}, a nie poprawny mianownik.</color>";
            PlayAudio(2); // B³¹d
        }
    }

    private void UnlockReward()
    {
        UnlockRewardVisuals();
    }

    private void HandleCompletedChanged(bool oldVal, bool newVal)
    {
        if (newVal)
        {
            UnlockRewardVisuals();
        }
    }

    private void UnlockRewardVisuals()
    {
        if (rewardItem != null)
        {
            rewardItem.SetActive(true);

            if (rewardSpawnPoint != null)
            {
                rewardItem.transform.position = rewardSpawnPoint.position;
                rewardItem.transform.rotation = rewardSpawnPoint.rotation;
            }

            if (rewardItem.TryGetComponent<GrabbableObject>(out var grabbable))
            {
                grabbable.enabled = true;
            }

            if (rewardItem.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.isKinematic = false;
            }
        }
    }

    private void PlayAudio(int soundId)
    {
        if (audioSource == null) return;
        if (soundId == 1 && successSound != null) audioSource.PlayOneShot(successSound);
        else if (soundId == 2 && errorSound != null) audioSource.PlayOneShot(errorSound);
    }
}