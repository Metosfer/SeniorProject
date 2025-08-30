using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EazyCamera
{
    using EazyCamera.Events;
    using Util = EazyCameraUtility;

    public class EazyController : MonoBehaviour
    {
        [SerializeField] private EazyCam _controlledCamera = null;
    [Header("Input Options")]
    [Tooltip("Sadece sağ tık basılıyken kamerayı fare ile döndür")] 
    [SerializeField] private bool _rotateOnlyWhileRightMouse = false;
    [Tooltip("Sağ tık ile döndürürken özel imleç göster")] 
    [SerializeField] private bool _changeCursorWhileRotating = true;
    [Tooltip("Döndürme sırasında gösterilecek imleç görseli (opsiyonel)")] 
    [SerializeField] private Texture2D _rotateCursor = null;
    [SerializeField] private Vector2 _rotateCursorHotspot = new Vector2(8,8);
    [SerializeField] private CursorMode _rotateCursorMode = CursorMode.Auto;
    [Header("Cursor Sizing")]
    [Tooltip("İmleci hedef boyuta yeniden ölçekle")] 
    [SerializeField] private bool _resizeRotateCursor = false;
    [Tooltip("Hedef imleç boyutu (px)")] 
    [SerializeField] private int _rotateCursorSize = 32;
    [Tooltip("Ekran DPI değerine göre otomatik boyutlandır")] 
    [SerializeField] private bool _autoAdjustRotateCursorForDPI = true;

    private bool _cursorOwned = false;
    private Texture2D _scaledRotateCursor = null;
    private Vector2 _scaledRotateCursorHotspot = Vector2.zero;
    private bool _cursorDirty = true;

        private void Start()
        {
            Debug.Assert(_controlledCamera != null, "Attempting to use a controller on a GameOjbect without an EazyCam component");
#if ENABLE_INPUT_SYSTEM
            SetupInput();
#endif
        }

        private void Update()
        {
            float dt = Time.deltaTime;

#if ENABLE_INPUT_SYSTEM
            HandleInput(dt);
#else
            HandleLegacyInput(dt);
#endif // ENABLE_INPUT_SYSTEM
        }

        public void SetControlledCamera(EazyCam cam)
        {
            _controlledCamera = cam;
        }

        private void ToggleLockOn()
        {
            _controlledCamera.ToggleLockOn();
        }

        private void CycleTargets()
        {
            _controlledCamera.CycleTargets();
        }

        private void CycleRight()
        {
            _controlledCamera.CycleTargetsRight();
        }

        private void CycleLeft()
        {
            _controlledCamera.CycleTargetsLeft();
        }

        private void ToggleUi()
        {
            EazyEventManager.TriggerEvent(EazyEventKey.OnUiToggled);
        }

#if ENABLE_INPUT_SYSTEM
        [SerializeField] private InputAction _toggleLockOn = new InputAction("ToggleLock");
        [SerializeField] private InputAction _cycleTargets = new InputAction("Cycle");
        [SerializeField] private InputAction _cycleRight = new InputAction("CycleRight");
        [SerializeField] private InputAction _cycleLeft = new InputAction("CycleLeft");
        [SerializeField] private InputAction _zoom = new InputAction("Zoom");
        [SerializeField] private InputAction _orbit = new InputAction("Orbit");
        [SerializeField] private InputAction _toggleUi = new InputAction("ToggleUi");
        [SerializeField] private InputAction _reset = new InputAction("ResetCamera");

        private Vector2 _rotation = new Vector2();

        public void HandleInput(float dt)
        {
            Vector2 rot = _rotation;
            bool rmb = false;
            if (_rotateOnlyWhileRightMouse)
            {
#if ENABLE_INPUT_SYSTEM
                // If a mouse is present, require RMB to be pressed to rotate
                if (UnityEngine.InputSystem.Mouse.current != null)
                {
                    rmb = UnityEngine.InputSystem.Mouse.current.rightButton.isPressed;
                    if (!rmb) rot = Vector2.zero;
                }
#endif
            }
            UpdateRotateCursorState(active: _rotateOnlyWhileRightMouse && _changeCursorWhileRotating && rmb);
            _controlledCamera.IncreaseRotation(rot.x, rot.y, dt);
            
        }

        public void SetupInput()
        {
            Validate();

            _toggleLockOn.canceled += ctx => ToggleLockOn();
            _toggleLockOn.Enable();

            _cycleTargets.performed += ctx => CycleTargets();
            _cycleTargets.Enable();

            _cycleRight.performed += ctx => CycleRight();
            _cycleRight.Enable();

            _cycleLeft.performed += ctx => CycleLeft();
            _cycleLeft.Enable();

            _toggleUi.canceled += ctx => ToggleUi();
            _toggleUi.Enable();

            _reset.canceled += ctx => _controlledCamera.ResetPositionAndRotation();
            _reset.Enable();

            _zoom.performed += OnZoom;
            _zoom.canceled += OnZoom;
            _zoom.Enable();

            _orbit.performed += OnOrbit;
            _orbit.canceled += OnOrbit;
            _orbit.Enable();
        }

        private void OnZoom(InputAction.CallbackContext ctx)
        {
            _controlledCamera.IncreaseZoomDistance(ctx.ReadValue<Vector2>().y, Time.deltaTime);
        }

        private void OnOrbit(InputAction.CallbackContext ctx)
        {
            _rotation = ctx.ReadValue<Vector2>();
        }

        private void Validate()
        {
            if (_toggleLockOn.bindings.Count == 0)
            {
                _toggleLockOn.AddBinding(Keyboard.current.tKey);
                _toggleLockOn.AddBinding(Gamepad.current.leftTrigger);
            }

            if (_cycleTargets.bindings.Count == 0)
            {
                _cycleTargets.AddBinding(Keyboard.current.spaceKey);
            }

            if (_cycleRight.bindings.Count == 0)
            {
                _cycleRight.AddBinding(Keyboard.current.eKey);
                _cycleRight.AddBinding(Gamepad.current.rightShoulder);
            }

            if (_cycleLeft.bindings.Count == 0)
            {
                _cycleLeft.AddBinding(Keyboard.current.qKey);
                _cycleLeft.AddBinding(Gamepad.current.leftShoulder);
            }

            if (_zoom.bindings.Count == 0)
            {
                _zoom.AddBinding(Mouse.current.scroll);
            }

            if (_orbit.bindings.Count == 0)
            {
                _orbit.AddBinding(Mouse.current.delta);
                _orbit.AddBinding(Gamepad.current.rightStick);
            }

            if (_toggleUi.bindings.Count == 0)
            {
                _toggleUi.AddBinding(Keyboard.current.uKey);
                _toggleUi.AddBinding(Gamepad.current.startButton);
            }

            if (_reset.bindings.Count == 0)
            {
                _reset.AddBinding(Keyboard.current.rKey);
                _reset.AddBinding(Gamepad.current.rightStickButton);
            }
        }

        private void OnValidate()
        {
            Validate();
        }

#else
        public void HandleLegacyInput(float dt)
        {
            float scrollDelta = Input.mouseScrollDelta.y;
            if (scrollDelta > Constants.DeadZone || scrollDelta < -Constants.DeadZone)
            {
                _controlledCamera.IncreaseZoomDistance(scrollDelta, dt);
            }

            // Rotate only if RMB pressed when the option is enabled
            bool rotatingNow = !_rotateOnlyWhileRightMouse || Input.GetMouseButton(1);
            UpdateRotateCursorState(active: _rotateOnlyWhileRightMouse && _changeCursorWhileRotating && Input.GetMouseButton(1));
            if (rotatingNow)
            {
                float horz = Input.GetAxis(Util.MouseX);
                float vert = Input.GetAxis(Util.MouseY);
                _controlledCamera.IncreaseRotation(horz, vert, dt);
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                _controlledCamera.ResetPositionAndRotation();
            }

            if (Input.GetKeyUp(KeyCode.T))
            {
                ToggleLockOn();
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                CycleTargets();
            }

            if (Input.GetKeyDown(KeyCode.Q))
            {
                CycleLeft();
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                CycleRight();
            }

            if (Input.GetKeyDown(KeyCode.U))
            {
                ToggleUi();
            }
        }
#endif // ENABLE_INPUT_SYSTEM

        private void UpdateRotateCursorState(bool active)
        {
            if (!_changeCursorWhileRotating) return;
            if (active)
            {
                if (!_cursorOwned)
                {
                    EnsureScaledRotateCursor();
                    var tex = _scaledRotateCursor != null ? _scaledRotateCursor : _rotateCursor;
                    var hs = _scaledRotateCursor != null ? _scaledRotateCursorHotspot : _rotateCursorHotspot;
                    Cursor.SetCursor(tex, hs, _rotateCursorMode);
                    _cursorOwned = true;
                }
            }
            else if (_cursorOwned)
            {
                var cm = CursorManager.Instance;
                if (cm != null) cm.UseDefaultNow(); else Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                _cursorOwned = false;
            }
        }

        private void OnDisable()
        {
            if (_cursorOwned)
            {
                var cm = CursorManager.Instance;
                if (cm != null) cm.UseDefaultNow(); else Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                _cursorOwned = false;
            }
            if (_scaledRotateCursor != null)
            {
                Destroy(_scaledRotateCursor);
                _scaledRotateCursor = null;
            }
        }

        private void OnDestroy()
        {
            if (_cursorOwned)
            {
                var cm = CursorManager.Instance;
                if (cm != null) cm.UseDefaultNow(); else Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                _cursorOwned = false;
            }
            if (_scaledRotateCursor != null)
            {
                Destroy(_scaledRotateCursor);
                _scaledRotateCursor = null;
            }
        }

        private void EnsureScaledRotateCursor()
        {
            if (!_resizeRotateCursor || _rotateCursor == null)
            {
                if (_scaledRotateCursor != null)
                {
                    Destroy(_scaledRotateCursor);
                    _scaledRotateCursor = null;
                }
                return;
            }
            if (!_cursorDirty && _scaledRotateCursor != null) return;

            int target = Mathf.Clamp(_rotateCursorSize, 8, 256);
            float dpi = Screen.dpi;
            if (_autoAdjustRotateCursorForDPI && dpi > 1f)
            {
                float scale = dpi / 96f;
                target = Mathf.Clamp(Mathf.RoundToInt(target * scale), 8, 256);
            }
            int srcW = _rotateCursor.width;
            int srcH = _rotateCursor.height;
            int maxSrc = Mathf.Max(srcW, srcH);
            float scaleF = maxSrc > 0 ? (float)target / maxSrc : 1f;
            int dstW = Mathf.Max(8, Mathf.RoundToInt(srcW * scaleF));
            int dstH = Mathf.Max(8, Mathf.RoundToInt(srcH * scaleF));

            var rt = RenderTexture.GetTemporary(dstW, dstH, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            Graphics.Blit(_rotateCursor, rt);
            RenderTexture.active = rt;
            var tex = new Texture2D(dstW, dstH, TextureFormat.RGBA32, false, false);
            tex.ReadPixels(new Rect(0, 0, dstW, dstH), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            tex.filterMode = FilterMode.Bilinear;

            if (_scaledRotateCursor != null) Destroy(_scaledRotateCursor);
            _scaledRotateCursor = tex;
            _scaledRotateCursorHotspot = _rotateCursorHotspot * scaleF;
            _cursorDirty = false;
        }
    }


}
