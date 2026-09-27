using TMPro;
using Unity.Netcode;
using UnityEngine;

public class SlotMachineManager : NetworkBehaviour
{
    [System.Serializable]
    public struct ReelConfig
    {
        public SlotReel reel;
        public int[] numbers;
    }

    [SerializeField] private ReelConfig reel1Config = new ReelConfig { numbers = new int[4] { 1, 2, 3, 4 } };
    [SerializeField] private ReelConfig reel2Config = new ReelConfig { numbers = new int[4] { 2, 4, 6, 8 } };
    [SerializeField] private ReelConfig reel3Config = new ReelConfig { numbers = new int[4] { 1, 3, 5, 7 } };

    [SerializeField] private int[] targetCode = new int[3] { 4, 6, 5 };

    private void Awake()
    {
        if (reel1Config.reel != null) reel1Config.reel.SetupNumbers(reel1Config.numbers);
        if (reel2Config.reel != null) reel2Config.reel.SetupNumbers(reel2Config.numbers);
        if (reel3Config.reel != null) reel3Config.reel.SetupNumbers(reel3Config.numbers);
    }

    public override void OnNetworkSpawn()
    {
        if (reel1Config.reel != null) reel1Config.reel.CurrentIndex.OnValueChanged += OnReelChanged;
        if (reel2Config.reel != null) reel2Config.reel.CurrentIndex.OnValueChanged += OnReelChanged;
        if (reel3Config.reel != null) reel3Config.reel.CurrentIndex.OnValueChanged += OnReelChanged;

        UpdateDisplayAndCheck();
    }

    public override void OnNetworkDespawn()
    {
        if (reel1Config.reel != null) reel1Config.reel.CurrentIndex.OnValueChanged -= OnReelChanged;
        if (reel2Config.reel != null) reel2Config.reel.CurrentIndex.OnValueChanged -= OnReelChanged;
        if (reel3Config.reel != null) reel3Config.reel.CurrentIndex.OnValueChanged -= OnReelChanged;
    }

    private void OnReelChanged(int oldIdx, int newIdx)
    {
        UpdateDisplayAndCheck();
    }

    private void UpdateDisplayAndCheck()
    {
        if (reel1Config.reel == null || reel2Config.reel == null || reel3Config.reel == null) return;

        int val1 = reel1Config.reel.CurrentValue;
        int val2 = reel2Config.reel.CurrentValue;
        int val3 = reel3Config.reel.CurrentValue;

        if (IsServer)
        {
            if (val1 == targetCode[0] && val2 == targetCode[1] && val3 == targetCode[2])
            {
                Debug.Log("[SlotMachine] ZAGADKA ROZWI¥ZANA!");
            }
        }
    }
}