using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 6f;
    [SerializeField] private float runSpeed = 12f;
    [SerializeField] private float jumpForce = 7f;
    [SerializeField] private float turnSmoothTime = 0.1f;
    [SerializeField] private float speedSmoothTime = 0.1f;
    [SerializeField] private float gravity = -30f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundDistance = 0.4f;
    [SerializeField] private LayerMask groundMask;

    [Header("Camera")]
    [SerializeField] private Transform cameraTransform;

    // Private variables
    private CharacterController controller;
    private Animator animator;
    private float turnSmoothVelocity;
    private float speedSmoothVelocity;
    private float currentSpeed;
    private float velocityY;
    private bool isGrounded;
    private bool isRunning;
    private bool isSpuding; // Yeni eklenen animasyonlar için (örneğin Spuding)

    // Animation parameter hashes for better performance
    private int speedHash;
    private int groundedHash;
    private int jumpHash;
    private int spudingHash; // Yeni eklenen animasyonlar için

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();

        if (cameraTransform == null)
        {
            cameraTransform = Camera.main.transform;
        }

        if (groundCheck == null)
        {
            Debug.LogWarning("Ground check transform not assigned. Creating one at feet position.");
            groundCheck = new GameObject("GroundCheck").transform;
            groundCheck.SetParent(transform);
            groundCheck.localPosition = new Vector3(0, -0.9f, 0);
        }

        // Set up animation hashes
        if (animator != null)
        {
            speedHash = Animator.StringToHash("Speed");
            groundedHash = Animator.StringToHash("Grounded");
            jumpHash = Animator.StringToHash("Jump");
            spudingHash = Animator.StringToHash("isSpuding"); // Yeni eklenen hash
        }
    }

    private void Update()
    {
        // Check if player is grounded
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        // Get movement input
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Vector3 direction = new Vector3(horizontal, 0f, vertical).normalized;

        // Handle running
        isRunning = Input.GetKey(KeyCode.LeftShift);

        // Handle movement
        if (direction.magnitude >= 0.1f)
        {
            MovePlayer(direction);
        }
        else
        {
            // Slow down to stop
            float targetSpeed = 0;
            currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedSmoothVelocity, speedSmoothTime);
        }

        // Handle jumping
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            Jump();
        }

        // Handle spuding (örnek bir input)
        if (Input.GetKeyDown(KeyCode.E) && isGrounded && !isSpuding)
        {
            StartSpuding();
        }

        // Apply gravity
        ApplyGravity();

        // Update animations
        UpdateAnimations(direction.magnitude);
    }

    private void MovePlayer(Vector3 direction)
    {
        // Kameranın yönüne göre hedef açıyı hesapla
        float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
        float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
        transform.rotation = Quaternion.Euler(0f, angle, 0f);

        // Hareket yönünü doğrudan kameranın forward vektörüne göre hesapla
        Vector3 moveDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
        moveDirection.y = 0f;
        moveDirection = moveDirection.normalized;

        float targetSpeed = isRunning ? runSpeed : walkSpeed;
        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedSmoothVelocity, speedSmoothTime);
        controller.Move(moveDirection * currentSpeed * Time.deltaTime);
    }

    private void Jump()
    {
        velocityY = jumpForce;
        if (animator != null)
        {
            animator.SetTrigger(jumpHash);
        }
    }

    private void ApplyGravity()
    {
        if (isGrounded && velocityY < 0)
        {
            velocityY = -2f;
        }
        velocityY += gravity * Time.deltaTime;
        controller.Move(new Vector3(0, velocityY * Time.deltaTime, 0));
    }

    private void UpdateAnimations(float inputMagnitude)
    {
        if (animator == null) return;

        // Normalize speed based on max speed for Blend Tree
        float maxSpeed = isRunning ? runSpeed : walkSpeed;
        float normalizedSpeed = currentSpeed / maxSpeed;
        animator.SetFloat(speedHash, normalizedSpeed, speedSmoothTime, Time.deltaTime);

        // Update grounded state
        animator.SetBool(groundedHash, isGrounded);

        // Update spuding state (örnek)
        animator.SetBool(spudingHash, isSpuding);
    }

    private void StartSpuding()
    {
        isSpuding = true;
        animator.SetBool(spudingHash, true);
    }

    // Animation Event ile çağrılacak (Spuding animasyonu bittiğinde)
    public void OnSpudingAnimationEnd()
    {
        isSpuding = false;
        animator.SetBool(spudingHash, false);
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawSphere(groundCheck.position, groundDistance);
        }
    }
}