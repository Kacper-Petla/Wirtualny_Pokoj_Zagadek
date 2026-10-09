using System;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class RouletteManager : NetworkBehaviour
{
    private const int NumberCount = 9;

    [SerializeField] private RouletteChip targetChip;
    [SerializeField] private TMP_Text riddleDisplay;
    [SerializeField] private TMP_Text numbersDisplay;

    [SerializeField] private PuzzlePiece scenePuzzlePiece;
    [SerializeField] private Transform rouletteLidTransform;

    private float openHeightOffset = 1.5f; 
    private float openXOffset = 2.0f;
    private Vector3 openRotationEuler = new Vector3(90f, 0f, 0f);

    private string riddleText = $"<mark=#1E1E1Eff><color=white>Maszyna zaciê³a siê. Za ka¿dym razem wypada mediana z ostatnich {NumberCount} losowañ.</color></mark>";
    private string winMessage = "<mark=#1E1E1Eff><color=green>W³aœciwe pole obstawione.</color></mark>";
    private string loseMessage = "<mark=#1E1E1Eff><color=red>B³êdne pole obstawione.</color></mark>";
    private string noChipMessage = "<mark=#1E1E1Eff><color=red>¯eton nie znajduje siê na ¿adnym polu!</color></mark>";

    private readonly NetworkVariable<int> correctFieldValue = new(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public readonly NetworkVariable<bool> IsSolved = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<FixedString64Bytes> syncedNumbersText = new(
        string.Empty,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<FixedString512Bytes> syncedRiddleText = new(
        string.Empty,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Vector3 initialLidPosition;
    private Quaternion initialLidRotation;
    private Vector3 targetLidPosition;
    private Quaternion targetLidRotation;

    private void Awake()
    {
        if (rouletteLidTransform != null)
        {
            initialLidPosition = rouletteLidTransform.localPosition;
            initialLidRotation = rouletteLidTransform.localRotation;

            targetLidPosition = initialLidPosition + Vector3.forward * openXOffset + Vector3.up * openHeightOffset;
            targetLidRotation = initialLidRotation * Quaternion.Euler(openRotationEuler);
        }
    }

    public override void OnNetworkSpawn()
    {
        syncedNumbersText.OnValueChanged += OnNumbersChanged;
        syncedRiddleText.OnValueChanged += OnRiddleTextChanged;
        IsSolved.OnValueChanged += OnSolvedChanged;

        ApplyRewardState(IsSolved.Value);

        if (!syncedNumbersText.Value.IsEmpty && numbersDisplay != null)
        {
            numbersDisplay.text = syncedNumbersText.Value.ToString();
        }

        if (!syncedRiddleText.Value.IsEmpty && riddleDisplay != null)
        {
            riddleDisplay.text = syncedRiddleText.Value.ToString();
        }

        if (IsSolved.Value && rouletteLidTransform != null)
        {
            rouletteLidTransform.localPosition = targetLidPosition;
            rouletteLidTransform.localRotation = targetLidRotation;
        }

        if (IsServer)
        {
            GeneratePuzzle();
            syncedRiddleText.Value = riddleText;
        }
    }

    public override void OnNetworkDespawn()
    {
        syncedNumbersText.OnValueChanged -= OnNumbersChanged;
        syncedRiddleText.OnValueChanged -= OnRiddleTextChanged;
        IsSolved.OnValueChanged -= OnSolvedChanged;
    }

    public void DisplayRiddle()
    {
        syncedRiddleText.Value = riddleText;
    }

    private void OnNumbersChanged(FixedString64Bytes oldVal, FixedString64Bytes newVal)
    {
        if (numbersDisplay != null && !newVal.IsEmpty)
        {
            numbersDisplay.text = newVal.ToString();
        }
    }

    private void OnRiddleTextChanged(FixedString512Bytes oldVal, FixedString512Bytes newVal)
    {
        if (riddleDisplay != null && !newVal.IsEmpty)
        {
            riddleDisplay.text = newVal.ToString();
        }
    }

    private void OnSolvedChanged(bool oldVal, bool newVal)
    {
        if (newVal && rouletteLidTransform != null)
        {
            rouletteLidTransform.localPosition = targetLidPosition;
            rouletteLidTransform.localRotation = targetLidRotation;
        }
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

    private void GeneratePuzzle()
    {
        if (!IsServer) return;

        int[] generatedNumbers = new int[NumberCount];
        int[] sortedCopy = new int[NumberCount];

        for (int i = 0; i < NumberCount; i++)
        {
            int val = UnityEngine.Random.Range(0, 37);
            generatedNumbers[i] = val;
            sortedCopy[i] = val;
        }

        int med = NumberCount / 2;
        Array.Sort(sortedCopy);
        correctFieldValue.Value = sortedCopy[med];

        Debug.Log($"[Roulette] Wylosowano: {string.Join(", ", generatedNumbers)}");
        Debug.Log($"[Roulette] Posortowane: {string.Join(", ", sortedCopy)} | Mediana (Cel): {correctFieldValue.Value}");

        syncedNumbersText.Value = string.Join("  ", generatedNumbers);
    }

    public void Verify()
    {
        if (IsSolved.Value)
        {
            Debug.Log("[Roulette] Zagadka ju¿ rozwi¹zana.");
            return;
        }

        if (targetChip == null || targetChip.CurrentField == null)
        {
            Debug.Log("[Roulette] ¯eton nie le¿y na ¿adnym polu!");
            syncedRiddleText.Value = noChipMessage;
            return;
        }

        int placedValue = targetChip.CurrentField.FieldValue;
        Debug.Log($"[Roulette] Sprawdzanie pola: {placedValue} (Wymagane: {correctFieldValue.Value})");

        if (placedValue == correctFieldValue.Value)
        {
            IsSolved.Value = true;
            Debug.Log("[Roulette] SUKCES! W³aœciwe pole obstawione.");
            syncedRiddleText.Value = winMessage;
        }
        else
        {
            Debug.Log("[Roulette] B£¥D! Z³e pole.");
            syncedRiddleText.Value = loseMessage;
        }
    }
}