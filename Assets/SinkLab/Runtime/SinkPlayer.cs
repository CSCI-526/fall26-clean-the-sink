using UnityEngine;
using UnityEngine.InputSystem;

namespace SinkLab
{
    /// <summary>First-person controls. Water is the player's only way to affect the mess.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class SinkPlayer : MonoBehaviour
    {
        public Camera viewCamera;
        public WaterJet water;
        public SinkWorld world;
        public bool InputEnabled = true;
        [Min(0.1f)] public float moveSpeed = 2f;
        [Min(0.001f)] public float lookSensitivity = 0.1f;
        [Range(5f, 85f)] public float initialPitch = 35f;

        public bool HasControl => InputEnabled && _hasControl;
        public float Yaw => _yaw;
        public float Pitch => _pitch;

        CharacterController _controller;
        Vector3 _spawnPosition;
        float _spawnYaw;
        float _yaw;
        float _pitch;
        float _verticalSpeed;
        bool _hasControl;
        bool _mustReleaseFire;
        bool _spawnCaptured;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        void Start()
        {
            if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
            CaptureSpawn();
            SetView(_spawnYaw, initialPitch);
            SetControl(true);
        }

        void Update()
        {
            if (!InputEnabled)
            {
                StopWater();
                return;
            }

            Keyboard keys = Keyboard.current;
            Mouse mouse = Mouse.current;

            // Reset is available with either captured or released mouse input.
            if (keys != null && keys.fKey.wasPressedThisFrame && world != null && world.drain != null)
                world.drain.TryOpenFully();

            if (keys != null && keys.rKey.wasPressedThisFrame && world != null)
            {
                world.ResetRun();
                _mustReleaseFire = true;
                StopWater();
                return;
            }

            if (keys != null && (keys.escapeKey.wasPressedThisFrame || keys.tabKey.wasPressedThisFrame))
            {
                SetControl(false);
                return;
            }

            if (!_hasControl)
            {
                StopWater();
                if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                    SetControl(true);
                return;
            }

            if (mouse != null)
            {
                Vector2 look = mouse.delta.ReadValue() * lookSensitivity;
                SetView(_yaw + look.x, _pitch - look.y);

                if (!mouse.leftButton.isPressed) _mustReleaseFire = false;
                if (water != null)
                {
                    float scroll = mouse.scroll.ReadValue().y;
                    if (Mathf.Abs(scroll) > 0.01f)
                        water.Pressure = Mathf.Clamp01(water.Pressure + Mathf.Sign(scroll) * 0.1f);
                    if (mouse.rightButton.wasPressedThisFrame)
                        water.WideSpray = !water.WideSpray;
                    bool canSpray = !_mustReleaseFire && (world == null || !world.IsDrainSealed);
                    water.SetSpraying(mouse.leftButton.isPressed && canSpray);
                }
            }
            else StopWater();

            Vector2 movement = Vector2.zero;
            if (keys != null)
            {
                movement.x = (keys.dKey.isPressed ? 1f : 0f) - (keys.aKey.isPressed ? 1f : 0f);
                movement.y = (keys.wKey.isPressed ? 1f : 0f) - (keys.sKey.isPressed ? 1f : 0f);
                if (world != null && world.drain != null && keys.eKey.wasPressedThisFrame)
                    world.drain.ToggleOpen();
                if (water != null)
                {
                    if (keys.qKey.wasPressedThisFrame) water.WideSpray = !water.WideSpray;
                    if (keys.digit1Key.wasPressedThisFrame) water.WideSpray = false;
                    if (keys.digit2Key.wasPressedThisFrame) water.WideSpray = true;
                }
            }
            Move(movement, Time.deltaTime);
        }

        /// <summary>Degrees: yaw about world up and positive pitch looking down.</summary>
        public void SetView(float yaw, float pitch)
        {
            _yaw = Mathf.Repeat(yaw + 180f, 360f) - 180f;
            _pitch = Mathf.Clamp(pitch, -25f, 85f);
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (viewCamera != null) viewCamera.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        /// <summary>The same collision-based movement used by human input, also callable by gameplay tests.</summary>
        public void Move(Vector2 input, float dt)
        {
            if (_controller == null) _controller = GetComponent<CharacterController>();
            if (_controller == null || !_controller.enabled || dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            input = Vector2.ClampMagnitude(input, 1f);
            Vector3 horizontal = transform.right * input.x + transform.forward * input.y;
            if (_controller.isGrounded && _verticalSpeed < 0f) _verticalSpeed = -2f;
            _verticalSpeed += Physics.gravity.y * dt;
            CollisionFlags collisions = _controller.Move((horizontal * moveSpeed + Vector3.up * _verticalSpeed) * dt);
            if ((collisions & CollisionFlags.Below) != 0 && _verticalSpeed < 0f) _verticalSpeed = -2f;
            if ((collisions & CollisionFlags.Above) != 0 && _verticalSpeed > 0f) _verticalSpeed = 0f;
        }

        public void SetControl(bool enabled)
        {
            _hasControl = enabled;
            _mustReleaseFire = true;
            Cursor.lockState = enabled ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !enabled;
            StopWater();
        }

        public void ResetPlayer()
        {
            if (!_spawnCaptured) CaptureSpawn();
            bool controllerEnabled = _controller != null && _controller.enabled;
            if (controllerEnabled) _controller.enabled = false;
            transform.position = _spawnPosition;
            SetView(_spawnYaw, initialPitch);
            _verticalSpeed = 0f;
            if (controllerEnabled) _controller.enabled = true;
            _mustReleaseFire = true;
            StopWater();
        }

        void CaptureSpawn()
        {
            if (_spawnCaptured) return;
            _spawnPosition = transform.position;
            _spawnYaw = transform.eulerAngles.y;
            _spawnCaptured = true;
        }

        void StopWater()
        {
            if (water != null) water.SetSpraying(false);
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused) SetControl(false);
        }

        void OnDisable()
        {
            SetControl(false);
        }
    }
}
