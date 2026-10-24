using TMPro;
using Unity.Netcode;
using UnityEngine;

public class LobbyMenuController : NetworkBehaviour
{
    [Header("Displays")]
    [SerializeField] private TMP_Text skipTutorialStatusText;

    public override void OnNetworkSpawn()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SkipTutorial.OnValueChanged += OnSkipOptionChanged;
            UpdateDisplay(GameManager.Instance.SkipTutorial.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SkipTutorial.OnValueChanged -= OnSkipOptionChanged;
        }
    }

    private void OnSkipOptionChanged(bool oldVal, bool newVal)
    {
        UpdateDisplay(newVal);
    }

    private void UpdateDisplay(bool isSkipped)
    {
        if (skipTutorialStatusText != null)
        {
            skipTutorialStatusText.text = isSkipped
                ? "<color=green>[X] POMIÑ TUTORIAL: W£¥CZONE</color>"
                : "<color=red>[ ] POMIÑ TUTORIAL: WY£¥CZONE</color>";
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