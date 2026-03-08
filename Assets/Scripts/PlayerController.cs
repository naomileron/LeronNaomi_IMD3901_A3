using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

//class code
public class PlayerController : NetworkBehaviour
{
    public float speed = 5f;
    public float mouseSensitivity = 2f;

    public CharacterController controller;
    public Transform cameraTransform;

    public Camera playerCamera;

    float xRotation = 0f;

    private void Awake()
    {
        // Prevent spawn-collision correction during the same frame as spawn
        if (controller != null) controller.enabled = false;
    }

    public override void OnNetworkSpawn()
    {
        
        if(!IsOwner)
        {
            playerCamera.enabled = false;
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        StartCoroutine(EnableControllerNextFrame());

    }

    private System.Collections.IEnumerator EnableControllerNextFrame()
    {
        yield return null;
        if (controller != null)
        {
            controller.center = new Vector3(0f, controller.height * 0.5f, 0f);
            controller.enabled = true;
        }
    }

    void Update()
    {

        if(!IsOwner)
        {
            return;
        }

        if (controller == null || !controller.enabled)
        {
            return;
        }

        Vector2 moveInput = Keyboard.current != null ? new Vector2 
            (
                (Keyboard.current.aKey.isPressed ? -1 : 0) + (Keyboard.current.dKey.isPressed ? 1 : 0),
                (Keyboard.current.sKey.isPressed ? -1 : 0) + (Keyboard.current.wKey.isPressed ? 1 : 0)
            ) : Vector2.zero;   

        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        controller.Move(move * speed * Time.deltaTime);

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        float mouseX = mouseDelta.x * mouseSensitivity * Time.deltaTime;
        float mouseY = mouseDelta.y * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);

        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
        

    }
}
