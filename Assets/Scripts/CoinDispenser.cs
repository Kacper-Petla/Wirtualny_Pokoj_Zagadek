using Unity.Netcode;
using UnityEngine;

public class CoinDispenser : NetworkBehaviour, IInteractable
{
    [SerializeField] private GameObject coinPrefab;

    public void Interact() { }

    public void Interact(RaycastPointer pointer, RaycastHit hit)
    {
        if (!IsServer || pointer == null || coinPrefab == null) return;
        if (pointer.IsHoldingObject) return;

        Vector3 spawnPosition = hit.point;
        Quaternion spawnRotation = Quaternion.LookRotation(hit.normal);

        GameObject newCoin = Instantiate(coinPrefab, spawnPosition, spawnRotation);

        if (newCoin.TryGetComponent<NetworkObject>(out var netObj))
        {
            netObj.Spawn();
        }

        if (newCoin.TryGetComponent<GrabbableObject>(out var grabbable))
        {
            grabbable.Grab(pointer.RayOrigin, hit.distance);
            pointer.ForceGrab(grabbable);
        }
    }
}