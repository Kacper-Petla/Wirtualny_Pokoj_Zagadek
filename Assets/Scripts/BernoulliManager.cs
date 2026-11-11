using System;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class BernoulliManager : NetworkBehaviour
{
    [Serializable]
    public struct BernoulliDataSet
    {
        [TextArea(2, 4)]
        public string ghostSpeech;
        public string valN;      // Slot 0: n (np. "4")
        public string valK;      // Slot 1: k (np. "3")
        public string valP;      // Slot 2: p (np. "1/37")
        public string valQ;      // Slot 3: q (np. "36/37")
        public string[] distractors; // 6 fa³szywych wartoœci
    }

    [Header("Ghost & Dialogue")]
    [SerializeField] private TMP_Text ghostDialogueText;
    [SerializeField] private TMP_Text feedbackText;

    [Header("Formula Sockets (Tablica ze wzorem)")]
    [Tooltip("Slot 0 = n, Slot 1 = k, Slot 2 = p, Slot 3 = q")]
    [SerializeField] private BernoulliCardSocket[] formulaSockets = new BernoulliCardSocket[4];

    [Header("Table Cards (10 kart na stoliku)")]
    [SerializeField] private BernoulliCard[] tableCards = new BernoulliCard[10];

    [SerializeField] private GameObject keyItem;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip ghostInteractSound;
    [SerializeField] private AudioClip successSound;
    [SerializeField] private AudioClip errorResetSound;

    private readonly BernoulliDataSet[] datasets = new BernoulliDataSet[]
    {
        new BernoulliDataSet
        {
            ghostSpeech = "Obstawiam zawsze moj¹ szczêœliw¹ <b>siódemkê</b> w ruletce (1 z 37 liczb). Jeœli wypadnie dok³adnie <b>3 razy w 4 spinach</b>, bêdê wolny...\n" +
                          "Uzupe³nij schemat Bernoulliego na tablicy kartami ze sto³u.",
            valN = "4", valK = "3", valP = "1/37", valQ = "36/37",
            distractors = new string[] { "5", "2", "1", "7", "35/37", "1/38" }
        },
        new BernoulliDataSet
        {
            ghostSpeech = "Rzucam klasyczn¹ koœci¹ do gry. Uwolniê siê, jeœli <b>szóstka</b> wypadnie dok³adnie <b>2 razy w 5 rzutach</b>...\n" +
                          "Uzupe³nij schemat Bernoulliego na tablicy kartami ze sto³u.",
            valN = "5", valK = "2", valP = "1/6", valQ = "5/6",
            distractors = new string[] { "6", "3", "4", "1/5", "2/6", "1/36" }
        },
        new BernoulliDataSet
        {
            ghostSpeech = "Stawiam na czerwone w ruletce (18 z 37 pól). Wygram, gdy czerwone padnie dok³adnie <b>2 razy w 3 zakrêceniach</b>...\n" +
                          "Uzupe³nij schemat Bernoulliego na tablicy kartami ze sto³u.",
            valN = "3", valK = "2", valP = "18/37", valQ = "19/37",
            distractors = new string[] { "4", "1", "1/37", "36/37", "18/36", "2/3" }
        }
    };

    public readonly NetworkVariable<bool> IsPuzzleCompleted = new(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<int> selectedIndex = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<FixedString512Bytes> syncedSpeech = new(
        string.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<FixedString512Bytes> syncedFeedback = new(
        string.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        syncedSpeech.OnValueChanged += (o, n) => { if (ghostDialogueText != null) ghostDialogueText.text = n.ToString(); };
        syncedFeedback.OnValueChanged += (o, n) => { if (feedbackText != null) feedbackText.text = n.ToString(); };
        IsPuzzleCompleted.OnValueChanged += HandleCompletedChanged;

        ApplyRewardState(IsPuzzleCompleted.Value);

        if (ghostDialogueText != null && !syncedSpeech.Value.IsEmpty)
            ghostDialogueText.text = syncedSpeech.Value.ToString();

        if (feedbackText != null && !syncedFeedback.Value.IsEmpty)
            feedbackText.text = syncedFeedback.Value.ToString();

        if (IsServer)
        {
            InitPuzzle();
        }
    }

    private void InitPuzzle()
    {
        int chosen = UnityEngine.Random.Range(0, datasets.Length);
        selectedIndex.Value = chosen;

        var d = datasets[chosen];
        syncedSpeech.Value = d.ghostSpeech;
        syncedFeedback.Value = "Wstaw karty do wzoru i kliknij Ducha, aby sprawdziæ.";

        Debug.Log($"[Bernoulli] N: {d.valN}, K: {d.valK}, P: {d.valP}, Q: {d.valQ},");

        // Przygotowanie 10 wartoœci: 4 poprawne + 6 losowych dystraktorów
        List<string> pool = new List<string> { d.valN, d.valK, d.valP, d.valQ };
        pool.AddRange(d.distractors);

        // Tasowanie losowe (Fisher-Yates)
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int r = UnityEngine.Random.Range(0, i + 1);
            string tmp = pool[i];
            pool[i] = pool[r];
            pool[r] = tmp;
        }

        // Przypisanie do fizycznych kart na stoliku
        for (int i = 0; i < tableCards.Length && i < pool.Count; i++)
        {
            if (tableCards[i] != null)
            {
                tableCards[i].SetLabel(pool[i]);
            }
        }
    }

    // =========================================================================
    // SPRAWDZENIE ODPOWIEDZI (Interakcja z Duchem)
    // =========================================================================

    public void OnGhostClicked()
    {
        if (IsPuzzleCompleted.Value) return;

        var d = datasets[selectedIndex.Value];

        // Sprawdzamy czy ka¿dy slot ma poprawn¹ wartoœæ
        bool nOk = formulaSockets[0] != null && formulaSockets[0].CurrentValue == d.valN;
        bool kOk = formulaSockets[1] != null && formulaSockets[1].CurrentValue == d.valK;
        bool pOk = formulaSockets[2] != null && formulaSockets[2].CurrentValue == d.valP;
        bool qOk = formulaSockets[3] != null && formulaSockets[3].CurrentValue == d.valQ;

        if (nOk && kOk && pOk && qOk)
        {
            // SUKCES
            IsPuzzleCompleted.Value = true;
            syncedFeedback.Value = "<color=green>WZÓR POPRAWNY! DUCH JEST WOLNY. KLUCZ WYDANY.</color>";
            PlayAudio(1); // Sukces
        }
        else
        {
            // B£¥D - Wszystkie karty wracaj¹ na stolik
            ResetAllCards();
            syncedFeedback.Value = "<color=red>B£ÊDNY WZÓR! KARTY WRACAJ¥ NA STÓ£.</color>";
            PlayAudio(2); // B³¹d
        }
    }


    public void ResetAllCards()
    {
        if (IsPuzzleCompleted.Value) return;

        // Opró¿niamy socket tablicy
        foreach (var s in formulaSockets)
        {
            if (s != null && s.IsOccupied.Value)
            {
                s.EjectCard();
            }
        }

        // Wszystkie karty wracaj¹ do punktów startowych na stoliku
        foreach (var c in tableCards)
        {
            if (c != null)
            {
                c.ResetToOrigin();
            }
        }
    }

    private void HandleCompletedChanged(bool oldVal, bool newVal)
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

    private void PlayAudio(int soundId)
    {
        if (audioSource == null) return;
        if (soundId == 0 && ghostInteractSound != null) audioSource.PlayOneShot(ghostInteractSound);
        else if (soundId == 1 && successSound != null) audioSource.PlayOneShot(successSound);
        else if (soundId == 2 && errorResetSound != null) audioSource.PlayOneShot(errorResetSound);
    }
}