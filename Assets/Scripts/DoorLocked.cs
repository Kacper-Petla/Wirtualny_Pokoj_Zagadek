using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

public class DoorLocked : NetworkBehaviour, IInteractable
{
    public readonly NetworkVariable<bool> IsUnlocked = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip doorOpenSound;
    [SerializeField] private AudioClip doorLockedSound;

    public void UnlockDoor()
    {
        if (!IsServer || IsUnlocked.Value) return;

        IsUnlocked.Value = true;
        Debug.Log($"[{gameObject.name}] Drzwi zosta³y odblokowane.");
    }

    public void Interact()
    {
        if (IsUnlocked.Value)
        {
            PlaySound(true);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.GoToNextRoom();
            }
        }
        else
        {
            PlaySound(false);
            Debug.Log($"[{gameObject.name}] Drzwi s¹ zamkniête. Ukoñcz zagadkê w tym pokoju.");
        }
    }

    private void PlaySound(bool isOpenSuccess)
    {
        if (audioSource == null) return;

        AudioClip clip = isOpenSuccess ? doorOpenSound : doorLockedSound;
        if (clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}