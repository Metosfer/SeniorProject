using UnityEngine;

namespace EazyCamera
{
    using Util = EazyCameraUtility;
    
    [RequireComponent(typeof(Camera))]
    [ExecuteInEditMode]
    public class EazyCam : MonoBehaviour
    {
        [System.Serializable]
        public struct Settings
        {
            [Header("Temel")]
            [Tooltip("Kameranın hedefe olan başlangıç uzaklığı (negatif değer kamerayı hedefin arkasına alır).")]
            public float Distance;
            [Tooltip("Kameranın varsayılan dönüşü (X = dikey, Y = yatay derece).")]
            public Vector2 DefaultRotation;
            public float DefaultDistance { get; set; }

            [Header("Orbit")]
            [Tooltip("Fareyle hedef etrafında dönmeyi (orbit) etkinleştir.")]
            public bool OrbitEnabled;

            [Header("Movement")]
            [Tooltip("Kameranın odak noktasına doğru hareket hızı.")]
            public float MoveSpeed;
            [Tooltip("Odak noktasına yaklaşırken kullanılan yaklaştırma katsayısı (1 = anında).")]
            [Range(.1f, 1f)] public float SnapFactor;
            [Tooltip("Kameranın odak noktası gerisinde kalmasına izin verilen maksimum mesafe.")]
            [Range(0f, 10f)] public float MaxLagDistance;

            [Header("Rotation")]
            [Tooltip("Orbit sırasında dönüş hızı.")]
            public float RotationSpeed;
            [Tooltip("Dikey dönüş (pitch) için minimum ve maksimum açı sınırı (derece). Burayı kısıtlayarak kameranın zeminin altını görmesini engelleyebilirsiniz.")]
            public FloatRange VerticalRotation;
            [Tooltip("Hareket yumuşatma için kullanılan eğri (0..1 arası ilerleme → hız katsayısı).")]
            public AnimationCurve EaseCurve;
            [Space]
            [Tooltip("Yatay dönüşü (yaw) belirtilen aralıkta sınırla.")]
            public bool LimitHorizontalRotation;
            [Tooltip("Yatay dönüş (yaw) için minimum ve maksimum açı sınırı (derece). Sadece LimitHorizontalRotation açıkken uygulanır.")]
            public FloatRange HorizontalRotation;

            [Header("Input")]
            [Tooltip("Fare hassasiyeti katsayısı (1 = varsayılan).")]
            public float MouseSensitivity;
            [Tooltip("Yatay (Mouse X) girişini ters çevir.")]
            public bool InvertX;
            [Tooltip("Dikey (Mouse Y) girişini ters çevir.")]
            public bool InvertY;

            [Header("Collision")]
            [Tooltip("Kamera ile çevre arasındaki çarpışmayı kontrol et (duvarın içine girmeyi engeller).")]
            public bool EnableCollision;
            [Tooltip("Kamera çarpışması için dikkate alınacak katman maskesi.")]
            public LayerMask CollisionLayer;
            [Tooltip("Çarpışma algılandığında kameranın hedef mesafeye daha pürüzsüz yaklaşmasını sağlar.")]
            public bool CollisionSmoothingEnabled;
            [Tooltip("Çarpışma sırasında kamera mesafesinin hedef değere yumuşatılarak yaklaşma süresi (sn). 0 = anında.")]
            [Min(0f)] public float CollisionSmoothTime;

            [Header("Zoom")]
            [Tooltip("Fare tekeri vb. ile yakınlaştırmayı etkinleştir.")]
            public bool EnableZoom;
            public float ZoomDistance { get; set; }
            [Tooltip("Yakınlaştırma için izin verilen mesafe aralığı (negatif değerler hedefin arkasını ifade eder).")]
            public FloatRange ZoomRange;

            [Header("Target Offset")]
            [Tooltip("Kamera odak noktasını hedef pozisyondan bu kadar kaydır (dünya uzayı).")]
            public Vector3 TargetOffset;

            [Header("Targeting")]
            [Tooltip("Hedef kilitleme (lock-on) sistemini etkinleştir.")]
            public bool EnableTargetLock;
            [Tooltip("Hedef kilit ikonu (opsiyonel).")]
            public EazyTargetReticle TargetLockIcon;
        }

    [SerializeField]
    [Tooltip("Kameranın takip edeceği hedef (genellikle oyuncu karakterinin transformu).")]
    private Transform _followTarget = null;

        public Settings CameraSettings => _settings;
        [SerializeField] private Settings _settings = new Settings()
        {
            Distance = -5f,
            DefaultDistance = -5f,
            OrbitEnabled = true,
            MoveSpeed = 5f,
            SnapFactor = .75f,
            MaxLagDistance = 1f,
            RotationSpeed = 30f,
            VerticalRotation = new FloatRange(-89f, 89f),
            EaseCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f),
            LimitHorizontalRotation = false,
            HorizontalRotation = new FloatRange(-360f, 360f),
            MouseSensitivity = 1f,
            InvertX = false,
            EnableCollision = true,
            CollisionSmoothingEnabled = true,
            CollisionSmoothTime = 0.15f,
            EnableZoom = true,
            ZoomDistance = -5,
            ZoomRange = new FloatRange(-10f, -1f),
            EnableTargetLock = true,
            TargetOffset = Vector3.zero,
        };
        
        public Transform TargetRoot { get; private set; }

        public Vector3 FocalPoint => _focalPoint;
        private Vector3 _focalPoint = new Vector3();

        public Camera UnityCamera { get; private set; }
        
        public Transform CameraTransform => _transform;
        private Transform _transform = null;

        private Vector2 _rotation = new Vector2();

        private EazyCollider _collider = null;
        private EazyTargetManager _targetManager = null;
        
    // Dahili: çarpışma ve zoom kaynaklı mesafe değişimlerini pürüzsüzleştirmek için
    private float _currentDistance = 0f;
    private float _distanceDampVelocity = 0f;

        private bool IsTargetLockAllowed => _settings.EnableTargetLock && _targetManager != null;

        private ITargetable _lookTargetOverride = null;
        public bool OverrideLookTarget => _lookTargetOverride != null;

        private void Awake()
        {
            _transform = this.transform;
            
            Debug.Assert(_followTarget != null, "Target should not be null on an EazyCam component");
            TargetRoot = _followTarget.root;

            UnityCamera = this.GetComponent<Camera>();

            if (_settings.EnableCollision)
            {
                _collider = new EazyCollider(this);
            }

            if (_settings.EnableTargetLock)
            {
                _targetManager = new EazyTargetManager(this);
            }
        }

        private void Start()
        {
            _settings.DefaultDistance = _settings.Distance;
            _settings.ZoomDistance = _settings.Distance;

            _rotation = _settings.DefaultRotation;
            Quaternion rotation = CalculateRotationFromVector(_rotation);
            _focalPoint = _followTarget.position + _settings.TargetOffset;
            _currentDistance = GetDistance();
            Vector3 position = _focalPoint + ((rotation * Vector3.forward) * _currentDistance);
            _transform.SetPositionAndRotation(position, Quaternion.LookRotation(_focalPoint - position));

            // Cursor görünür olsun - UI etkileşimi için gerekli
            if (Application.isPlaying)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (OverrideLookTarget)
            {
                TargetedLook(dt);
            }
            else
            {
                DefaultLook(dt);
            }

            DebugDrawFocualCross(_focalPoint);

            if (_targetManager != null)
            {
                _targetManager.Tick(dt);
            }

            if (_collider != null)
            {
                _collider.Tick();
            }

            Debug.DrawLine(_focalPoint, _transform.position, Color.black);
        }

        private void DefaultLook(float deltaTime)
        {
            UpdatePosition(deltaTime);
#if UNITY_EDITOR
            Quaternion rotation = CalculateRotationFromVector(Application.isPlaying ? _rotation : _settings.DefaultRotation);
#else
            Quaternion rotation = CalculateRotationFromVector(_rotation);
#endif // UNITY_EDITOR

            float effectiveDistance = GetEffectiveDistance(deltaTime);
            Vector3 position = _focalPoint + ((rotation * Vector3.forward) * effectiveDistance);

            _transform.SetPositionAndRotation(position, Quaternion.LookRotation(_focalPoint - position));
        }

        private void TargetedLook(float deltaTime)
        {
            UpdatePosition(deltaTime);
            Vector3 lookDirection = _lookTargetOverride.LookAtPosition - CameraTransform.position;
            Quaternion rotation = Quaternion.LookRotation(lookDirection);
            float effectiveDistance = GetEffectiveDistance(deltaTime);
            Vector3 position = _focalPoint + ((rotation * Vector3.forward) * effectiveDistance);
            _transform.SetPositionAndRotation(position, rotation);
        }

        private void UpdatePosition(float deltaTime)
        {
            Vector3 targetPos = _followTarget.position + _settings.TargetOffset;
            if (_settings.MaxLagDistance > 0f && _settings.SnapFactor < 1f)
            {
                Vector3 travelDirection = targetPos - _focalPoint;
                float travelDistance = travelDirection.sqrMagnitude;
                float maxDistance = _settings.MaxLagDistance.Squared();

                if (travelDistance > maxDistance)
                {
                    _focalPoint = Vector3.MoveTowards(_focalPoint, targetPos - (travelDirection.normalized * _settings.MaxLagDistance), _settings.MoveSpeed * deltaTime);
                }
                else if (travelDistance < Constants.DeadZone.Squared())
                {
                    _focalPoint = targetPos;
                }

                float pointOnCurve = 1 - Mathf.Clamp01(travelDistance / maxDistance);
                float speed = _settings.MoveSpeed * _settings.EaseCurve.Evaluate(pointOnCurve);

                float step = _settings.SnapFactor * deltaTime * speed;

                _focalPoint = Vector3.MoveTowards(_focalPoint, targetPos, step);
            }
            else
            {
                _focalPoint = targetPos;
            }
        }

        public void SetRotation(float horzRot, float vertRot)
        {
            _rotation.x = vertRot;
            _rotation.y = horzRot;

            ClampHorizontalRotation();
            ClampVerticalRotation();
        }

        public void SetRotationUnclamped(float horzRot, float vertRot)
        {
            _rotation.x = vertRot;
            _rotation.y = horzRot;
        }

        public void IncreaseRotation(float horzRotDelta, float vertRotDelta, float deltaTime)
        {
            if (_settings.OrbitEnabled)
            {
                Vector2 rotation = new Vector2(horzRotDelta, vertRotDelta);
                if (rotation.sqrMagnitude > 1f)
                {
                    rotation.Normalize();
                    horzRotDelta = rotation.x;
                    vertRotDelta = rotation.y;
                }

                float sensitivity = Mathf.Max(0f, _settings.MouseSensitivity);
                float step = deltaTime * _settings.RotationSpeed * sensitivity;
                float horz = _settings.InvertX ? -horzRotDelta : horzRotDelta;
                float vert = _settings.InvertY ? vertRotDelta : -vertRotDelta;

                _rotation.y += horz * step;
                ClampHorizontalRotation();

                _rotation.x += vert * step;
                ClampVerticalRotation();
            }
        }

        private void ClampHorizontalRotation()
        {
            if (_settings.LimitHorizontalRotation)
            {
                _rotation.y = Mathf.Clamp(_rotation.y, _settings.HorizontalRotation.Min, _settings.HorizontalRotation.Max);
            }
            else
            {
                // Yalnızca sayıyı sınırlı aralıkta tutmak için wrap uygula
                if (_rotation.y > 360f)
                {
                    _rotation.y -= 360f;
                }
                else if (_rotation.y < -360f)
                {
                    _rotation.y += 360f;
                }
            }
        }

        private void ClampVerticalRotation()
        {
            _rotation.x = Mathf.Clamp(_rotation.x, _settings.VerticalRotation.Min, _settings.VerticalRotation.Max);
        }

        public Vector3 GetDefaultPosition()
        {
            float distance = _settings.EnableZoom ? _settings.ZoomDistance : _settings.Distance;
            return _focalPoint + ((CalculateRotationFromVector(_rotation) * Vector3.forward) * distance);
        }

        private float GetEffectiveDistance(float deltaTime)
        {
            // Hedef mesafe (zoom ve çarpışma sonrası değerlere göre)
            float targetDistance = GetDistance();

            if (Application.isPlaying && _settings.EnableCollision && _settings.CollisionSmoothingEnabled && _settings.CollisionSmoothTime > 0f)
            {
                _currentDistance = Mathf.SmoothDamp(
                    _currentDistance,
                    targetDistance,
                    ref _distanceDampVelocity,
                    _settings.CollisionSmoothTime,
                    Mathf.Infinity,
                    deltaTime
                );
            }
            else
            {
                _currentDistance = targetDistance;
                _distanceDampVelocity = 0f;
            }

            return _currentDistance;
        }

        private Quaternion CalculateRotationFromVector(Vector3 rotation)
        {
            Quaternion addRot = Quaternion.Euler(0f, rotation.y, 0f);
            return addRot * Quaternion.Euler(rotation.x, 0f, 0f); // Not commutative
        }

        private void DebugDrawFocualCross(Vector3 position)
        {
            Debug.DrawLine(position - (Vector3.up / 2f), position + (Vector3.up / 2f), Color.green);
            Debug.DrawLine(position - (Vector3.right / 2f), position + (Vector3.right / 2f), Color.red);
            Debug.DrawLine(position - (Vector3.forward / 2f), position + (Vector3.forward / 2f), Color.blue);
        }
        
        /// <summary>
        /// Directly sets the distance of the camera. If zoom is enabled, it will chose the farther of the two values
        /// </summary>
        /// <param name="distance"></param>
        public void SetDistance(float distance)
        {
            if (_settings.EnableZoom)
            {
                // #DG: account for camera in front (take abs of both values and pick the smaller)
                _settings.Distance = Mathf.Max(distance, _settings.ZoomDistance);
            }
            else
            {
                _settings.Distance = distance;
            }
        }

        /// <summary>
        /// Sets Zoom Distance of the camera. If distance is not in the allowed range, it will be clamped to the range
        /// </summary>
        /// <param name="zoomDistance"></param>
        public void SetZoomDistance(float zoomDistance)
        {
            _settings.ZoomDistance = Util.ClampToRange(zoomDistance, _settings.ZoomRange);
            SetDistance(_settings.ZoomDistance);
        }

        public void IncreaseZoomDistance(float inputDelta, float deltaTime)
        {
            if (_settings.EnableZoom)
            {
                inputDelta = Mathf.Clamp(inputDelta, -_settings.MoveSpeed, _settings.MoveSpeed);
                inputDelta *= _settings.MoveSpeed * deltaTime;
                inputDelta += _settings.ZoomDistance;
                _settings.ZoomDistance = Util.ClampToRange(inputDelta, _settings.ZoomRange);
                SetDistance(_settings.ZoomDistance);
            }
        }

        public float GetDistance()
        {
            if (_settings.EnableZoom)
            {
                return Mathf.Max(_settings.Distance, _settings.ZoomDistance);
            }

            return _settings.Distance;
        }

        public void ResetToUnoccludedDistance()
        {
            _settings.Distance = _settings.EnableZoom ? _settings.ZoomDistance : _settings.DefaultDistance;
        }

        public void ResetToDefaultDistance()
        {
            _settings.Distance = _settings.DefaultDistance;
        }

        public void SetZoomEnabled(EnabledState state)
        {
            _settings.EnableZoom = state == EnabledState.Enabled;
        }

        public void SetCollisionEnabled(EnabledState state)
        {
            _settings.EnableCollision = state == EnabledState.Enabled;
            if (_settings.EnableCollision)
            {
                if (_collider == null)
                {
                    _collider = new EazyCollider(this);
                }
            }
            else
            {
                if (_collider != null)
                {
                    ResetToUnoccludedDistance();
                }

                _collider = null;
            }
        }

        public void ResetPositionAndRotation()
        {
            _rotation = new Vector2();
            _focalPoint = _followTarget.position + _settings.TargetOffset;
            ResetToDefaultDistance();
        }

        public bool PointIsOnScreen(Vector2 point)
        {
            Rect rect = new Rect(0f, 0f, Screen.width, Screen.height);
            return rect.Contains(point);
        }

        public void SetOrbitEnabled(EnabledState state)
        {
            _settings.OrbitEnabled = state == EnabledState.Enabled ? true : false;
        }

        //
        // Targeting
#region Targeting
        public void SetTargetingEnabled(EnabledState state)
        {
            _settings.EnableTargetLock = state == EnabledState.Enabled;
            if (_settings.EnableCollision)
            {
                if (_targetManager == null)
                {
                    _targetManager = new EazyTargetManager(this);
                }
            }
            else
            {
                if (_targetManager != null)
                {
                    _targetManager.ClearTargetsInRange();
                }

                _targetManager = null;
            }
        }

        public void AddTargetInRange(ITargetable target)
        {
            if (IsTargetLockAllowed)
            {
                _targetManager.AddTargetInRange(target);
            }
        }

        public void RemoveTargetInRange(ITargetable target)
        {
            if (IsTargetLockAllowed)
            {
                _targetManager.AddTargetInRange(target);
            }
        }

        public void BeginLockOn()
        {
            if (IsTargetLockAllowed)
            {
                _targetManager.BeginTargetLock();
            }
        }

        public void EndLockOn()
        {
            if (IsTargetLockAllowed)
            {
                _targetManager.EndTargetLock();
            }
        }

        public void ToggleLockOn()
        {
            if (IsTargetLockAllowed)
            {
                _targetManager.ToggleLockOn();
            }
        }

        public void SetLookTargetOverride(ITargetable targetOverride)
        {
            _lookTargetOverride = targetOverride;
        }

        public void ClearLookTargetOverride()
        {
            _lookTargetOverride = null;
            Vector3 currentRotation = _settings.OrbitEnabled ? _transform.rotation.eulerAngles : _settings.DefaultRotation;

            SetRotation(currentRotation.y, currentRotation.x);
        }

        public void CycleTargets()
        {
            _targetManager.CycleTargets();
        }

        public void CycleTargetsRight()
        {
            _targetManager.CycleTargets(_transform.right);
        }

        public void CycleTargetsLeft()
        {
            _targetManager.CycleTargets(-_transform.right);
        }

        public void CycleTargets(Vector3 direction)
        {
            _targetManager.CycleTargets(direction);
        }

#endregion // Targeting

        public void OverrideSettings(Settings settings)
        {
            _settings = settings;
        }
    }
}

