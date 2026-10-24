using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class NotebookController : NetworkBehaviour
{

    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text contentText;
    [SerializeField] private Image hintImageDisplay;
    [SerializeField] private TMP_Text pageCounterText;

    private readonly List<RoomHintPage> unlockedPages = new();

    public readonly NetworkVariable<int> CurrentPageIndex = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public readonly NetworkVariable<int> TotalUnlockedPages = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Coroutine hintUnlockCoroutine;
    private RoomHintPage[] currentRoomHintPool;

    public override void OnNetworkSpawn()
    {
        CurrentPageIndex.OnValueChanged += OnPageIndexChanged;
        TotalUnlockedPages.OnValueChanged += OnTotalPagesChanged;
        UpdateDisplay();
    }

    public override void OnNetworkDespawn()
    {
        CurrentPageIndex.OnValueChanged -= OnPageIndexChanged;
        TotalUnlockedPages.OnValueChanged -= OnTotalPagesChanged;
        StopHintsTimer();
    }

    public void LoadRoomHints(RoomHintPage[] pages)
    {
        if (!IsServer) return;

        StopHintsTimer();
        currentRoomHintPool = pages;

        // Czyœcimy poprzednie strony
        ClearPagesClientRpc();

        if (pages == null || pages.Length == 0) return;

        hintUnlockCoroutine = StartCoroutine(UnlockHintsRoutine());
    }

    private void StopHintsTimer()
    {
        if (hintUnlockCoroutine != null)
        {
            StopCoroutine(hintUnlockCoroutine);
            hintUnlockCoroutine = null;
        }
    }

    private IEnumerator UnlockHintsRoutine()
    {
        float timer = 0f;
        int nextPageIndexToUnlock = 0;

        while (nextPageIndexToUnlock < currentRoomHintPool.Length)
        {
            yield return new WaitForSeconds(1f);
            timer += 1f;

            while (nextPageIndexToUnlock < currentRoomHintPool.Length &&
                   timer >= currentRoomHintPool[nextPageIndexToUnlock].unlockDelaySeconds)
            {
                // Przekazujemy klientom tylko indeks z puli pokoju
                UnlockPageClientRpc(nextPageIndexToUnlock);
                nextPageIndexToUnlock++;
            }
        }
    }

    [ClientRpc]
    private void ClearPagesClientRpc()
    {
        unlockedPages.Clear();
        if (IsServer)
        {
            TotalUnlockedPages.Value = 0;
            CurrentPageIndex.Value = 0;
        }
        UpdateDisplay();
    }

    [ClientRpc]
    private void UnlockPageClientRpc(int poolIndex)
    {
        // Pobieramy dane strony z bie¿¹cego pokoju lokalnie
        int currentRoomIdx = GameManager.Instance.CurrentRoomIndex.Value;
        RoomController room = GameManager.Instance.GetRoom(currentRoomIdx);

        if (room != null && room.HintPages != null && poolIndex < room.HintPages.Length)
        {
            unlockedPages.Add(room.HintPages[poolIndex]);

            if (IsServer)
            {
                TotalUnlockedPages.Value = unlockedPages.Count;
                CurrentPageIndex.Value = unlockedPages.Count - 1;
            }

            UpdateDisplay();
        }
    }

    public void NextPage() => ChangePageServerRpc(1);
    public void PreviousPage() => ChangePageServerRpc(-1);

    [ServerRpc(RequireOwnership = false)]
    private void ChangePageServerRpc(int step)
    {
        if (TotalUnlockedPages.Value <= 0) return;

        int newIdx = CurrentPageIndex.Value + step;
        if (newIdx >= TotalUnlockedPages.Value) newIdx = 0;
        if (newIdx < 0) newIdx = TotalUnlockedPages.Value - 1;

        CurrentPageIndex.Value = newIdx;
    }

    private void OnPageIndexChanged(int oldIdx, int newIdx) => UpdateDisplay();
    private void OnTotalPagesChanged(int oldTotal, int newTotal) => UpdateDisplay();

    private void UpdateDisplay()
    {
        if (unlockedPages.Count == 0 || CurrentPageIndex.Value >= unlockedPages.Count)
        {
            if (titleText != null) titleText.text = "NOTATNIK";
            if (contentText != null)
            {
                contentText.gameObject.SetActive(true);
                contentText.text = "<color=#888888>Brak wpisów dla tego pokoju...</color>";
            }
            if (hintImageDisplay != null) hintImageDisplay.gameObject.SetActive(false);
            if (pageCounterText != null) pageCounterText.text = "0 / 0";
            return;
        }

        int idx = CurrentPageIndex.Value;
        RoomHintPage currentPage = unlockedPages[idx];

        if (titleText != null) titleText.text = currentPage.hintTitle;
        if (pageCounterText != null) pageCounterText.text = $"{idx + 1} / {unlockedPages.Count}";

        // =========================================================================
        // Prze³¹czanie widoku: OBRAZ vs TEKST
        // =========================================================================
        if (currentPage.HasImage)
        {
            // Ukrywamy tekst, pokazujemy obraz
            if (contentText != null) contentText.gameObject.SetActive(false);

            if (hintImageDisplay != null)
            {
                hintImageDisplay.gameObject.SetActive(true);
                hintImageDisplay.sprite = currentPage.hintImage;
                hintImageDisplay.preserveAspect = true; // Zapobiega zniekszta³ceniu proporcji
            }
        }
        else
        {
            // Ukrywamy obraz, pokazujemy tekst
            if (hintImageDisplay != null) hintImageDisplay.gameObject.SetActive(false);

            if (contentText != null)
            {
                contentText.gameObject.SetActive(true);
                contentText.text = currentPage.content;
            }
        }
    }
}