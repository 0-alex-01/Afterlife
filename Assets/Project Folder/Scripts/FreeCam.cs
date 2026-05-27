using UnityEngine;

public class FreeCam : MonoBehaviour
{
    public float moveSpeed = 8f;
    public float fastMoveMultiplier = 3f;
    public float lookSensitivity = 2f;

    private float yaw;
    private float pitch;

    void Start()
    {
        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = angles.x;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        LookAround();
        MoveCamera();

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (Input.GetMouseButtonDown(0))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void LookAround()
    {
        yaw += Input.GetAxis("Mouse X") * lookSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * lookSensitivity;
        pitch = Mathf.Clamp(pitch, -89f, 89f);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    void MoveCamera()
    {
        float speed = moveSpeed;

        if (Input.GetKey(KeyCode.LeftShift))
            speed *= fastMoveMultiplier;

        Vector3 direction = Vector3.zero;

        if (Input.GetKey(KeyCode.W))
            direction += transform.forward;

        if (Input.GetKey(KeyCode.S))
            direction -= transform.forward;

        if (Input.GetKey(KeyCode.D))
            direction += transform.right;

        if (Input.GetKey(KeyCode.A))
            direction -= transform.right;

        if (Input.GetKey(KeyCode.E))
            direction += transform.up;

        if (Input.GetKey(KeyCode.Q))
            direction -= transform.up;

        transform.position += direction * speed * Time.deltaTime;
    }
}