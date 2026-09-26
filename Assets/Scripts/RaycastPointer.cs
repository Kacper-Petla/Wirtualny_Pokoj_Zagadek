using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(LineRenderer))]
public class RaycastPointer : NetworkBehaviour
{
    [SerializeField] private float maxDistance = 20.0f;
    [SerializeField] private LayerMask interactableLayer = ~0;
    [SerializeField] private Transform rayOrigin;
    [SerializeField] private float mouseSensitivity = 0.1f;

    [SerializeField] private float scrollSpeed = 3.0f;
    [SerializeField] private float minGrabDistance = 0.5f;
    [SerializeField] private float maxGrabDistance = 15.0f;

    private LineRenderer lineRenderer;
    private GrabbableObject grabbedObject = null;
    private float currentHoldDistance;

    public Transform RayOrigin => rayOrigin;
    public bool IsHoldingObject => grabbedObject != null;

    private InteractableHighlight currentHoveredTarget;

    private readonly NetworkVariable<Vector3> networkHitPoint = new(
        writePerm: NetworkVariableWritePermission.Server,
        readPerm: NetworkVariableReadPermission.Everyone
    );

    private readonly NetworkVariable<bool> networkHasHit = new(
        writePerm: NetworkVariableWritePermission.Server,
        readPerm: NetworkVariableReadPermission.Everyone
    );

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = 2;
        if (rayOrigin == null) rayOrigin = transform;
    }

    public override void OnNetworkSpawn()
    {
        networkHitPoint.OnValueChanged += OnHitPointChanged;
    }

    public override void OnNetworkDespawn()
    {
        networkHitPoint.OnValueChanged -= OnHitPointChanged;
    }

    private void Update()
    {
        if (IsServer)
        {
            UpdateAimWithMouse();
            HandleGrabInput();
            PerformRaycast();
            HandleHoverHighlight();
            HandleDistanceAdjustment();
        }

        UpdateVisualLine();
    }

    private void HandleHoverHighlight()
    {
        Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);
        InteractableHighlight newHovered = null;

        if (grabbedObject == null)
        {
            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, interactableLayer))
            {
                newHovered = hit.collider.GetComponentInParent<InteractableHighlight>();
            }
        }

        if (newHovered != currentHoveredTarget)
        {
            if (currentHoveredTarget != null)
            {
                currentHoveredTarget.SetHighlight(false);
            }

            if (newHovered != null)
            {
                newHovered.SetHighlight(true);
            }

            currentHoveredTarget = newHovered;
        }
    }

    private void UpdateAimWithMouse()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity;

        Vector3 currentRot = transform.localEulerAngles;
        float newPitch = currentRot.x - delta.y;
        float newYaw = currentRot.y + delta.x;

        transform.localRotation = Quaternion.Euler(newPitch, newYaw, 0f);
    }

    private void HandleGrabInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            if (grabbedObject != null && (!grabbedObject.IsGrabbed || grabbedObject == null))
            {
                grabbedObject = null;
            }

            if (grabbedObject != null)
            {
                grabbedObject.Drop();
                grabbedObject = null;
                return;
            }

            Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, interactableLayer))
            {
                if (hit.collider.TryGetComponent<GrabbableObject>(out var targetObject))
                {
                    grabbedObject = targetObject;
                    currentHoldDistance = hit.distance;
                    grabbedObject.Grab(rayOrigin, hit.distance);
                }
                else if (hit.collider.TryGetComponent<IInteractable>(out var interactable))
                {
                    interactable.Interact(this, hit);
                }
            }
        }
    }

    private void HandleDistanceAdjustment()
    {
        if (grabbedObject == null) return;

        float verticalInput = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.upArrowKey.isPressed)
            {
                verticalInput += 1f; // Oddalanie (w przód)
            }
            if (Keyboard.current.downArrowKey.isPressed)
            {
                verticalInput -= 1f; // Przybli¿anie (do gracza)
            }
        }

        if (Mathf.Abs(verticalInput) > 0.01f)
        {
            currentHoldDistance += verticalInput * scrollSpeed * Time.deltaTime;
            currentHoldDistance = Mathf.Clamp(currentHoldDistance, minGrabDistance, maxGrabDistance);

            grabbedObject.UpdateHoldDistance(currentHoldDistance);
        }
    }

    private void PerformRaycast()
    {
        Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, interactableLayer))
        {
            networkHitPoint.Value = hit.point;
            networkHasHit.Value = true;
        }
        else
        {
            networkHitPoint.Value = rayOrigin.position + rayOrigin.forward * maxDistance;
            networkHasHit.Value = false;
        }
    }

    private void UpdateVisualLine()
    {
        lineRenderer.SetPosition(0, rayOrigin.position);
        lineRenderer.SetPosition(1, networkHitPoint.Value);
    }

    private void OnHitPointChanged(Vector3 previousValue, Vector3 newValue)
    {
        UpdateVisualLine();
    }

    public void ForceGrab(GrabbableObject obj, float distance)
    {
        if (obj == null) return;

        grabbedObject = obj;
        currentHoldDistance = distance;
        grabbedObject.Grab(rayOrigin, currentHoldDistance);
    }
}