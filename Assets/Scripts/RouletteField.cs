using UnityEngine;

public class RouletteField : MonoBehaviour
{
    [SerializeField] private int fieldValue = 0;
    [SerializeField] private Transform snapPoint;

    public int FieldValue => fieldValue;

    public Vector3 GetSnapPosition()
    {
        return snapPoint != null ? snapPoint.position : transform.position;
    }

    private void OnDrawGizmos()
    {
        // Wizualizacja punktu przyci¹gania w edytorze
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(GetSnapPosition(), 0.03f);
    }
}