using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class SlotReel : NetworkBehaviour
{
    [SerializeField] private TMP_Text[] faceTexts = new TMP_Text[4];

    private Vector3 localRotationAxis = Vector3.back;

    private int[] pendingDigits = null;

    private readonly NetworkVariable<FixedString32Bytes> syncedDigits = new(
        "0123",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public readonly NetworkVariable<int> CurrentIndex = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public int CurrentValue
    {
        get
        {
            string s = syncedDigits.Value.ToString();
            if (string.IsNullOrEmpty(s) || CurrentIndex.Value >= s.Length) return 0;
            return s[CurrentIndex.Value] - '0';
        }
    }

    public void SetupNumbers(int[] digits)
    {
        if (digits == null || digits.Length < 4) return;

        // Jeœli obiekt jest ju¿ w pe³ni zespawnowany w Netcode:
        if (IsSpawned && IsServer)
        {
            syncedDigits.Value = $"{digits[0]}{digits[1]}{digits[2]}{digits[3]}";
            UpdateVisuals();
        }
        else
        {
            // Jeœli obiekt czeka jeszcze w kolejce sweepa, zapamiêtujemy dane
            pendingDigits = (int[])digits.Clone();
        }
    }

    public override void OnNetworkSpawn()
    {
        CurrentIndex.OnValueChanged += OnIndexChanged;
        syncedDigits.OnValueChanged += OnNumbersChanged;

        // Jeœli serwer otrzyma³ cyfry przed wejœciem w OnNetworkSpawn, aplikujemy je teraz
        if (IsServer && pendingDigits != null)
        {
            syncedDigits.Value = $"{pendingDigits[0]}{pendingDigits[1]}{pendingDigits[2]}{pendingDigits[3]}";
            pendingDigits = null;
        }

        UpdateVisuals();
        ApplyRotation(CurrentIndex.Value);
    }

    public override void OnNetworkDespawn()
    {
        CurrentIndex.OnValueChanged -= OnIndexChanged;
        syncedDigits.OnValueChanged -= OnNumbersChanged;
    }

    private void OnNumbersChanged(FixedString32Bytes oldVal, FixedString32Bytes newVal)
    {
        UpdateVisuals();
    }

    private void OnIndexChanged(int oldIndex, int newIndex)
    {
        ApplyRotation(newIndex);
    }

    private void UpdateVisuals()
    {
        string numbers = syncedDigits.Value.ToString();
        if (string.IsNullOrEmpty(numbers) || faceTexts == null) return;

        for (int i = 0; i < faceTexts.Length && i < numbers.Length; i++)
        {
            if (faceTexts[i] != null)
            {
                faceTexts[i].text = numbers[i].ToString();
            }
        }
    }

    public void RotateStep(int direction)
    {
        if (!IsServer) return;

        int nextIndex = (CurrentIndex.Value + direction) % 4;
        if (nextIndex < 0) nextIndex += 4;

        CurrentIndex.Value = nextIndex;
    }

    private void ApplyRotation(int index)
    {
        float targetAngle = index * -90f;
        transform.localRotation = Quaternion.AngleAxis(targetAngle, localRotationAxis);
    }
}