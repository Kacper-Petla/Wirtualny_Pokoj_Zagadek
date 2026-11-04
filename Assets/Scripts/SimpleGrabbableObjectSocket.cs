using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class SimpleGrabbableObjectSocket : NetworkBehaviour
{
    [SerializeField] private GameObject targetObject;

    [SerializeField] private Transform snapSocket;

    [SerializeField] private UnityEvent onObjectSnapped;

    public readonly NetworkVariable<bool> IsSnapped = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || IsSnapped.Value || targetObject == null) return;

        var grabbable = other.GetComponent<GrabbableObject>();
        if (grabbable == null) return;

        if (grabbable.gameObject != targetObject && other.gameObject != targetObject) return;

        GameObject candidate = targetObject;
        IsSnapped.Value = true;

        grabbable.IsResettable = false;
        grabbable.Drop();
        grabbable.enabled = false;

        if (candidate.TryGetComponent<Collider>(out var c))
        {
            c.enabled = false;
        }

        if (candidate.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        Transform target = snapSocket != null ? snapSocket : transform;
        candidate.transform.position = target.position;
        candidate.transform.rotation = target.rotation;

        onObjectSnapped?.Invoke();
    }
}