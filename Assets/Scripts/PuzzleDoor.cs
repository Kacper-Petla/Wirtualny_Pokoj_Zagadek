using Unity.Netcode;
using UnityEngine;

public class PuzzleDoor : NetworkBehaviour, IInteractable
{
    private const int TotalPieces = 4;

    [SerializeField] private Transform[] pieceSockets = new Transform[TotalPieces];
    [SerializeField] private GrabbableObject[] PuzzlePieces = new GrabbableObject[TotalPieces];

    private readonly bool[] placedPieces = new bool[TotalPieces];

    public readonly NetworkVariable<bool> IsDoorUnlocked = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || IsDoorUnlocked.Value) return;

        if (other.TryGetComponent<PuzzlePiece>(out var puzzle))
        {
            int id = puzzle.PieceId;

            if (id >= 0 && id < TotalPieces && !placedPieces[id])
            {
                placedPieces[id] = true;

                puzzle.SnapToSlot(pieceSockets[id]);

                Debug.Log($"[PuzzleDoor] Wpasowano puzel ID: {id} ({CountPlaced()}/{TotalPieces})");
            }
        }
    }

    public void Interact()
    {
        if (IsDoorUnlocked.Value) return;

        if (CountPlaced() == TotalPieces)
        {
            IsDoorUnlocked.Value = true;
            Debug.Log("[PuzzleDoor] SUKCES! Wszystkie 4 puzzle s¹ na miejscu.");

            // =========================================================================
        }
        else
        {
            Debug.Log($"[PuzzleDoor] Drzwi zablokowane. Brakuje jeszcze {TotalPieces - CountPlaced()} puzzli.");
            for(int i = 0; i < TotalPieces; i++)
            {
                if (PuzzlePieces[i] != null || PuzzlePieces[i].gameObject != null)
                {
                    if (PuzzlePieces[i].gameObject.activeInHierarchy)
                    {
                        if (PuzzlePieces[i].NetworkObject != null && PuzzlePieces[i].NetworkObject.IsSpawned)
                        {
                            PuzzlePieces[i].ResetToOrigin();
                        }
                    }
                }
            }
        }
    }

    private int CountPlaced()
    {
        int count = 0;
        for (int i = 0; i < TotalPieces; i++)
        {
            if (placedPieces[i]) count++;
        }
        return count;
    }
}