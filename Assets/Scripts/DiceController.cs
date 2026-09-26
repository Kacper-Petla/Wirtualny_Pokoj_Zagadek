using Unity.Netcode;
using UnityEngine;

public class DiceController : NetworkBehaviour
{
    private readonly (Vector3 localDir, int value)[] faceMappings = new[]
    {
        (Vector3.up, 4),
        (Vector3.down, 3),
        (Vector3.forward, 6),
        (Vector3.back, 1),
        (Vector3.right, 2),
        (Vector3.left, 5)
    };

    public readonly NetworkVariable<int> CurrentTopValue = new(
        6,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            UpdateTopValue();
        }
    }

    public void RotateDice(Vector3 axis, float angle)
    {
        transform.rotation = Quaternion.AngleAxis(angle, axis) * transform.rotation;

        UpdateTopValue();
    }

    private void UpdateTopValue()
    {
        float bestDot = -1f;
        int bestValue = 1;

        foreach (var mapping in faceMappings)
        {
            Vector3 worldDir = transform.TransformDirection(mapping.localDir);
            float dot = Vector3.Dot(worldDir, Vector3.up);

            if (dot > bestDot)
            {
                bestDot = dot;
                bestValue = mapping.value;
            }
        }

        CurrentTopValue.Value = bestValue;
    }
}