using Unity.Netcode;
using UnityEngine;

public class SlotNote : NetworkBehaviour, IInteractable
{
    [SerializeField] private SlotManager slotManager;

    [SerializeField] private float maxDistanceFromSpawn = 10.0f;

    private Vector3 initialPosition;

    public void Interact()
    {
        if (slotManager != null)
        {
            slotManager.PinNoteToWall();
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            initialPosition = transform.position;
        }
    }

    private void Update()
    {
        if (!IsServer) return;

        float distance = Vector3.Distance(transform.position, initialPosition);
        if (distance > maxDistanceFromSpawn)
        {
            transform.position = initialPosition;
        }
    }

    // Wizualizacja zasiêgu w edytorze Unity
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 center = Application.isPlaying ? initialPosition : transform.position;
        Gizmos.DrawWireSphere(center, maxDistanceFromSpawn);
    }
}