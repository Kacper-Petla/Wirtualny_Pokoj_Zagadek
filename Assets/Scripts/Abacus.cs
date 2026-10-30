using Unity.Netcode;
using UnityEngine;

public class Abacus : NetworkBehaviour
{

    [SerializeField] private Transform[] tensBeads = new Transform[9];
    [SerializeField] private Transform[] unitsBeads = new Transform[9];

    
    private Vector3 shiftOffsetLocal = new Vector3(0f, 0f, -0.26f);

    private Vector3[] tensBasePositions;
    private Vector3[] unitsBasePositions;

    public readonly NetworkVariable<int> TensDigit = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );
    public readonly NetworkVariable<int> UnitsDigit = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private bool isLocked = false;

    public int CurrentValue => (TensDigit.Value * 10) + UnitsDigit.Value;

    private void Awake()
    {
        tensBasePositions = new Vector3[tensBeads.Length];
        for (int i = 0; i < tensBeads.Length; i++)
        {
            if (tensBeads[i] != null) tensBasePositions[i] = tensBeads[i].localPosition;
        }

        unitsBasePositions = new Vector3[unitsBeads.Length];
        for (int i = 0; i < unitsBeads.Length; i++)
        {
            if (unitsBeads[i] != null) unitsBasePositions[i] = unitsBeads[i].localPosition;
        }
    }

    public override void OnNetworkSpawn()
    {
        TensDigit.OnValueChanged += (oldVal, newVal) => UpdateBeadsVisuals(tensBeads, tensBasePositions, newVal);
        UnitsDigit.OnValueChanged += (oldVal, newVal) => UpdateBeadsVisuals(unitsBeads, unitsBasePositions, newVal);

        UpdateBeadsVisuals(tensBeads, tensBasePositions, TensDigit.Value);
        UpdateBeadsVisuals(unitsBeads, unitsBasePositions, UnitsDigit.Value);
    }

    public void SetLocked(bool locked)
    {
        isLocked = locked;
    }

    public void ClickedUnits()
    {
        ClickRow(false);
    }

    public void ClickedTens()
    {
        ClickRow(true);
    }

    private void ClickRow(bool isTens)
    {
        if (isLocked) return;

        if (isTens)
        {
            int next = TensDigit.Value + 1;
            TensDigit.Value = (next > 9) ? 0 : next;
        }
        else
        {
            int next = UnitsDigit.Value + 1;
            UnitsDigit.Value = (next > 9) ? 0 : next;
        }
    }

    public void ResetValue()
    {
        if (!IsServer) return;
        TensDigit.Value = 0;
        UnitsDigit.Value = 0;
    }

    private void UpdateBeadsVisuals(Transform[] beads, Vector3[] basePositions, int activeCount)
    {
        if (beads == null || basePositions == null) return;

        for (int i = 0; i < beads.Length; i++)
        {
            if (beads[i] == null) continue;

            // Pierwsze activeCount koralików jest przesuniête w prawo
            if (i < activeCount)
            {
                beads[i].localPosition = basePositions[i] + shiftOffsetLocal;
            }
            else
            {
                beads[i].localPosition = basePositions[i];
            }
        }
    }
}