using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 6f;
    [SerializeField] private float runSpeed = 12f;
    
    [SerializeField] private float turnSmoothTime = 0.1f;
    [SerializeField] private float speedSmoothTime = 0.1f;
    [SerializeField] private float gravity = -30f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundDistance = 0.4f;
    [SerializeField] private LayerMask groundMask;
    [Tooltip("Yürünebilir kabul etmek için Ground tag'ı da zorunlu olsun")] 
    [SerializeField] private bool requireGroundTag = true;

    [Header("Camera")]
    [SerializeField] private Transform cameraTransform;

    [Header("Controller Constraints")] 
    [Tooltip("Daha yüksek yüzeylere tırmanmayı engellemek için önerilen düşük değer (ör. 0-0.1).")]
    [SerializeField] private float controllerStepOffset = 0.0f; // 0 = basamak çıkma kapalı
    [Tooltip("Tırmanılabilir maksimum eğim (derece). Daha düşük değer daha az tırmanır.")]
    [SerializeField] private float controllerSlopeLimit = 40f;

    // Private variables
    private CharacterController controller;
    private PlayerAnimationController animController;
    private float turnSmoothVelocity;
    private float speedSmoothVelocity;
    private float currentSpeed;
    private float velocityY;
    private bool isGrounded;
    private bool wasGrounded; // Önceki frame'deki ground durumu
    private bool isRunning;
    private bool isJumping;
    private float lastY; // frame başındaki Y
    private bool nonWalkableLiftBlocked; // bu framede non-walkable yüzeye yükselme oldu mu
    [SerializeField] private float nonWalkableStepEpsilon = 0.02f; // ayak referans toleransı

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animController = GetComponentInChildren<PlayerAnimationController>();

        if (animController == null)
        {
            Debug.LogError("PlayerAnimationController scripti bulunamadı! Lütfen oyuncu nesnesine veya çocuklarından birine ekleyin.", this);
        }

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
        else if(cameraTransform == null)
        {
             Debug.LogError("Kamera Transformu atanmamış ve Ana Kamera bulunamadı!", this);
        }

        if (groundCheck == null)
        {
            Debug.LogWarning("Ground check transform atanmamış. Ayak pozisyonunda bir tane oluşturuluyor.", this);
            GameObject groundCheckObj = new GameObject("GroundCheck");
            groundCheck = groundCheckObj.transform;
            groundCheck.SetParent(transform);
            groundCheck.localPosition = new Vector3(0, -controller.height / 2, 0);
        }

        // Controller kısıtlarını uygula (yükseltideki objelere tırmanmayı engelle)
        if (controller != null)
        {
            controller.stepOffset = Mathf.Max(0f, controllerStepOffset);
            controller.slopeLimit = Mathf.Clamp(controllerSlopeLimit, 0f, 89f);
        }
    }

    private void Update()
    {
    // Başlangıç yüksekliğini kaydet
    lastY = transform.position.y;
    nonWalkableLiftBlocked = false;

        // Yerde olma kontrolü
        CheckGrounded();

        // Girdi al ve hareketi işle
        HandleInputAndMovement();

        // Yerçekimi uygula
        ApplyGravity();

        // Animasyonları güncelle
        UpdateAnimations();

        // Non-walkable yüzeylere çıkmayı engelle: bu framede yukarı doğru yer değiştirdiysek geri al
        if (nonWalkableLiftBlocked)
        {
            float dy = transform.position.y - lastY;
            if (dy > 0f)
            {
                controller.Move(new Vector3(0f, -dy, 0f));
            }
        }
    }

    /// <summary>
    /// Oyuncunun yerde olup olmadığını kontrol eder ve ilgili değişkenleri ayarlar.
    /// </summary>
    private void CheckGrounded()
    {
        wasGrounded = isGrounded;
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        // Yere iniş kontrolü - Landing detection
        if (!wasGrounded && isGrounded)
        {
            OnLanding();
        }
        
        // Yerden ayrılma kontrolü - Takeoff detection  
        if (wasGrounded && !isGrounded)
        {
            OnTakeoff();
        }
    }

    /// <summary>
    /// Yere iniş olayını işler
    /// </summary>
    private void OnLanding()
    {
        isJumping = false;
        velocityY = -2f; // Yerçekimi sıfırlama
        
        // Eğer hareket yoksa hızı sıfırla
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Vector3 direction = new Vector3(horizontal, 0f, vertical).normalized;
        
        if (direction.magnitude < 0.1f)
        {
            currentSpeed = 0f;
        }
    }

    /// <summary>
    /// Yerden ayrılma olayını işler
    /// </summary>
    private void OnTakeoff()
    {
        if (!isJumping) // Eğer zıplama ile değil de düşme ile ayrıldıysak
        {
            isJumping = true;
        }
    }

    /// <summary>
    /// Kullanıcı girdilerini alır ve hareketi, zıplamayı, "spuding"i yönetir.
    /// </summary>
    private void HandleInputAndMovement()
    {
        // Rythm game active? Block movement input
        if (RythmGameManager.RhythmInputLock)
        {
            currentSpeed = 0f;
            return;
        }
        // Animasyon oynarken hareket girişini engelle
        if (animController != null && animController.IsSpuding())
        {
            // Spuding sırasında hareket etmeyi engelle
            currentSpeed = 0f;
            return;
        }

        // Girdi al
        float horizontalInput = Input.GetAxisRaw("Horizontal");
        float verticalInput = Input.GetAxisRaw("Vertical");
        Vector3 directionInput = new Vector3(horizontalInput, 0f, verticalInput).normalized;

        // Koşma kontrolü
        isRunning = Input.GetKey(KeyCode.LeftShift);

        // Hareket etme
        if (directionInput.magnitude >= 0.1f)
        {
            MovePlayer(directionInput);
        }
        else
        {
            // Durmak için yavaşla
            float targetSpeed = 0;
            currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedSmoothVelocity, speedSmoothTime);
            
            // Hareket etmiyorsa CharacterController'ın yatay hızını yavaşla
            Vector3 horizontalVelocity = controller.velocity;
            horizontalVelocity.y = 0;
            if(horizontalVelocity.magnitude > 0.1f && currentSpeed < 0.1f)
            {
                 controller.Move(-horizontalVelocity * Time.deltaTime * 2f); // Daha hızlı durma
            }
        }


    // "Spuding" E tetikleme iptal — ileride farklı plan uygulanacak
        
        // Debug için F tuşu ile animator durumunu kontrol et
        if (Input.GetKeyDown(KeyCode.F) && animController != null)
        {
            animController.DebugCurrentState();
        }
    }

    /// <summary>
    /// Oyuncuyu verilen yöne doğru hareket ettirir ve döndürür.
    /// </summary>
    private void MovePlayer(Vector3 direction)
    {
        if (cameraTransform == null) return;

        float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
        float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
        transform.rotation = Quaternion.Euler(0f, angle, 0f);

        Vector3 moveDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

        float targetSpeed = isRunning ? runSpeed : walkSpeed;
        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedSmoothVelocity, speedSmoothTime);
        controller.Move(moveDirection.normalized * currentSpeed * Time.deltaTime);
    }

    /// <summary>
    /// Zıplama eylemini gerçekleştirir.
    /// </summary>


    /// <summary>
    /// Yerçekimini uygular ve dikey hareketi yönetir.
    /// </summary>
    private void ApplyGravity()
    {
        if (isGrounded && velocityY < 0)
        {
            velocityY = -2f; // Yerdeyken hafif bir yerçekimi uygula
        }
        else
        {
            velocityY += gravity * Time.deltaTime;
        }
        
        controller.Move(new Vector3(0, velocityY * Time.deltaTime, 0));
    }

    /// <summary>
    /// Animasyon kontrolcüsüne güncel durumları gönderir.
    /// </summary>
    private void UpdateAnimations()
    {
        if (animController == null) return;

        float maxSpeed = isRunning ? runSpeed : walkSpeed;
        animController.UpdateSpeed(currentSpeed, maxSpeed, speedSmoothTime);
        animController.UpdateGrounded(isGrounded);
    // Jump animasyonu kaldırıldı; IsJumping parametresi güncellenmiyor
    }

    /// <summary>
    /// Sahnede yer kontrolü için Gizmo çizer.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit == null || hit.collider == null) return;
        // Yürünebilir mi? Layer mask + opsiyonel tag şartı
        bool inMask = (groundMask.value & (1 << hit.collider.gameObject.layer)) != 0;
        bool tagOk = !requireGroundTag || hit.collider.CompareTag("Ground");
        bool isWalkable = inMask && tagOk;

        if (!isWalkable)
        {
            // Temas noktası ayak seviyesinin üzerinde mi? (yukarı tırmanma davranışı)
            float feetY = controller.bounds.min.y; // skinWidth zaten bounds'a dahildir
            if (hit.point.y > feetY + nonWalkableStepEpsilon)
            {
                nonWalkableLiftBlocked = true;
            }
        }
    }

    // Debug için getter'lar
    public bool IsGrounded() => isGrounded;
    public bool IsJumping() => isJumping;
    public float CurrentSpeed() => currentSpeed;
}