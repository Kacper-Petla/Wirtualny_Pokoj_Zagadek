using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class CoinMachine : NetworkBehaviour
{
    public enum LightState
    {
        Off,
        Success,
        Error
    }

    [SerializeField] private int targetRequiredValue = 5;

    [SerializeField] private TextMeshPro scoreDisplay;
    [SerializeField] private Renderer statusLightRenderer;
    [SerializeField] private Material lightOffMat;
    [SerializeField] private Material lightSuccessMat;
    [SerializeField] private Material lightErrorMat;

    private readonly NetworkVariable<int> currentScore = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<LightState> currentLightState = new(
        LightState.Off,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        currentScore.OnValueChanged += OnScoreChanged;
        currentLightState.OnValueChanged += OnLightStateChanged;

        UpdateDisplay(currentScore.Value);
        ApplyLightState(currentLightState.Value);
    }

    public override void OnNetworkDespawn()
    {
        currentScore.OnValueChanged -= OnScoreChanged;
        currentLightState.OnValueChanged -= OnLightStateChanged;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        if (currentLightState.Value == LightState.Success) return;

        if (other.TryGetComponent<CoinToken>(out var coin))
        {
            if (currentLightState.Value != LightState.Off) currentLightState.Value = LightState.Off;

            currentScore.Value += coin.Value;

            //usuwanie obiektu
            if (other.TryGetComponent<NetworkObject>(out var netObj))
            {
                if (netObj.IsSpawned)
                {
                    if (netObj.IsSceneObject == true)
                    {
                        netObj.Despawn(false);
                        other.gameObject.SetActive(false);
                    }
                    else
                    {
                        netObj.Despawn(true);
                    }
                }
            }
            else
            {
                Destroy(other.gameObject);
            }
        }
    }

    public void OnAcceptButtonPressed()
    {
        if (!IsServer) return;

        if (currentLightState.Value == LightState.Success) return;

        if (currentScore.Value == targetRequiredValue)
        {
            currentLightState.Value = LightState.Success;
        }
        else
        {
            currentLightState.Value = LightState.Error;
            currentScore.Value = 0;
        }
    }

    public void OnResetButtonPressed()
    {
        if (!IsServer) return;

        //if (currentLightState.Value == LightState.Success) return;

        currentScore.Value = 0;
        currentLightState.Value = LightState.Off;
    }

   
    private void OnScoreChanged(int previousValue, int newValue)
    {
        UpdateDisplay(newValue);
    }

    private void OnLightStateChanged(LightState previousState, LightState newState)
    {
        ApplyLightState(newState);
    }

    private void UpdateDisplay(int value)
    {
        if (scoreDisplay != null)
        {
            scoreDisplay.text = value.ToString();
        }
    }

    private void ApplyLightState(LightState state)
    {
        if (statusLightRenderer == null) return;

        switch (state)
        {
            case LightState.Off:
                statusLightRenderer.material = lightOffMat;
                break;
            case LightState.Success:
                statusLightRenderer.material = lightSuccessMat;
                break;
            case LightState.Error:
                statusLightRenderer.material = lightErrorMat;
                break;
        }
    }
}