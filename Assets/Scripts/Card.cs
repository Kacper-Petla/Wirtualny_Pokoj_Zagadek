using UnityEngine;

public class Card : MonoBehaviour
{
    [Range(2, 10)]
    [SerializeField] private int cardValue = 2;

    public int CardValue => cardValue;
}