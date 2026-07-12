using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class DesktopPlayerController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Camera Offset innerhalb des XR Origin, nicht die Main Camera.")]
    [SerializeField] private Transform cameraOffset;

    [Tooltip("Main Camera innerhalb des Camera Offset.")]
    [SerializeField] private Transform playerCamera;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float gravity = -9.81f;

    [Header("Mouse Look")]
    [SerializeField] private float mouseSensitivity = 0.18f;
    [SerializeField] private float maximumLookAngle = 80f;

    [Header("Keyboard Turning")]
    [Tooltip("Drehgeschwindigkeit für Q/E und Pfeiltasten.")]
    [SerializeField] private float keyboardTurnSpeed = 100f;

    [Header("Testing")]
    [SerializeField] private bool desktopControlEnabled = true;
    [SerializeField] private bool showDebugLogs = true;

    private CharacterController characterController;

    private float verticalVelocity;
    private float cameraPitch;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (playerCamera == null)
        {
            Camera foundCamera = GetComponentInChildren<Camera>(true);

            if (foundCamera != null)
            {
                playerCamera = foundCamera.transform;
            }
        }

        if (cameraOffset == null && playerCamera != null)
        {
            cameraOffset = playerCamera.parent;
        }
    }

    private void Start()
    {
        if (!desktopControlEnabled)
        {
            return;
        }

        LockCursor();

        if (showDebugLogs)
        {
            Debug.Log(
                $"DesktopPlayerController gestartet auf '{name}'.",
                this
            );
        }

        if (playerCamera == null)
        {
            Debug.LogError(
                "DesktopPlayerController: Main Camera fehlt.",
                this
            );
        }

        if (cameraOffset == null)
        {
            Debug.LogError(
                "DesktopPlayerController: Camera Offset fehlt.",
                this
            );
        }
    }

    private void Update()
    {
        if (!desktopControlEnabled)
        {
            return;
        }

        HandleCursor();
        HandleMovement();
        HandleKeyboardTurning();
        HandleMouseLook();
    }

    private void HandleMovement()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        Vector2 input = Vector2.zero;

        if (Keyboard.current.wKey.isPressed)
            input.y += 1f;

        if (Keyboard.current.sKey.isPressed)
            input.y -= 1f;

        if (Keyboard.current.dKey.isPressed)
            input.x += 1f;

        if (Keyboard.current.aKey.isPressed)
            input.x -= 1f;

        input = Vector2.ClampMagnitude(input, 1f);

        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 horizontalMovement =
            forward * input.y +
            right * input.x;

        if (characterController.isGrounded &&
            verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 movement =
            horizontalMovement * moveSpeed;

        movement.y = verticalVelocity;

        characterController.Move(
            movement * Time.deltaTime
        );
    }

    private void HandleKeyboardTurning()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        float turnInput = 0f;

        if (Keyboard.current.qKey.isPressed ||
            Keyboard.current.leftArrowKey.isPressed)
        {
            turnInput -= 1f;
        }

        if (Keyboard.current.eKey.isPressed ||
            Keyboard.current.rightArrowKey.isPressed)
        {
            turnInput += 1f;
        }

        if (Mathf.Abs(turnInput) > 0.01f)
        {
            transform.Rotate(
                Vector3.up,
                turnInput *
                keyboardTurnSpeed *
                Time.deltaTime,
                Space.Self
            );
        }
    }

    private void HandleMouseLook()
    {
        if (Mouse.current == null)
        {
            return;
        }

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        Vector2 mouseDelta =
            Mouse.current.delta.ReadValue();

        float yaw =
            mouseDelta.x * mouseSensitivity;

        float pitch =
            mouseDelta.y * mouseSensitivity;

        // Links und rechts: gesamten XR Origin drehen.
        transform.Rotate(
            Vector3.up,
            yaw,
            Space.Self
        );

        // Hoch und runter: Camera Offset drehen.
        // Nicht die Main Camera, weil der Tracked Pose Driver
        // deren Rotation überschreiben kann.
        cameraPitch -= pitch;

        cameraPitch = Mathf.Clamp(
            cameraPitch,
            -maximumLookAngle,
            maximumLookAngle
        );

        if (cameraOffset != null)
        {
            cameraOffset.localRotation =
                Quaternion.Euler(
                    cameraPitch,
                    0f,
                    0f
                );
        }
    }

    private void HandleCursor()
    {
        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            UnlockCursor();
        }

        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame &&
            Cursor.lockState != CursorLockMode.Locked)
        {
            LockCursor();
        }
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}