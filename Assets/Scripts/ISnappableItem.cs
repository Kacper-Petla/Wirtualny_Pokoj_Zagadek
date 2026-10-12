using UnityEngine;

public interface ISnappableItem
{
    int SlotId { get; }

    bool IsPlaced { get; }

    void SnapToSlot(Transform targetSocket);

    void ResetToOrigin();
}