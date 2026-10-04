using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class RouletteChip : NetworkBehaviour
{
    [SerializeField] private float snapCheckDistance = 0.25f;
    [SerializeField] private LayerMask interactableLayer = ~0;

    public RouletteField CurrentField { get; private set; }

    private GrabbableObject grabbable;
    private Rigidbody rb;

    private void Awake()
    {
        grabbable = GetComponent<GrabbableObject>();
        rb = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        if (grabbable != null)
        {
            grabbable.OnDropped += HandleDropped;
            grabbable.OnGrabbed += HandleGrabbed;
        }
    }

    private void OnDisable()
    {
        if (grabbable != null)
        {
            grabbable.OnDropped -= HandleDropped;
            grabbable.OnGrabbed -= HandleGrabbed;
        }
    }

    private void HandleGrabbed()
    {
        if (!IsServer) return;
        if (CurrentField != null)
        {
            if (TryGetComponent<GrabbableObject>(out var grabbable))
            {
                grabbable.IsResettable = true;
            }
            CurrentField = null;
        }
    }

    // Wywo³aj to w momencie, gdy gracz puszcza obiekt (Drop)
    public void HandleDropped()
    {
        if (!IsServer) return;

        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, snapCheckDistance, interactableLayer))
        {
            if (hit.collider.TryGetComponent<RouletteField>(out var field))
            {
                PlaceOnField(field);
                return;
            }
        }

        CurrentField = null;
    }

    private void PlaceOnField(RouletteField field)
    {
        if (TryGetComponent<GrabbableObject>(out var grabbable))
        {
            grabbable.IsResettable = false;
        }
        CurrentField = field;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        transform.position = field.GetSnapPosition();
        transform.rotation = Quaternion.identity;
    }

    public void OnGrabbedAgain()
    {
        if (!IsServer) return;

        CurrentField = null;
        if (rb != null)
        {
            rb.isKinematic = false;
        }
    }
}