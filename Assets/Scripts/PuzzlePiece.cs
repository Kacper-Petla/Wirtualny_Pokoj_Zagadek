using Unity.Netcode;
using UnityEngine;

public class PuzzlePiece : NetworkBehaviour
{
    [SerializeField] private int pieceId;

    public int PieceId => pieceId;

    public readonly NetworkVariable<bool> IsPlaced = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private GrabbableObject grabbable;
    private Rigidbody rb;

    private void Awake()
    {
        grabbable = GetComponent<GrabbableObject>();
        rb = GetComponent<Rigidbody>();
    }

    public void SnapToSlot(Transform targetSocket)
    {
        if (!IsServer) return;

        if (TryGetComponent<GrabbableObject>(out var grabbable))
        {
            grabbable.IsResettable = false;
        }

        if (grabbable != null)
        {
            grabbable.Drop();
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        transform.position = targetSocket.position;
        transform.rotation = targetSocket.rotation;

        IsPlaced.Value = true;
    }
}