using UnityEngine;
using UnityEngine.InputSystem;

namespace Camera
{
    public class DebugCamera : MonoBehaviour
    {
		public static DebugCamera Instance { get; private set; }

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

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else DestroyImmediate(gameObject);
        }

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

            var movement = new Vector3(0f, 0f, 0f);
            
            switch (moveInput.x)
            {
                case > 0f:
                    movement += transform.rotation * Vector3.right;
                    break;
                case < 0f:
                    movement += transform.rotation * Vector3.left;
                    break;
            }
            
            switch (moveInput.y)
            {
                case > 0f:
                    movement += transform.rotation * Vector3.forward;
                    break;
                case < 0f:
                    movement += transform.rotation * Vector3.back;
                    break;
            }
            
            var currentMoveSpeed = movementSpeed;
            if (_sprintAction.IsPressed())
                currentMoveSpeed *= 3f;

            _transform.Translate(movement * (currentMoveSpeed * Time.deltaTime), Space.World);
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
