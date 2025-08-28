using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class DeerPatroll : MonoBehaviour
{
    [Header("Roam Area")]
    [Tooltip("Devriye merkez noktası (boş bırakılırsa başlangıç pozisyonu kullanılır)")]
    public Transform areaCenter;
    [Tooltip("Geyiğin dolaşacağı yarıçap (metre)")]
    public float roamRadius = 20f;

    [Header("Movement")]
    [Tooltip("NavMesh varsa kullan (önerilir). Yoksa basit yönlendirme ile hareket eder)")]
    public bool useNavMesh = true;
    [Tooltip("İlerleme hızı (m/s)")]
    public float moveSpeed = 2.0f;
    [Tooltip("Dönüş hızı (slerp faktörü)")]
    public float turnSpeed = 5f;
    [Tooltip("Hedefe varmış sayılacağı mesafe")]
    public float arrivalThreshold = 1.2f;

    [Header("Timing")]
    [Tooltip("Hedefe varınca bekleme süresi aralığı (sn)")]
    public Vector2 waitTimeRange = new Vector2(1.5f, 3.5f);

    [Header("Ground Detection")]
    [Tooltip("Zemin Layer'ı (Ground). Boşsa otomatik 'Ground' layer'ını dener")]
    public LayerMask groundMask;
    [Tooltip("Rastgele nokta seçerken yukarıdan aşağı ray atma yüksekliği")]
    public float groundRayHeight = 25f;
    [Tooltip("Ray uzunluğu")]
    public float groundRayLength = 60f;

    [Header("Obstacle Detection")]
    [Tooltip("Engellerin layer mask'ı (Default katmanı dahil edilebilir)")]
    public LayerMask obstacleMask = -1;
    [Tooltip("Önünde engel kontrolü için ray mesafesi")]
    public float obstacleRayDistance = 2.5f;
    [Tooltip("Engel ray'ının yüksekliği (zemin seviyesinden)")]
    public float obstacleRayHeight = 0.8f;
    [Tooltip("Engel ray'larının genişliği (merkez + sol/sağ)")]
    public float obstacleRayWidth = 0.5f;
    [Tooltip("Alternatif rota arama açısı (derece)")]
    public float alternativeRouteAngle = 45f;
    [Tooltip("Alternatif rota deneme sayısı")]
    public int alternativeRouteAttempts = 8;

    [Header("Animation (Optional)")]
    public Animator animator;
    public string speedParam = "Speed";

    // Private
    private NavMeshAgent _agent;
    private Vector3 _targetPos;
    private bool _hasTarget;
    private float _waitUntil;
    private Vector3 _lastPos;
    private float _stuckTimer;
    // Debug
    [Header("Debug Vis")]
    public bool debugDraw = true;
    private Vector3 _dbgRayStart;
    private Vector3 _dbgRayHit;
    private bool _dbgRayOk;
    // Obstacle debug
    private Vector3[] _dbgObstacleRays = new Vector3[6]; // start, end pairs for 3 rays
    private bool[] _dbgObstacleHits = new bool[3];

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        if (useNavMesh && _agent == null)
        {
            // Opsiyonel olarak otomatik ekle
            _agent = gameObject.AddComponent<NavMeshAgent>();
        }
        if (_agent != null)
        {
            _agent.speed = moveSpeed;
            _agent.angularSpeed = 360f;
            _agent.acceleration = 16f;
            _agent.stoppingDistance = Mathf.Max(0.1f, arrivalThreshold * 0.5f);
            _agent.autoBraking = true;
            _agent.updateRotation = true;
        }

        if (groundMask == 0)
        {
            groundMask = LayerMask.GetMask("Ground");
        }

        // Obstacle mask varsayılan olarak Ground hariç her şey
        if (obstacleMask == -1)
        {
            obstacleMask = ~groundMask;
        }

        // Eğer animator atanmadıysa dene
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    void Start()
    {
        // Çoklu geyikler için ufak randomizasyon
        moveSpeed *= Random.Range(0.9f, 1.1f);
        roamRadius *= Random.Range(0.95f, 1.1f);
        if (_agent != null) _agent.speed = moveSpeed;

        // İlk hedefi daha sonra seçelim
        _hasTarget = false;
        _waitUntil = Time.time + Random.Range(0.2f, 0.8f);
        _lastPos = transform.position;
        _stuckTimer = 0f;
    }

    void Update()
    {
        // Bekleme süresi
        if (Time.time < _waitUntil)
        {
            SetAnimSpeed(0f);
            return;
        }

        // Hedef yoksa seç
        if (!_hasTarget)
        {
            if (PickRandomDestination(out _targetPos))
            {
                _hasTarget = true;
                if (useNavMesh && _agent != null && _agent.isOnNavMesh)
                {
                    _agent.SetDestination(_targetPos);
                }
            }
            else
            {
                // Uygun zemin bulunamadıysa kısa bekle ve tekrar dene
                _waitUntil = Time.time + 0.5f;
                return;
            }
        }

        // Hareket
        if (useNavMesh && _agent != null && _agent.isOnNavMesh)
        {
            // NavMesh kullanırken de engel kontrolü yap
            if (IsObstacleInFront())
            {
                // Engel varsa alternatif rota bul
                if (FindAlternativeRoute())
                {
                    // Yeni rota bulundu, hareket animasyonunu güncelle
                    SetAnimSpeed(_agent.desiredVelocity.magnitude);
                }
                else
                {
                    // Hiç rota bulunamadı, yeni hedef seç
                    _hasTarget = false;
                    _waitUntil = Time.time + 0.1f;
                    SetAnimSpeed(0f);
                }
            }
            else
            {
                // Normal hareket
                SetAnimSpeed(_agent.velocity.magnitude);
                if (!_agent.pathPending && _agent.remainingDistance <= arrivalThreshold)
                {
                    Arrived();
                }
            }
        }
        else
        {
            // Transform tabanlı hareket - engel kontrolü
            if (IsObstacleInFront())
            {
                // Engel var, alternatif rota dene
                if (FindAlternativeRoute())
                {
                    // Yeni rota bulundu, bu rotaya doğru hareket et
                    Vector3 to = _targetPos - transform.position; to.y = 0f;
                    float dist = to.magnitude;
                    if (dist > arrivalThreshold)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(to.normalized, Vector3.up);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
                        transform.position += transform.forward * (moveSpeed * Time.deltaTime);
                        SetAnimSpeed(moveSpeed);
                    }
                }
                else
                {
                    // Hiç rota bulunamadı, yeni hedef seç
                    _hasTarget = false;
                    _waitUntil = Time.time + 0.1f;
                    SetAnimSpeed(0f);
                }
            }
            else
            {
                // Normal hareket - engel yok
                Vector3 to = _targetPos - transform.position; to.y = 0f;
                float dist = to.magnitude;
                if (dist > arrivalThreshold)
                {
                    Quaternion targetRot = Quaternion.LookRotation(to.normalized, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
                    transform.position += transform.forward * (moveSpeed * Time.deltaTime);
                    SetAnimSpeed(moveSpeed);
                }
                else
                {
                    Arrived();
                }
            }
        }

    // Her kare zemine yapıştır (ground clamp)
    ClampToGround();

        // Stuck (takılma) tespiti
        float moved = (transform.position - _lastPos).sqrMagnitude;
        _lastPos = transform.position;
        if (moved < 0.0001f)
        {
            _stuckTimer += Time.deltaTime;
            if (_stuckTimer > 2.0f)
            {
                // Yeni hedef seç
                _hasTarget = false;
                _waitUntil = Time.time + 0.3f;
                _stuckTimer = 0f;
            }
        }
        else
        {
            _stuckTimer = 0f;
        }
    }

    private void Arrived()
    {
        _hasTarget = false;
        _waitUntil = Time.time + Random.Range(waitTimeRange.x, waitTimeRange.y);
        SetAnimSpeed(0f);
    }

    private void SetAnimSpeed(float s)
    {
        if (animator != null && !string.IsNullOrEmpty(speedParam))
        {
            animator.SetFloat(speedParam, s);
        }
    }

    // Rastgele hedef noktası seçer ve zemine projeler
    private bool PickRandomDestination(out Vector3 dest)
    {
        Vector3 center = areaCenter != null ? areaCenter.position : transform.position;
        // Rastgele daire içinde nokta
        Vector2 rnd = Random.insideUnitCircle * roamRadius;
        Vector3 sample = new Vector3(center.x + rnd.x, center.y + groundRayHeight, center.z + rnd.y);

        // Aşağı doğru ray atarak zemini bul
        if (Physics.Raycast(sample, Vector3.down, out RaycastHit hit, groundRayLength, groundMask))
        {
            // Debug bilgileri
            _dbgRayStart = sample;
            _dbgRayHit = hit.point;
            _dbgRayOk = true;
            Vector3 point = hit.point;
            // NavMesh kullanılıyorsa NavMesh üzerinde geçerli bir nokta örnekle
            if (useNavMesh && NavMesh.SamplePosition(point, out NavMeshHit navHit, 2.0f, NavMesh.AllAreas))
            {
                dest = navHit.position;
                return true;
            }
            // Fallback: direkt zemini kullan
            dest = point;
            return true;
        }

        // Miss durumunu işaretle
        _dbgRayStart = sample;
        _dbgRayHit = sample + Vector3.down * (groundRayLength * 0.9f);
        _dbgRayOk = false;
        dest = Vector3.zero;
        return false;
    }

    // Zemine yapıştırma
    private void ClampToGround()
    {
        Vector3 origin = transform.position + Vector3.up * groundRayHeight * 0.5f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundRayLength, groundMask))
        {
            Vector3 p = transform.position;
            // Y eksenini zemine ayarla (küçük sarsıntıları önlemek için slerp edilebilir)
            p.y = hit.point.y;
            transform.position = p;
        }
    }

    // Önünde engel var mı kontrol et
    private bool IsObstacleInFront()
    {
        Vector3 origin = transform.position + Vector3.up * obstacleRayHeight;
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        // 3 ray: merkez, sol, sağ
        Vector3[] rayStarts = new Vector3[3];
        Vector3[] rayDirs = new Vector3[3];
        
        rayStarts[0] = origin; // merkez
        rayStarts[1] = origin - right * obstacleRayWidth; // sol
        rayStarts[2] = origin + right * obstacleRayWidth; // sağ
        
        rayDirs[0] = forward;
        rayDirs[1] = forward;
        rayDirs[2] = forward;

        for (int i = 0; i < 3; i++)
        {
            _dbgObstacleRays[i * 2] = rayStarts[i];
            _dbgObstacleRays[i * 2 + 1] = rayStarts[i] + rayDirs[i] * obstacleRayDistance;
            
            if (Physics.Raycast(rayStarts[i], rayDirs[i], obstacleRayDistance, obstacleMask))
            {
                _dbgObstacleHits[i] = true;
                return true; // Engel bulundu
            }
            else
            {
                _dbgObstacleHits[i] = false;
            }
        }
        
        return false; // Yol açık
    }

    // Alternatif rota bulma - engel varsa farklı yönlere deneme yapar
    private bool FindAlternativeRoute()
    {
        Vector3 currentPos = transform.position;
        Vector3 originalTarget = _targetPos;
        
        // Mevcut hedefe olan yön vektörü
        Vector3 toTarget = (originalTarget - currentPos).normalized;
        
        // Farklı açılarda alternatif yönler dene
        for (int i = 1; i <= alternativeRouteAttempts; i++)
        {
            float angle = alternativeRouteAngle * i * (i % 2 == 0 ? 1f : -1f); // Sol-sağ-sol-sağ
            
            // Yeni yön hesapla
            Vector3 newDirection = Quaternion.AngleAxis(angle, Vector3.up) * toTarget;
            
            // Bu yönde engel var mı kontrol et
            if (!IsDirectionBlocked(newDirection))
            {
                // Bu yön açık, yeni hedef belirle
                Vector3 newTarget;
                if (FindValidPositionInDirection(currentPos, newDirection, out newTarget))
                {
                    _targetPos = newTarget;
                    _hasTarget = true; // Önemli: hedef var olduğunu işaretle
                    
                    // NavMesh kullanıyorsa yeni hedef ver
                    if (useNavMesh && _agent != null && _agent.isOnNavMesh)
                    {
                        _agent.SetDestination(_targetPos);
                    }
                    
//                    Debug.Log($"Alternative route found at angle {angle}° to position {newTarget}");
                    return true;
                }
            }
        }
        
        // Hiçbir alternatif bulunamadı
      //  Debug.Log("No alternative route found");
        return false;
    }

    // Belirli bir yönde engel var mı kontrol et
    private bool IsDirectionBlocked(Vector3 direction)
    {
        Vector3 origin = transform.position + Vector3.up * obstacleRayHeight;
        
        // Bu yönde engel kontrolü
        if (Physics.Raycast(origin, direction, obstacleRayDistance, obstacleMask))
        {
            return true; // Engel var
        }
        
        // Sol ve sağdan da kontrol et
        Vector3 right = Vector3.Cross(direction, Vector3.up).normalized;
        Vector3 leftRay = origin - right * obstacleRayWidth;
        Vector3 rightRay = origin + right * obstacleRayWidth;
        
        if (Physics.Raycast(leftRay, direction, obstacleRayDistance, obstacleMask) ||
            Physics.Raycast(rightRay, direction, obstacleRayDistance, obstacleMask))
        {
            return true;
        }
        
        return false; // Yol açık
    }

    // Belirli yönde geçerli bir pozisyon bul
    private bool FindValidPositionInDirection(Vector3 startPos, Vector3 direction, out Vector3 validPos)
    {
        // Roam alanı içinde kalmaya çalış
        Vector3 center = areaCenter != null ? areaCenter.position : startPos;
        float distance = Mathf.Min(roamRadius * 0.5f, obstacleRayDistance * 3f); // Daha uzun mesafe dene
        
        Vector3 targetPoint = startPos + direction * distance;
        
        // Roam alanı sınırlarını kontrol et
        if (Vector3.Distance(targetPoint, center) > roamRadius)
        {
            // Alan dışına çıkıyorsa, alan içine getir
            Vector3 toTarget = (targetPoint - center).normalized;
            targetPoint = center + toTarget * (roamRadius * 0.8f);
        }
        
        // Zemini bul
        Vector3 sample = new Vector3(targetPoint.x, targetPoint.y + groundRayHeight, targetPoint.z);
        if (Physics.Raycast(sample, Vector3.down, out RaycastHit hit, groundRayLength, groundMask))
        {
            validPos = hit.point;
            
            // NavMesh kullanıyorsa NavMesh üzerinde geçerli nokta bul
            if (useNavMesh && NavMesh.SamplePosition(validPos, out NavMeshHit navHit, 3.0f, NavMesh.AllAreas))
            {
                validPos = navHit.position;
            }
            
            Debug.Log($"Valid position found: {validPos}");
            return true;
        }
        
        Debug.Log($"No valid ground found for direction {direction}");
        validPos = Vector3.zero;
        return false;
    }

    // Inspector'da alanı görselleştir
    void OnDrawGizmosSelected()
    {
        // Alanı çiz
        Gizmos.color = new Color(0.2f, 0.8f, 0.3f, 0.35f);
        Vector3 c = areaCenter != null ? areaCenter.position : transform.position;
        Gizmos.DrawSphere(c, 0.2f);
        Gizmos.DrawWireSphere(c, roamRadius);

        if (!debugDraw) return;

        // Son seçilen ray ve isabet noktası
        if (_dbgRayStart != Vector3.zero)
        {
            Gizmos.color = _dbgRayOk ? Color.green : Color.red;
            Gizmos.DrawLine(_dbgRayStart, _dbgRayOk ? _dbgRayHit : _dbgRayHit);
            Gizmos.DrawSphere(_dbgRayOk ? _dbgRayHit : _dbgRayHit, 0.15f);
        }

        // Hedef pozisyonu çiz
        if (_hasTarget)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(_targetPos, 0.2f);
            Gizmos.DrawLine(transform.position, _targetPos);
        }

        // NavMesh yolu (varsa)
        #if UNITY_EDITOR
        if (useNavMesh && _agent != null && _agent.hasPath)
        {
            var p = _agent.path;
            var corners = p.corners;
            Gizmos.color = new Color(0.1f, 0.6f, 1f, 1f);
            for (int i = 0; i < corners.Length - 1; i++)
            {
                Gizmos.DrawLine(corners[i], corners[i + 1]);
                Gizmos.DrawSphere(corners[i], 0.075f);
            }
            if (corners.Length > 0) Gizmos.DrawSphere(corners[corners.Length - 1], 0.1f);
        }
        #endif

        // Engel ray'larını çiz
        if (debugDraw && _dbgObstacleRays != null && _dbgObstacleRays.Length >= 6)
        {
            for (int i = 0; i < 3; i++)
            {
                Gizmos.color = _dbgObstacleHits[i] ? Color.red : Color.cyan;
                Vector3 start = _dbgObstacleRays[i * 2];
                Vector3 end = _dbgObstacleRays[i * 2 + 1];
                if (start != Vector3.zero && end != Vector3.zero)
                {
                    Gizmos.DrawLine(start, end);
                    Gizmos.DrawSphere(end, 0.05f);
                }
            }
        }
    }
}
