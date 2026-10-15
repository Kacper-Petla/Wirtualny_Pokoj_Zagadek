using System.Collections;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class CoinManager : NetworkBehaviour
{
    private const int TotalTokens = 20;
    private const int Denominator = 380;

    [SerializeField] private TMP_Text riddleDisplayText;

    [SerializeField] private CoinMachine redMachine;
    [SerializeField] private CoinMachine blueMachine;
    [SerializeField] private CoinMachine greenMachine;
    [SerializeField] private CoinMachine mainMachine;

    [SerializeField] private DoorItem keyItem;

    private string riddleText = string.Empty;
    private string winText = "<mark=#FFFFFF0F><color=green>Uda³o ci siê rozwi¹zaæ zagadkê. Odbierz swoj¹ nagrodê.</color></mark>";

    private int targetRed;
    private int targetBlue;
    private int targetGreen;
    private int targetMain;

    private readonly NetworkVariable<FixedString512Bytes> syncedRiddleText = new(
        string.Empty,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public readonly NetworkVariable<bool> IsSolved = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        syncedRiddleText.OnValueChanged += OnRiddleTextChanged;
        IsSolved.OnValueChanged += OnSolvedChanged;

        ApplyRewardState(IsSolved.Value);

        if (riddleDisplayText != null && !syncedRiddleText.Value.IsEmpty)
        {
            riddleDisplayText.text = syncedRiddleText.Value.ToString();
        }

        if (IsServer)
        {
            GeneratePuzzleData();
        }
    }

    public override void OnNetworkDespawn()
    {
        syncedRiddleText.OnValueChanged -= OnRiddleTextChanged;
        IsSolved.OnValueChanged -= OnSolvedChanged;
    }

    public void DisplayRiddle()
    {
        if (IsServer && !IsSolved.Value)
        {
            syncedRiddleText.Value = riddleText;
        }
    }

    private void OnRiddleTextChanged(FixedString512Bytes oldVal, FixedString512Bytes newVal)
    {
        if (riddleDisplayText != null)
        {
            riddleDisplayText.text = newVal.ToString();
        }
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

    public void VerifyMachine(CoinMachine machine)
    {
        if (!IsServer || machine == null) return;

        int score = machine.CurrentScore.Value;
        bool isCorrect = false;

        if (machine == redMachine)
        {
            isCorrect = (score == targetRed);
            Debug.Log($"[CoinManager] Sprawdzanie maszyny CZERWONEJ: {score} / {targetRed}");
        }
        else if (machine == blueMachine)
        {
            isCorrect = (score == targetBlue);
            Debug.Log($"[CoinManager] Sprawdzanie maszyny NIEBIESKIEJ: {score} / {targetBlue}");
        }
        else if (machine == greenMachine)
        {
            isCorrect = (score == targetGreen);
            Debug.Log($"[CoinManager] Sprawdzanie maszyny ZIELONEJ: {score} / {targetGreen}");
        }
        else if (machine == mainMachine)
        {
            isCorrect = (score == targetMain);
            Debug.Log($"[CoinManager] Sprawdzanie maszyny G£ÓWNEJ: {score} / {targetMain}");

            if (isCorrect && !IsSolved.Value)
            {
                IsSolved.Value = true;
                syncedRiddleText.Value = winText;
                Debug.Log("[CoinManager] Sukces! Maszyna g³ówna rozwi¹zana.");
            }
        }

        machine.SetResult(isCorrect);
    }

    private void GeneratePuzzleData()
    {
        // Losujemy liczby ¿etonów tak, by ka¿dy kolor mia³ sensown¹ reprezentacjê:
        // Czerwone: 4 do 9
        // Niebieskie: 4 do 9
        // Zielone: reszta (20 - R - B, min. 2)
        int red = UnityEngine.Random.Range(5, 9);
        int blue = UnityEngine.Random.Range(4, 15 - red);
        int green = TotalTokens - red - blue;

        // Obliczamy liczniki zdarzeñ sprzyjaj¹cych: n * (n - 1)
        targetRed = red * (red - 1);
        targetBlue = blue * (blue - 1);
        targetGreen = green * (green - 1);
        targetMain = targetRed + targetBlue + targetGreen;


        // Treœæ zagadki
        riddleText = $"<mark=#FFFFFF0F><color=white>Przed Tob¹ pojemnik. W œrodku spoczywa ³¹cznie 20 ¿etonów, z których:\n" +
                      $"• <color=red>{red} czerwonych</color>\n" +
                      $"• <color=lightblue>{blue} niebieskich</color>\n" +
                      $"• <color=green>{green} zielonych</color>\n\n" +
                      $"Jeœli bez patrzenia wyci¹gniesz kolejno dwa ¿etony, jakie jest prawdopodobieñstwo, ¿e oba ¿etony bêd¹ w tym samym kolorze?</color></mark>\n";

        syncedRiddleText.Value = riddleText;

        Debug.Log($"[ProbabilityPuzzle] Wylosowano: R={red}, B={blue}, G={green}");
        Debug.Log($"[ProbabilityPuzzle] Cele: R={targetRed}, B={targetBlue}, G={targetGreen} | G³ówny={targetMain}/{Denominator}");
    }
}