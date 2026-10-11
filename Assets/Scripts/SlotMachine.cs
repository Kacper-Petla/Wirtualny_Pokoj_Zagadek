using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class SlotMachine : NetworkBehaviour
{
    [SerializeField] private SlotReel reel1;
    [SerializeField] private SlotReel reel2;
    [SerializeField] private SlotReel reel3;

    private int[] currentTargetCode = new int[3] { 1, 2, 3 };

    public override void OnNetworkSpawn()
    {
        if (reel1 != null) reel1.CurrentIndex.OnValueChanged += OnReelChanged;
        if (reel2 != null) reel2.CurrentIndex.OnValueChanged += OnReelChanged;
        if (reel3 != null) reel3.CurrentIndex.OnValueChanged += OnReelChanged;

        UpdateDisplay();
    }

    public override void OnNetworkDespawn()
    {
        if (reel1 != null) reel1.CurrentIndex.OnValueChanged -= OnReelChanged;
        if (reel2 != null) reel2.CurrentIndex.OnValueChanged -= OnReelChanged;
        if (reel3 != null) reel3.CurrentIndex.OnValueChanged -= OnReelChanged;
    }

    public void SetupReelOptions(int[] targetCode)
    {
        currentTargetCode = (int[])targetCode.Clone();

        if (reel1 != null) reel1.SetupNumbers(GenerateReelNumbers(targetCode[0]));
        if (reel2 != null) reel2.SetupNumbers(GenerateReelNumbers(targetCode[1]));
        if (reel3 != null) reel3.SetupNumbers(GenerateReelNumbers(targetCode[2]));
    }

    private int[] GenerateReelNumbers(int targetDigit)
    {
        List<int> digits = new List<int> { targetDigit };

        while (digits.Count < 4)
        {
            int candidate = UnityEngine.Random.Range(0, 10);
            if (!digits.Contains(candidate))
            {
                digits.Add(candidate);
            }
        }

        for (int i = digits.Count - 1; i > 0; i--)
        {
            int r = UnityEngine.Random.Range(0, i + 1);
            (digits[i], digits[r]) = (digits[r], digits[i]);
        }

        return digits.ToArray();
    }

    private void OnReelChanged(int oldIdx, int newIdx)
    {
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (reel1 == null || reel2 == null || reel3 == null) return;

        int val1 = reel1.CurrentValue;
        int val2 = reel2.CurrentValue;
        int val3 = reel3.CurrentValue;
    }

    public bool VerifyCode()
    {
        if (!IsServer) return false;
        if (reel1 == null || reel2 == null || reel3 == null) return false;

        int val1 = reel1.CurrentValue;
        int val2 = reel2.CurrentValue;
        int val3 = reel3.CurrentValue;


        if (val1 == currentTargetCode[0] && val2 == currentTargetCode[1] && val3 == currentTargetCode[2])
        {
            Debug.Log("[SlotMachine] SUKCES: Kod poprawny!");
            return true;
        }
        else
        {
            Debug.Log("[SlotMachine] B£¥D: Z³a kombinacja.");
            return false;
        }
    }
}