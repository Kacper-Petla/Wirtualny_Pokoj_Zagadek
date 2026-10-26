using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

public class DoorWithSlots : NetworkBehaviour, IInteractable
{
    [SerializeField] private Transform[] snapSockets;
    [SerializeField] private GrabbableObject[] expectedItems;

    [SerializeField] private int roomNumber;

    public readonly NetworkVariable<bool> IsUnlocked = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [SerializeField] private UnityEvent onInteracted;

    private bool[] placedSlots;

    private int TotalSlots => snapSockets != null ? snapSockets.Length : 0;

    private void Awake()
    {
        placedSlots = new bool[TotalSlots];
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || IsUnlocked.Value) return;

        if (other.TryGetComponent<DoorItem>(out var snappable))
        {
            int slotIndex = snappable.SlotId;

            if (slotIndex >= 0 && slotIndex < TotalSlots && !placedSlots[slotIndex])
            {
                placedSlots[slotIndex] = true;
                snappable.SnapToSlot(snapSockets[slotIndex]);

                Debug.Log($"[Room{roomNumber}] Wpasowano element w slot {slotIndex} ({CountPlaced()}/{TotalSlots})");
            }
        }
    }

    public void Interact()
    {
        if (!IsServer || IsUnlocked.Value) return;

        onInteracted?.Invoke();

        if (CountPlaced() == TotalSlots)
        {
            IsUnlocked.Value = true;
            Debug.Log($"[Room{roomNumber}] SUKCES: Wszystkie sloty zape³nione!");

            if (GameManager.Instance != null)
            {
                GameManager.Instance.GoToNextRoom();
            }
        }
        else
        {
            Debug.Log($"[Room{roomNumber}] Brakuje jeszcze {TotalSlots - CountPlaced()} elementów. Resetowanie nieznalezionych...");
            ResetMissingItems();
        }
    }

    private void ResetMissingItems()
    {
        if (expectedItems == null) return;

        for (int i = 0; i < expectedItems.Length; i++)
        {
            if (expectedItems[i] != null && expectedItems[i].gameObject.activeInHierarchy)
            {
                if (expectedItems[i].NetworkObject == null || expectedItems[i].NetworkObject.IsSpawned)
                {
                    expectedItems[i].ResetToOrigin();
                }
            }
        }
    }

    private int CountPlaced()
    {
        int count = 0;
        for (int i = 0; i < placedSlots.Length; i++)
        {
            if (placedSlots[i]) count++;
        }
        return count;
    }
}