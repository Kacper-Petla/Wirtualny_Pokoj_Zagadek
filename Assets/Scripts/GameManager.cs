using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private RoomController[] rooms;

    [SerializeField] private Transform playerTransform;

    [SerializeField] private NotebookController notebook;
    [SerializeField] private GameObject calculator;

    [SerializeField] private Transform defaultNotebookSocket;
    [SerializeField] private Transform defaultCalculatorSocket;

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

    private bool isNotebookAtDefault = true;
    private bool isCalculatorAtDefault = true;

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

        if (IsClient && !IsServer && CurrentRoomIndex.Value > 0)
        {
            SyncPlayerToCurrentRoom(CurrentRoomIndex.Value);
        }
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

        if (rooms != null && targetRoom < rooms.Length)
        {
            CurrentRoomIndex.Value = targetRoom;
        }
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

        if (IsServer)
        {
            InitRoomTools(newIndex);
        }
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

        // 2. Resetujemy stan: na starcie nowego pokoju narzêdzia zawsze l¹duj¹ w defaultSocket
        isNotebookAtDefault = true;
        isCalculatorAtDefault = true;

        if (notebook != null && defaultNotebookSocket != null)
        {
            SnapToolToSocket(notebook.gameObject, defaultNotebookSocket);
        }

        if (calculator != null && defaultCalculatorSocket != null)
        {
            SnapToolToSocket(calculator, defaultCalculatorSocket);
        }
    }

    public void ToggleNotebookSocket()
    {
        if (!IsServer)
        {
            return;
        }

        if (notebook == null) return;
        RoomController currentRoom = rooms[CurrentRoomIndex.Value];

        // Jeœli jest przy default -> przenieœ do pokoju; w przeciwnym razie przywo³aj do default
        if (isNotebookAtDefault)
        {
            if (currentRoom != null && currentRoom.NotebookSocket != null)
            {
                SnapToolToSocket(notebook.gameObject, currentRoom.NotebookSocket);
                isNotebookAtDefault = false;
            }
        }
        else
        {
            if (defaultNotebookSocket != null)
            {
                SnapToolToSocket(notebook.gameObject, defaultNotebookSocket);
                isNotebookAtDefault = true;
            }
        }
    }

    /// <summary>
    /// Wywo³aj tê funkcjê przyciskiem (np. Przycisk 1 na kontrolerze lub C na klawiaturze)
    /// </summary>
    public void ToggleCalculatorSocket()
    {
        if (!IsServer)
        {
            return;
        }

        if (calculator == null) return;
        RoomController currentRoom = rooms[CurrentRoomIndex.Value];

        if (isCalculatorAtDefault)
        {
            if (currentRoom != null && currentRoom.CalculatorSocket != null)
            {
                SnapToolToSocket(calculator, currentRoom.CalculatorSocket);
                isCalculatorAtDefault = false;
            }
        }
        else
        {
            if (defaultCalculatorSocket != null)
            {
                SnapToolToSocket(calculator, defaultCalculatorSocket);
                isCalculatorAtDefault = true;
            }
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

        if (tool.TryGetComponent<NetworkObject>(out var netObj))
        {
            SyncToolPositionClientRpc(netObj, socket.position, socket.rotation);
        }
    }

    [ClientRpc]
    private void SyncToolPositionClientRpc(NetworkObjectReference toolRef, Vector3 pos, Quaternion rot)
    {
        if (toolRef.TryGet(out var netObj))
        {
            if (netObj.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            netObj.transform.position = pos;
            netObj.transform.rotation = rot;
        }
    }

    private void SyncPlayerToCurrentRoom(int targetRoomIdx)
    {
        if (rooms == null || targetRoomIdx <= 0 || targetRoomIdx >= rooms.Length) return;

        RoomController startRoom = rooms[0];
        RoomController targetRoom = rooms[targetRoomIdx];

        if (startRoom != null && targetRoom != null)
        {
            Vector3 offset = targetRoom.RoomAnchor.position - startRoom.RoomAnchor.position;
            ShiftPlayer(offset);
            targetRoom.OnRoomEntered();
        }
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