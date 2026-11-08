using System;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class LiquidCylinderManager : NetworkBehaviour
{
    [Serializable]
    public struct SectorData
    {
        public int probA, defectA;
        public int probB, defectB;
        public int probC, defectC;
        public int expectedPermille;
    }

    [Header("Displays & Note")]
    [SerializeField] private TMP_Text noteContentText;
    [SerializeField] private TMP_Text feedbackText;


    [SerializeField] private LiquidCylinder[] cylinders = new LiquidCylinder[2];

    [SerializeField] private DoorLocked exitDoor;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip valveTurnSound;
    [SerializeField] private AudioClip steamHissSound;
    [SerializeField] private AudioClip successSound;
    [SerializeField] private AudioClip errorResetSound;

    private readonly SectorData[] prebakedSets = new SectorData[]
    {
        new SectorData { probA = 50, defectA = 2, probB = 30, defectB = 5, probC = 20, defectC = 10, expectedPermille = 45 },
        new SectorData { probA = 40, defectA = 5, probB = 40, defectB = 2, probC = 20, defectC = 6,  expectedPermille = 40 },
        new SectorData { probA = 60, defectA = 3, probB = 20, defectB = 4, probC = 20, defectC = 7,  expectedPermille = 40 },
        new SectorData { probA = 50, defectA = 4, probB = 30, defectB = 3, probC = 20, defectC = 8,  expectedPermille = 45 }
    };

    public readonly NetworkVariable<bool> IsSteamCutOff = new(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<int> selectedIndex = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<FixedString512Bytes> syncedNote = new(
        string.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<FixedString512Bytes> syncedFeedback = new(
        string.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        syncedNote.OnValueChanged += (o, n) => { if (noteContentText != null) noteContentText.text = n.ToString(); };
        syncedFeedback.OnValueChanged += (o, n) => { if (feedbackText != null) feedbackText.text = n.ToString(); };

        if (noteContentText != null && !syncedNote.Value.IsEmpty)
            noteContentText.text = syncedNote.Value.ToString();

        if (feedbackText != null && !syncedFeedback.Value.IsEmpty)
            feedbackText.text = syncedFeedback.Value.ToString();

        if (IsServer)
        {
            InitDataset();
        }
    }

    private void InitDataset()
    {
        int idx = UnityEngine.Random.Range(0, prebakedSets.Length);
        selectedIndex.Value = idx;

        var d = prebakedSets[idx];
        syncedNote.Value = $"Wys³aliœmy zapytania do trzech sektorów. System losuje sektor z prawdopodobieñstwem:\n\n" +
                           $"• <b>Sektor A (Czerwony)</b>: {d.probA}% (wadliwe: {d.defectA}%)\n" +
                           $"• <b>Sektor B (Niebieski)</b>: {d.probB}% (wadliwe: {d.defectB}%)\n" +
                           $"• <b>Sektor C (Zielony)</b>: {d.probC}% (wadliwe: {d.defectC}%)\n\n" +
                           $"Oblicz ca³kowite prawdopodobieñstwo wylosowania wadliwego ¿etonu. " +
                           $"Ustaw poziom w promilach na cylindrach ciœnienia i przekrêæ zawór bezpieczeñstwa.";
    }

    public void OnValveTurned()
    {
        if (IsSteamCutOff.Value) return;

        PlayAudio(0); // DŸwiêk kurka

        // Sumujemy wartoœci ze wszystkich 4 cylindrów (np. Cyl1: 40 + Cyl2: 5 = 45‰)
        int totalEntered = 0;
        foreach (var c in cylinders)
        {
            if (c != null) totalEntered += c.Value;
        }

        int target = prebakedSets[selectedIndex.Value].expectedPermille;

        if (totalEntered == target)
        {
            // SUKCES: Zamkniêcie pary wodnej
            IsSteamCutOff.Value = true;
            syncedFeedback.Value = "<color=green>ZAWÓR ODCINAJ¥CY ZAMKNIÊTY. PARA USUNIÊTA.</color>";
            PlayAudio(1); // Sukces

            if (exitDoor != null)
            {
                exitDoor.UnlockDoor();
            }
        }
        else
        {
            // B£¥D: Reset p³ynu w cylindrach
            foreach (var c in cylinders)
            {
                if (c != null) c.ResetValue();
            }

            syncedFeedback.Value = $"<color=red>NIEPOPRAWNE CIŒNIENIE ({totalEntered}‰). ZRESETOWANO POZIOMY.</color>";
            PlayAudio(2); // B³¹d
        }
    }

    private void PlayAudio(int soundId)
    {
        if (audioSource == null) return;
        if (soundId == 0 && valveTurnSound != null) audioSource.PlayOneShot(valveTurnSound);
        else if (soundId == 1 && successSound != null) audioSource.PlayOneShot(successSound);
        else if (soundId == 2 && errorResetSound != null) audioSource.PlayOneShot(errorResetSound);
    }
}