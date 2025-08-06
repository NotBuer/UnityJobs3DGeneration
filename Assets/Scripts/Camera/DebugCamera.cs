using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Camera
{
    public class DebugCamera : MonoBehaviour
    {
        [Range(1f, 100f)] [SerializeField] private float movementSpeed = 25f;
        [Range(1f, 100f)] [SerializeField] private float mouseSpeed = 25f;
        [SerializeField] private bool showCursor = true;

        private Transform _transform;
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _sprintAction;

        private float mouseX;
        private float mouseY;

        private bool focused;

        private void Start()
        {
            _transform = gameObject.transform;
            _moveAction = InputSystem.actions.FindAction("Move");
            _lookAction = InputSystem.actions.FindAction("Look");
            _sprintAction  = InputSystem.actions.FindAction("Sprint");
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = showCursor;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            focused = hasFocus;
        }

        private void Update()
        {
            if (!focused) return;
            
            CameraMovement();
            CameraRotation();
        }

        private void CameraMovement()
        {
            if (!_moveAction.IsPressed()) return;
            
            var moveInput = _moveAction.ReadValue<Vector2>();

            var currentMoveSpeed = movementSpeed;
            if (_sprintAction.IsPressed())
                currentMoveSpeed *= 2f;

            var move = new Vector3(moveInput.x, transform.forward.y, moveInput.y);

            move = Quaternion.Euler(0, transform.eulerAngles.y, 0) * move;

            _transform.Translate(move * (currentMoveSpeed * Time.deltaTime), Space.World);
        }

        private void CameraRotation()
        {
            var lookInput = _lookAction.ReadValue<Vector2>();
            
            mouseX += lookInput.x * (mouseSpeed * Time.deltaTime);
            mouseY -= lookInput.y * (mouseSpeed * Time.deltaTime);
            
            mouseY = Mathf.Clamp(mouseY, -90f, 90f);
            
            //Unity uses ZXY Euler angle order, that's why the reverse order of the rotation.
            transform.eulerAngles = new Vector3(mouseY, mouseX, 0f);
        }
    }
}
