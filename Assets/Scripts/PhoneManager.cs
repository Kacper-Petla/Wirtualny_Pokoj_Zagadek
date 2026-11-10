using System;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PhoneManager : NetworkBehaviour
{
    [Serializable]
    public struct SlotRiddleData
    {
        public int cost;
        public string win1Fraction;
        public int win1Amount;
        public string win2Fraction;
        public int win2Amount;
        public string win3Fraction;
        public int expectedNetProfit; // Oczekiwany zysk netto (wpisywany numer)
    }

    [SerializeField] private TMP_Text digit0Text;
    [SerializeField] private TMP_Text digit1Text;

    [SerializeField] private TMP_Text riddleText;

    [SerializeField] private TMP_Text feedbackText;

    [SerializeField] private MeshRenderer digit0BackgroundRenderer;
    [SerializeField] private MeshRenderer digit1BackgroundRenderer;

    [SerializeField] private Material normalMaterial;
    [SerializeField] private Material activeMaterial;

    [SerializeField] private GameObject keyItem;


    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip keyClickSound;
    [SerializeField] private AudioClip nextPositionSound;
    [SerializeField] private AudioClip successSound;
    [SerializeField] private AudioClip busyErrorSound;

    private readonly SlotRiddleData[] datasets = new SlotRiddleData[]
    {
        new SlotRiddleData { cost = 50, win1Fraction = "1/5", win1Amount = 150, win2Fraction = "3/10", win2Amount = 100, win3Fraction = "1/2", expectedNetProfit = 10 },
        new SlotRiddleData { cost = 40, win1Fraction = "1/4", win1Amount = 160, win2Fraction = "2/5",  win2Amount = 50,  win3Fraction = "7/20", expectedNetProfit = 20 },
        new SlotRiddleData { cost = 60, win1Fraction = "1/10", win1Amount = 300, win2Fraction = "1/2", win2Amount = 110, win3Fraction = "2/5", expectedNetProfit = 25 },
        new SlotRiddleData { cost = 30, win1Fraction = "1/5", win1Amount = 200, win2Fraction = "3/10", win2Amount = 100, win3Fraction = "1/2", expectedNetProfit = 40 },
        new SlotRiddleData { cost = 45, win1Fraction = "1/4", win1Amount = 120, win2Fraction = "3/10", win2Amount = 100, win3Fraction = "9/20", expectedNetProfit = 15 }
    };

    // Cyfry: [0] = dziesiπtki, [1] = jednoúci
    public readonly NetworkVariable<int> Digit0 = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public readonly NetworkVariable<int> Digit1 = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public readonly NetworkVariable<int> ActiveDigitIndex = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public readonly NetworkVariable<bool> IsUnlocked = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> selectedIndex = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<FixedString512Bytes> syncedRulesText = new(string.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<FixedString64Bytes> syncedFeedbackText = new(string.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        Digit0.OnValueChanged += (o, n) => UpdatePhoneDisplay();
        Digit1.OnValueChanged += (o, n) => UpdatePhoneDisplay();
        ActiveDigitIndex.OnValueChanged += (o, n) => UpdatePhoneDisplay();
        syncedFeedbackText.OnValueChanged += (o, n) => { if (feedbackText != null) feedbackText.text = n.ToString(); };
        syncedRulesText.OnValueChanged += (o, n) => { if (riddleText != null) riddleText.text = n.ToString(); };
        IsUnlocked.OnValueChanged += HandleUnlockedChanged;

        ApplyRewardState(IsUnlocked.Value);

        if (riddleText != null && !syncedRulesText.Value.IsEmpty)
            riddleText.text = syncedRulesText.Value.ToString();

        if (feedbackText != null && !syncedFeedbackText.Value.IsEmpty)
            feedbackText.text = syncedFeedbackText.Value.ToString();

        if (IsServer)
        {
            InitPuzzle();
        }

        UpdatePhoneDisplay();
    }

    private void InitPuzzle()
    {
        int chosen = UnityEngine.Random.Range(0, datasets.Length);
        selectedIndex.Value = chosen;

        var d = datasets[chosen];
        syncedRulesText.Value = $"<b>REGU£Y AUTOMATU:</b>\n\n" +
                               $"Koszt jednej gry: <b>{d.cost} monet</b>.\n" +
                               $"ï Szansa <b>{d.win1Fraction}</b> na wygranπ <b>{d.win1Amount} monet</b>\n" +
                               $"ï Szansa <b>{d.win2Fraction}</b> na wygranπ <b>{d.win2Amount} monet</b>\n" +
                               $"ï Szansa <b>{d.win3Fraction}</b>, øe nie wygrasz nic (0 monet)\n\n" +
                               $"Oblicz úredni <b>zysk netto</b> na jednej grze i zadzwoÒ pod ten numer na telefonie.";

        syncedFeedbackText.Value = "WPROWADè NUMER";

        Debug.Log($"[Phone] Cel: {d.expectedNetProfit}");
    }

    // =========================================================================
    // OBS£UGA PRZYCISK”W TELEFONU
    // =========================================================================


    public void ChangeActiveDigit(int delta)
    {
        if (IsUnlocked.Value) return;

        PlayAudio(0); // DüwiÍk klawisza

        if (ActiveDigitIndex.Value == 0)
        {
            int val = (Digit0.Value + delta + 10) % 10;
            Digit0.Value = val;
        }
        else
        {
            int val = (Digit1.Value + delta + 10) % 10;
            Digit1.Value = val;
        }

        syncedFeedbackText.Value = string.Empty;
    }

    public void NextDigitPosition()
    {
        if (IsUnlocked.Value) return;

        PlayAudio(1); // DüwiÍk zmiany pozycji
        ActiveDigitIndex.Value = (ActiveDigitIndex.Value + 1) % 2; // Prze≥πczenie 0 <-> 1
    }

    public void SubmitCall()
    {
        if (IsUnlocked.Value) return;

        int enteredNumber = (Digit0.Value * 10) + Digit1.Value;
        int target = datasets[selectedIndex.Value].expectedNetProfit;

        if (enteredNumber == target)
        {
            // SUKCES
            IsUnlocked.Value = true;
            syncedFeedbackText.Value = "<color=green>PO£•CZONO! ODBLOKOWANO.</color>";
            PlayAudio(2); // DüwiÍk sukcesu
        }
        else
        {
            // B£•D
            syncedFeedbackText.Value = "<color=red>NIE MA TAKIEGO NUMERU</color>";
            PlayAudio(3); // Sygna≥ zajÍtoúci
        }
    }

    private void UpdatePhoneDisplay()
    {
        // Aktualizacja cyfr
        if (digit0Text != null) digit0Text.text = Digit0.Value.ToString();
        if (digit1Text != null) digit1Text.text = Digit1.Value.ToString();

        // Aktualizacja materia≥Ûw t≥a
        if (IsUnlocked.Value)
        {
            SetBackgroundMaterial(digit0BackgroundRenderer, normalMaterial);
            SetBackgroundMaterial(digit1BackgroundRenderer, normalMaterial);
            digit0Text.color = Color.green;
            digit1Text.color = Color.green;
            return;
        }

        bool isZeroActive = ActiveDigitIndex.Value == 0;
        SetBackgroundMaterial(digit0BackgroundRenderer, isZeroActive ? activeMaterial : normalMaterial);
        SetBackgroundMaterial(digit1BackgroundRenderer, !isZeroActive ? activeMaterial : normalMaterial);
    }

    private void SetBackgroundMaterial(MeshRenderer renderer, Material mat)
    {
        if (renderer != null && mat != null)
        {
            renderer.sharedMaterial = mat;
        }
    }

    private void HandleUnlockedChanged(bool oldVal, bool newVal)
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
        if (soundId == 0 && keyClickSound != null) audioSource.PlayOneShot(keyClickSound);
        else if (soundId == 1 && nextPositionSound != null) audioSource.PlayOneShot(nextPositionSound);
        else if (soundId == 2 && successSound != null) audioSource.PlayOneShot(successSound);
        else if (soundId == 3 && busyErrorSound != null) audioSource.PlayOneShot(busyErrorSound);
    }
}