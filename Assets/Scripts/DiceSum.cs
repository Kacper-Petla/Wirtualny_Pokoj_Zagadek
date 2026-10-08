using TMPro;
using Unity.Netcode;
using UnityEngine;

public class DiceSum : NetworkBehaviour
{
    [SerializeField] private TMP_Text sumText;

    [SerializeField] private DiceController[] dice;

    public override void OnNetworkSpawn()
    {
        if (dice == null || dice.Length == 0) return;

        foreach (var die in dice)
        {
            if (die != null)
            {
                die.CurrentTopValue.OnValueChanged += HandleDiceValueChanged;
            }
        }

        UpdateSumDisplay();
    }

    public override void OnNetworkDespawn()
    {
        if (dice == null) return;

        foreach (var die in dice)
        {
            if (die != null)
            {
                die.CurrentTopValue.OnValueChanged -= HandleDiceValueChanged;
            }
        }
    }

    private void HandleDiceValueChanged(int oldValue, int newValue)
    {
        UpdateSumDisplay();
    }

    private void UpdateSumDisplay()
    {
        if (sumText == null || dice == null) return;

        int totalSum = 0;
        foreach (var die in dice)
        {
            if (die != null)
            {
                totalSum += die.CurrentTopValue.Value;
            }
        }

        sumText.text = $"{totalSum}";
    }
}