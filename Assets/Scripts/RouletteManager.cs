using Unity.Netcode;
using UnityEngine;

public class RouletteManager : NetworkBehaviour
{
    [SerializeField] private RouletteChip targetChip;
    [SerializeField] private int correctFieldValue = 17;


    public void Verify()
    {
        if (targetChip == null || targetChip.CurrentField == null)
        {
            Debug.Log("[Roulette] ¯eton nie le¿y na ¿adnym polu!");
            return;
        }

        int placedValue = targetChip.CurrentField.FieldValue;
        Debug.Log($"[Roulette] Sprawdzanie pola: {placedValue} (Wymagane: {correctFieldValue})");

        if (placedValue == correctFieldValue)
        {
            Debug.Log("[Roulette] SUKCES! W³aœciwe pole obstawione.");
        }
        else
        {
            Debug.Log("[Roulette] B£¥D! Z³e pole.");
        }
    }
}