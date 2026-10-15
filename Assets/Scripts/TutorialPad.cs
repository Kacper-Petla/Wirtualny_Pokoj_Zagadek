using Unity.Netcode;
using UnityEngine;

public class TutorialPad : NetworkBehaviour
{
    [SerializeField] private TutorialManager tutorialManager;
    [SerializeField] private GrabbableObject expectedCube;
    [SerializeField] private bool isGreenPad;

    private bool isPlaced = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || isPlaced) return;

        if (other.TryGetComponent<GrabbableObject>(out var grabbable) && grabbable == expectedCube)
        {
            isPlaced = true;
            grabbable.Drop();
            grabbable.enabled = false;

            if (grabbable.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            grabbable.transform.position = transform.position + Vector3.up * 0.1f;
            grabbable.transform.rotation = Quaternion.identity;

            if (isGreenPad) tutorialManager.OnGreenPadPlaced();
            else tutorialManager.OnRedPadPlaced();
        }
    }
}