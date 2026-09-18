using UnityEngine;

public class CoinToken : MonoBehaviour
{
    [SerializeField] private int coinValue = 1;

    public int Value => coinValue;
}