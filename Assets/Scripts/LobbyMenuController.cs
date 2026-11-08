using TMPro;
using Unity.Netcode;
using UnityEngine;

public class LobbyMenuController : NetworkBehaviour
{
    [Header("Displays")]
    [SerializeField] private TMP_Text skipTutorialStatusText;
    [SerializeField] private TMP_Text muteAudioStatusText;

    public readonly NetworkVariable<bool> IsAudioMuted = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SkipTutorial.OnValueChanged += OnSkipOptionChanged;
            UpdateSkipDisplay(GameManager.Instance.SkipTutorial.Value);
        }

        IsAudioMuted.OnValueChanged += OnAudioMuteChanged;
        UpdateMuteDisplay(IsAudioMuted.Value);
        ApplyAudioListenerState(IsAudioMuted.Value);
    }

    public override void OnNetworkDespawn()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SkipTutorial.OnValueChanged -= OnSkipOptionChanged;
        }

        IsAudioMuted.OnValueChanged -= OnAudioMuteChanged;
    }

    public void OnToggleMutePressed()
    {
        if (!IsServer)
        {
            return;
        }

        IsAudioMuted.Value = !IsAudioMuted.Value;
    }

    private void OnAudioMuteChanged(bool oldVal, bool newVal)
    {
        UpdateMuteDisplay(newVal);
        ApplyAudioListenerState(newVal);
    }

    private void ApplyAudioListenerState(bool isMuted)
    {
        // Globalne wyciszenie wszystkich düwiÍkÛw, AudioSource'Ûw i muzyki na scenie
        AudioListener.pause = isMuted;
    }

    private void UpdateMuteDisplay(bool isMuted)
    {
        if (muteAudioStatusText != null)
        {
            muteAudioStatusText.text = isMuted
                ? "<color=red>[X] DèWI K: WYCISZONY</color>"
                : "<color=green>[ ] DèWI K: W£•CZONY</color>";
        }
    }

    private void OnSkipOptionChanged(bool oldVal, bool newVal)
    {
        UpdateSkipDisplay(newVal);
    }

    private void UpdateSkipDisplay(bool isSkipped)
    {
        if (skipTutorialStatusText != null)
        {
            skipTutorialStatusText.text = isSkipped
                ? "<color=green>[X] POMI— TUTORIAL: W£•CZONE</color>"
                : "<color=red>[ ] POMI— TUTORIAL: WY£•CZONE</color>";
        }
    }

    public void OnToggleSkipPressed()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ToggleSkipTutorial();
        }
    }

    public void OnStartButtonPressed()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartGame();
        }
    }
}