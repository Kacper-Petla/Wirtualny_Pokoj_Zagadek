using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BernoulliCardSocket : NetworkBehaviour, IInteractable
{
    [SerializeField] private Transform snapTransform;

    public readonly NetworkVariable<bool> IsOccupied = new(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private BernoulliCard currentCard;
    public string CurrentValue => currentCard != null ? currentCard.Value : string.Empty;

    private void Awake()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || IsOccupied.Value) return;

        var card = other.GetComponentInParent<BernoulliCard>();
        if (card == null) return;

        InsertCard(card);
    }

    private void InsertCard(BernoulliCard card)
    {
        currentCard = card;
        IsOccupied.Value = true;

        if (card.TryGetComponent<GrabbableObject>(out var grabbable))
        {
            grabbable.Drop();
            grabbable.enabled = false;
        }

        if (card.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        Transform target = snapTransform != null ? snapTransform : transform;
        card.transform.position = target.position;
        card.transform.rotation = target.rotation;

        if (card.TryGetComponent<NetworkObject>(out var netObj))
        {
            Snap(netObj, target.position, target.rotation);
        }
    }

    public void Interact() => EjectCard();

    public void EjectCard()
    {
        if (!IsOccupied.Value || currentCard == null) return;

        BernoulliCard cardToEject = currentCard;
        currentCard = null;
        IsOccupied.Value = false;

        cardToEject.ResetToOrigin();

        if (cardToEject.TryGetComponent<NetworkObject>(out var netObj))
        {
            Eject(netObj);
        }
    }

    private void Snap(NetworkObjectReference cardRef, Vector3 pos, Quaternion rot)
    {
        if (cardRef.TryGet(out var netObj))
        {
            if (netObj.TryGetComponent<GrabbableObject>(out var grabbable)) grabbable.enabled = false;
            if (netObj.TryGetComponent<Rigidbody>(out var rb)) rb.isKinematic = true;
            netObj.transform.position = pos;
            netObj.transform.rotation = rot;
        }
    }

    private void Eject(NetworkObjectReference cardRef)
    {
        if (cardRef.TryGet(out var netObj))
        {
            if (netObj.TryGetComponent<GrabbableObject>(out var grabbable)) grabbable.enabled = true;
            if (netObj.TryGetComponent<Rigidbody>(out var rb)) rb.isKinematic = false;
        }
    }
}