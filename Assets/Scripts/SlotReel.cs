using TMPro;
using Unity.Netcode;
using UnityEngine;

public class SlotReel : NetworkBehaviour
{
    [SerializeField] private Vector3 localRotationAxis = Vector3.right;

    [SerializeField] private TMP_Text[] faceTexts = new TMP_Text[4];

    private int[] reelNumbers = new int[4];

    public readonly NetworkVariable<int> CurrentIndex = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public int CurrentValue => (reelNumbers != null && reelNumbers.Length > CurrentIndex.Value)
        ? reelNumbers[CurrentIndex.Value]
        : 0;

    public void SetupNumbers(int[] numbers)
    {
        reelNumbers = numbers;

        if (faceTexts != null && numbers != null)
        {
            for (int i = 0; i < faceTexts.Length && i < numbers.Length; i++)
            {
                if (faceTexts[i] != null)
                {
                    faceTexts[i].text = numbers[i].ToString();
                }
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        CurrentIndex.OnValueChanged += OnIndexChanged;
        ApplyRotation(CurrentIndex.Value);
    }

    public override void OnNetworkDespawn()
    {
        CurrentIndex.OnValueChanged -= OnIndexChanged;
    }

    private void OnIndexChanged(int oldIndex, int newIndex)
    {
        ApplyRotation(newIndex);
    }

    public void RotateStep(int direction)
    {
        int nextIndex = (CurrentIndex.Value + direction) % 4;
        if (nextIndex < 0) nextIndex += 4;

        CurrentIndex.Value = nextIndex;
    }

    private void ApplyRotation(int index)
    {
        float targetAngle = index * 90f;
        transform.localRotation = Quaternion.AngleAxis(targetAngle, localRotationAxis);
    }
}