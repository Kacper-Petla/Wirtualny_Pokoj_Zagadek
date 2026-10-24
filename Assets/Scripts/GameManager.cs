using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Rooms Configuration")]
    [SerializeField] private RoomController[] rooms;

    [Header("Player / Camera (Do testów na WASD przeci¹gnij Main Camera)")]
    [SerializeField] private Transform playerTransform;

    [SerializeField] private NotebookController notebook;
    [SerializeField] private GameObject calculator;

    public readonly NetworkVariable<int> CurrentRoomIndex = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public readonly NetworkVariable<bool> SkipTutorial = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        CurrentRoomIndex.OnValueChanged += HandleRoomIndexChanged;
    }

    public override void OnNetworkDespawn()
    {
        CurrentRoomIndex.OnValueChanged -= HandleRoomIndexChanged;
    }

    public void ToggleSkipTutorial()
    {
        if (!IsServer) return;
        SkipTutorial.Value = !SkipTutorial.Value;
        Debug.Log($"[GameManager] Pomiñ tutorial: {SkipTutorial.Value}");
    }

    public void StartGame()
    {
        if (!IsServer) return;
        if (CurrentRoomIndex.Value != 0) return; // Gra ju¿ wystartowa³a

        // Jeœli pomijamy tutorial -> skaczemy do pokoju 2, w przeciwnym razie do 1
        int targetRoom = SkipTutorial.Value ? 2 : 1;
    }

    public void GoToNextRoom()
    {
        if (!IsServer) return;

        int nextIndex = CurrentRoomIndex.Value + 1;
        if (rooms == null || nextIndex >= rooms.Length)
        {
            Debug.Log("[GameManager] Wszystkie pokoje ukoñczone!");
            return;
        }

        Debug.Log($"[GameManager] Przejœcie do pokoju: {nextIndex}");
        CurrentRoomIndex.Value = nextIndex;
    }

    private void HandleRoomIndexChanged(int previousIndex, int newIndex)
    {
        if (rooms == null || previousIndex < 0 || previousIndex >= rooms.Length || newIndex >= rooms.Length) return;

        RoomController currentRoom = rooms[previousIndex];
        RoomController targetRoom = rooms[newIndex];

        if (currentRoom == null || targetRoom == null) return;

        Vector3 offset = targetRoom.RoomAnchor.position - currentRoom.RoomAnchor.position;

        ShiftPlayer(offset);
        targetRoom.OnRoomEntered();
    }

    private void InitRoomTools(int roomIdx)
    {
        if (!IsServer || rooms == null || roomIdx >= rooms.Length) return;

        RoomController currentRoom = rooms[roomIdx];

        // 1. Uruchamiamy odliczanie podpowiedzi dla nowego pokoju
        if (notebook != null)
        {
            notebook.LoadRoomHints(currentRoom.HintPages);
        }

        DockNotebookToCurrentRoom();
        DockCalculatorToCurrentRoom();
    }

    public void DockNotebookToCurrentRoom()
    {
        if (!IsServer)
        {
            return;
        }

        RoomController currentRoom = rooms[CurrentRoomIndex.Value];
        if (currentRoom != null && currentRoom.NotebookSocket != null && notebook != null)
        {
            SnapToolToSocket(notebook.gameObject, currentRoom.NotebookSocket);
        }
    }

    public void DockCalculatorToCurrentRoom()
    {
        if (!IsServer)
        {
            return;
        }

        RoomController currentRoom = rooms[CurrentRoomIndex.Value];
        if (currentRoom != null && currentRoom.CalculatorSocket != null && calculator != null)
        {
            SnapToolToSocket(calculator, currentRoom.CalculatorSocket);
        }
    }

    private void SnapToolToSocket(GameObject tool, Transform socket)
    {
        // Jeœli narzêdzie by³o chwycone przez gracza, upuszczamy je
        if (tool.TryGetComponent<GrabbableObject>(out var grabbable))
        {
            grabbable.Drop();
        }

        if (tool.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = socket.position;
            rb.rotation = socket.rotation;
        }

        tool.transform.position = socket.position;
        tool.transform.rotation = socket.rotation;

    }

    private void ShiftPlayer(Vector3 offset)
    {
        Transform target = playerTransform;

        if (target == null)
        {
            Debug.LogError("[GameManager] Brak referencji do gracza/kamery!");
            return;
        }

        CharacterController cc = target.GetComponent<CharacterController>();
        if (cc == null) cc = target.GetComponentInChildren<CharacterController>();

        if (cc != null) cc.enabled = false;

        // 2. Jeœli postaæ ma Rigidbody, czyœcimy pêdy
        Rigidbody rb = target.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position += offset;
        }

        // 3. W³aœciwa zmiana pozycji transformu
        target.position += offset;

        // 4. Ponowne w³¹czenie CharacterController z zaktualizowanym buforem pozycji
        if (cc != null) cc.enabled = true;
    }

    public RoomController GetRoom(int index)
    {
        if (rooms != null && index >= 0 && index < rooms.Length)
        {
            return rooms[index];
        }
        return null;
    }
}