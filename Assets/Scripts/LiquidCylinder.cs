using Unity.Netcode;
using UnityEngine;

public class LiquidCylinder : NetworkBehaviour
{
    [Header("Visual Column (P³yn)")]
    [SerializeField] private Transform liquidColumnTransform;
    [SerializeField] private Vector3 minLocalPos = Vector3.zero;
    [SerializeField] private Vector3 maxLocalPos = new Vector3(0f, 0.001f, 0f);

    [Header("Value Range")]
    [Tooltip("Minimalna wartoœæ (np. 0)")]
    [SerializeField] private int minValue = 0;
    [Tooltip("Maksymalna wartoœæ (np. 50 lub 5)")]
    [SerializeField] private int maxValue = 50;
    [Tooltip("Krok pojedynczego klikniêcia (np. 10 dla cylindra dziesi¹tek, 1 dla jednoœci)")]
    [SerializeField] private int stepValue = 10;

    public readonly NetworkVariable<int> CurrentValue = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    public int Value => CurrentValue.Value;

    public override void OnNetworkSpawn()
    {
        CurrentValue.OnValueChanged += (oldVal, newVal) => UpdateVisualLevel(newVal);
        UpdateVisualLevel(CurrentValue.Value);
    }

    public void IncreaseLevel()
    {
        ChangeLevel(stepValue);
    }

    public void DecreaseLevel()
    {
        ChangeLevel(-stepValue);
    }

    private void ChangeLevel(int delta)
    {
        int next = CurrentValue.Value + delta;

        if (next > maxValue)
        {
            next = minValue;
        }
        else if (next < minValue)
        {
            next = minValue;
        }

        CurrentValue.Value = next;
    }

    public void ResetValue()
    {
        if (!IsServer) return;
        CurrentValue.Value = minValue;
    }

    private void UpdateVisualLevel(int value)
    {
        if (liquidColumnTransform == null) return;

        float t = Mathf.InverseLerp(minValue, maxValue, value);
        liquidColumnTransform.localPosition = Vector3.Lerp(minLocalPos, maxLocalPos, t);
    }
}