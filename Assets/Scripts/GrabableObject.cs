using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkObject))]
public class GrabbableObject : NetworkBehaviour, IInteractable
{
    public enum BoundaryAction
    {
        None,
        Reset,
        Despawn
    }

    [SerializeField] private BoundaryAction boundaryAction = BoundaryAction.Reset;
    [SerializeField] private float maxDistanceFromSpawn = 10.0f;

    [SerializeField] private float smoothFollowSpeed = 20f;

    private Rigidbody rb;
    private Transform pointerTransform;
    private float grabDistance;
    private Quaternion initialRotationOffset;
    private bool isGrabbed = false;

    private Vector3 initialPosition;
    private Quaternion initialRotation;

    public bool IsGrabbed => isGrabbed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            initialPosition = transform.position;
            initialRotation = transform.rotation;
        }
    }

    private void Update()
    {
        if (!IsServer) return;

        if (isGrabbed && pointerTransform != null)
        {
            Vector3 targetPosition = pointerTransform.position + (pointerTransform.forward * grabDistance);
            Quaternion targetRotation = pointerTransform.rotation * initialRotationOffset;

            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * smoothFollowSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * smoothFollowSpeed);
        }

        CheckBoundaryDistance();
    }

    private void CheckBoundaryDistance()
    {
        if (boundaryAction == BoundaryAction.None) return;

        float distance = Vector3.Distance(transform.position, initialPosition);
        if (distance > maxDistanceFromSpawn)
        {
            HandleBoundaryExceeded();
        }
    }

    private void HandleBoundaryExceeded()
    {
        if (isGrabbed)
        {
            Drop();
        }

        if (boundaryAction == BoundaryAction.Reset)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            transform.position = initialPosition;
            transform.rotation = initialRotation;
        }
        else if (boundaryAction == BoundaryAction.Despawn)
        {
            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                if (NetworkObject.IsSceneObject == true)
                {
                    NetworkObject.Despawn(false);
                    gameObject.SetActive(false);
                }
                else
                {
                    NetworkObject.Despawn(destroy: true);
                }
            }
        }
    }

    public void Interact() { }

    public void Grab(Transform pointer, float distance)
    {
        if (!IsServer) return;

        isGrabbed = true;
        pointerTransform = pointer;
        grabDistance = distance;

        initialRotationOffset = Quaternion.Inverse(pointer.rotation) * transform.rotation;
        rb.isKinematic = true;
    }

    public void Drop()
    {
        if (!IsServer) return;

        isGrabbed = false;
        pointerTransform = null;
        rb.isKinematic = false;
    }

    // Wizualizacja zasiêgu w edytorze Unity
    private void OnDrawGizmosSelected()
    {
        if (boundaryAction == BoundaryAction.None) return;

        Gizmos.color = Color.yellow;
        Vector3 center = Application.isPlaying ? initialPosition : transform.position;
        Gizmos.DrawWireSphere(center, maxDistanceFromSpawn);
    }
}