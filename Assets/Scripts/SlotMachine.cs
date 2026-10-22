using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class SlotMachine : NetworkBehaviour
{
    [SerializeField] private SlotReel reel1;
    [SerializeField] private SlotReel reel2;
    [SerializeField] private SlotReel reel3;

    public void SetupReelOptions(int[] targetCode)
    {
        if (targetCode == null) return;

        if (reel1 != null) reel1.SetupNumbers(GenerateReelNumbers(targetCode[0]));
        if (reel2 != null) reel2.SetupNumbers(GenerateReelNumbers(targetCode[1]));
        if (reel3 != null) reel3.SetupNumbers(GenerateReelNumbers(targetCode[2]));
    }

    public int[] GetCurrentValues()
    {
        int v1 = reel1 != null ? reel1.CurrentValue : -1;
        int v2 = reel2 != null ? reel2.CurrentValue : -1;
        int v3 = reel3 != null ? reel3.CurrentValue : -1;
        return new int[] { v1, v2, v3 };
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
}