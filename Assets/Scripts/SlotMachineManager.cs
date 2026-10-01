using TMPro;
using Unity.Netcode;
using UnityEngine;

public class SlotMachineManager : NetworkBehaviour
{
    [SerializeField] private SlotReel reel1;
    [SerializeField] private SlotReel reel2;
    [SerializeField] private SlotReel reel3;

    public struct ReelConfig
    {
        public SlotReel reel;
        public int[] numbers;
    }

    private int[] targetCode = new int[3] { 1, 2, 3 };

    private ReelConfig reel1Config;
    private ReelConfig reel2Config;
    private ReelConfig reel3Config;

    private void Awake()
    {
        reel1Config = new ReelConfig { reel = reel1, numbers = new int[4] { 1, 2, 3, 4 } };
        reel2Config = new ReelConfig { reel = reel2, numbers = new int[4] { 2, 4, 6, 8 } };
        reel3Config = new ReelConfig { reel = reel3, numbers = new int[4] { 1, 3, 5, 7 } };

        if (reel1Config.reel != null) reel1Config.reel.SetupNumbers(reel1Config.numbers);
        if (reel2Config.reel != null) reel2Config.reel.SetupNumbers(reel2Config.numbers);
        if (reel3Config.reel != null) reel3Config.reel.SetupNumbers(reel3Config.numbers);
    }

    public override void OnNetworkSpawn()
    {
        if (reel1Config.reel != null) reel1Config.reel.CurrentIndex.OnValueChanged += OnReelChanged;
        if (reel2Config.reel != null) reel2Config.reel.CurrentIndex.OnValueChanged += OnReelChanged;
        if (reel3Config.reel != null) reel3Config.reel.CurrentIndex.OnValueChanged += OnReelChanged;

        UpdateDisplay();
    }

    public override void OnNetworkDespawn()
    {
        if (reel1Config.reel != null) reel1Config.reel.CurrentIndex.OnValueChanged -= OnReelChanged;
        if (reel2Config.reel != null) reel2Config.reel.CurrentIndex.OnValueChanged -= OnReelChanged;
        if (reel3Config.reel != null) reel3Config.reel.CurrentIndex.OnValueChanged -= OnReelChanged;
    }

    private void OnReelChanged(int oldIdx, int newIdx)
    {
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (reel1Config.reel == null || reel2Config.reel == null || reel3Config.reel == null) return;

        int val1 = reel1Config.reel.CurrentValue;
        int val2 = reel2Config.reel.CurrentValue;
        int val3 = reel3Config.reel.CurrentValue;
    }

    public void CheckSolution()
    {
        if (!IsServer) return;
        if (reel1Config.reel == null || reel2Config.reel == null || reel3Config.reel == null) return;

        int val1 = reel1Config.reel.CurrentValue;
        int val2 = reel2Config.reel.CurrentValue;
        int val3 = reel3Config.reel.CurrentValue;


        if (val1 == targetCode[0] && val2 == targetCode[1] && val3 == targetCode[2])
        {
            Debug.Log("[SlotMachine] SUKCES: Kod poprawny!");
        }
        else
        {
            Debug.Log("[SlotMachine] B£¥D: Z³a kombinacja.");
        }
    }
}