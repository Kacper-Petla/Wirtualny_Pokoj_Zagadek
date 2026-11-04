using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class KeypadManager : NetworkBehaviour
{
    [SerializeField] private TMP_Text keypadScreenText;
    [SerializeField] private TMP_Text leaderboardDisplayText;
    [SerializeField] private TMP_Text doorRiddleText;
    [SerializeField] private TMP_Text feedbackDisplayText;

    [SerializeField] private DoorLocked exitDoor;

    [Header("Lampka zasilania")]
    [SerializeField] private Renderer fuseStatusLampRenderer;
    [SerializeField] private Material lampOffMaterial;
    [SerializeField] private Material lampGreenMaterial;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip powerOnSound;
    [SerializeField] private AudioClip buttonClickSound;
    [SerializeField] private AudioClip successSound;
    [SerializeField] private AudioClip errorSound;

    public readonly NetworkVariable<bool> IsPowered = new(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    public readonly NetworkVariable<bool> IsDoorUnlocked = new(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    public readonly NetworkVariable<FixedString32Bytes> CurrentInputCode = new(
        string.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<FixedString512Bytes> SyncedLeaderboardText = new(
        string.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<FixedString128Bytes> SyncedDoorRiddleText = new(
        string.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<FixedString64Bytes> SyncedFeedbackText = new(
        string.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private int expectedTargetScore = 0;

    public override void OnNetworkSpawn()
    {
        IsPowered.OnValueChanged += OnPowerStateChanged;
        IsDoorUnlocked.OnValueChanged += OnDoorStateChanged;
        CurrentInputCode.OnValueChanged += (oldVal, newVal) => UpdateKeypadScreen(newVal.ToString());
        SyncedLeaderboardText.OnValueChanged += (oldVal, newVal) => { if (leaderboardDisplayText != null) leaderboardDisplayText.text = newVal.ToString(); };
        SyncedDoorRiddleText.OnValueChanged += (oldVal, newVal) => { if (doorRiddleText != null) doorRiddleText.text = newVal.ToString(); };
        SyncedFeedbackText.OnValueChanged += (oldVal, newVal) => { if (feedbackDisplayText != null) feedbackDisplayText.text = newVal.ToString(); };

        SetLampMaterial(IsPowered.Value);
        if (keypadScreenText != null) keypadScreenText.text = IsPowered.Value ? (CurrentInputCode.Value.IsEmpty ? "0" : CurrentInputCode.Value.ToString()) : "";
        if (leaderboardDisplayText != null) leaderboardDisplayText.text = SyncedLeaderboardText.Value.ToString();
        if (doorRiddleText != null) doorRiddleText.text = SyncedDoorRiddleText.Value.ToString();
        if (feedbackDisplayText != null) feedbackDisplayText.text = SyncedFeedbackText.Value.ToString();
    }

    public void PowerOn()
    {
        if (!IsServer || IsPowered.Value) return;

        IsPowered.Value = true;
        CurrentInputCode.Value = "0";
        SyncedFeedbackText.Value = string.Empty;

        GenerateLeaderboardAndRiddle();
        PlayAudio(0);
    }

    private void GenerateLeaderboardAndRiddle()
    {
        // 1. Generujemy 20 posortowanych rosn¹co liczb w zakresie [100, 5000]
        List<int> scores = new List<int>();
        scores.Add(5000); // 20. wynik zawsze wynosi 5000

        HashSet<int> generated = new HashSet<int> { 5000 };
        while (scores.Count < 20)
        {
            // Krok co 50 lub 100, aby wyniki by³y czytelnymi liczbami
            int val = UnityEngine.Random.Range(2, 99) * 50;
            if (generated.Add(val))
            {
                scores.Add(val);
            }
        }

        scores.Sort(); // Sortowanie rosn¹ce: scores[0] najmniejszy, scores[19] = 5000

        // 2. Losujemy centyl bêd¹cy wielokrotnoœci¹ 5 (od 30 do 95)
        int[] availablePercentiles = new int[] { 35, 40, 45, 50, 55, 60, 65, 70, 75, 80, 85, 90 };
        int chosenPercentile = availablePercentiles[UnityEngine.Random.Range(0, availablePercentiles.Length)];

        // Obliczenie pozycji progowej (1-based index)
        int rankIndex = Mathf.CeilToInt((chosenPercentile / 100f) * 20f); // 1 do 20
        expectedTargetScore = scores[rankIndex - 1]; // 0-based

        Debug.Log($"[PercentilePuzzle] Wybrany centyl: {chosenPercentile}%, Ranga: {rankIndex}, Oczekiwany wynik: {expectedTargetScore}");

        // 3. Budowanie tekstu tablicy wyników
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<b>== WYNIKI TURNIEJU (TOP 20) ==</b>");
        for (int i = 0; i < scores.Count; i++)
        {
            sb.AppendLine($"#{i + 1:D2} - {scores[i]} pkt");
        }
        SyncedLeaderboardText.Value = sb.ToString();

        // 4. Komunikat na drzwiach
        SyncedDoorRiddleText.Value = $"Wstêp tylko dla graczy, których wynik znajduje siê w <b>{chosenPercentile}.</b> centylu lub wy¿ej.";
    }

    public void OnKeypadDigitPressed(int digit)
    {
        if (!IsPowered.Value || IsDoorUnlocked.Value) return;
        DigitPressed(digit);
    }

    public void OnKeypadClearPressed()
    {
        if (!IsPowered.Value || IsDoorUnlocked.Value) return;
        ClearPressed();
    }

    public void OnKeypadSubmitPressed()
    {
        if (!IsPowered.Value || IsDoorUnlocked.Value) return;
        SubmitPressed();
    }


    private void DigitPressed(int digit)
    {
        PlayAudio(1);

        string current = CurrentInputCode.Value.ToString();
        if (current == "0" || string.IsNullOrEmpty(current))
        {
            current = digit.ToString();
        }
        else if (current.Length < 6)
        {
            current += digit.ToString();
        }

        CurrentInputCode.Value = current;
    }

    private void ClearPressed()
    {
        PlayAudio(1);
        CurrentInputCode.Value = "0";
        SyncedFeedbackText.Value = string.Empty;
    }

    private void SubmitPressed()
    {
        if (int.TryParse(CurrentInputCode.Value.ToString(), out int enteredVal))
        {
            if (enteredVal == expectedTargetScore)
            {
                if (exitDoor != null)
                {
                    exitDoor.UnlockDoor();
                }

                IsDoorUnlocked.Value = true;
                SyncedFeedbackText.Value = "<color=green>Odblokowano Drzwi</color>";
                PlayAudio(2);
                Debug.Log("[PercentilePuzzle] Drzwi odblokowane!");
            }
            else
            {
                CurrentInputCode.Value = "0";
                SyncedFeedbackText.Value = "<color=red>Niepoprawny kod</color>";
                PlayAudio(3);
            }
        }
    }

    private void OnPowerStateChanged(bool oldVal, bool newVal)
    {
        SetLampMaterial(newVal);
        if (!newVal && keypadScreenText != null) keypadScreenText.text = "";
    }

    private void OnDoorStateChanged(bool oldVal, bool newVal)
    {
        if (newVal && feedbackDisplayText != null)
        {
            feedbackDisplayText.text = "<color=green>Odblokowano Drzwi</color>";
        }
    }

    private void UpdateKeypadScreen(string code)
    {
        if (keypadScreenText != null)
        {
            keypadScreenText.text = string.IsNullOrEmpty(code) ? "0" : code;
        }
    }

    private void SetLampMaterial(bool powered)
    {
        if (fuseStatusLampRenderer != null)
        {
            fuseStatusLampRenderer.material = powered ? lampGreenMaterial : lampOffMaterial;
        }
    }

    private void PlayAudio(int soundId)
    {
        if (audioSource == null) return;
        if (soundId == 0 && powerOnSound != null) audioSource.PlayOneShot(powerOnSound);
        else if (soundId == 1 && buttonClickSound != null) audioSource.PlayOneShot(buttonClickSound);
        else if (soundId == 2 && successSound != null) audioSource.PlayOneShot(successSound);
        else if (soundId == 3 && errorSound != null) audioSource.PlayOneShot(errorSound);
    }
}