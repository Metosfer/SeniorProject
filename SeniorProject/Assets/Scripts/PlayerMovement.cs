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

    // Animation parameter hashes for better performance
    private int speedHash;
    private int groundedHash;
    private int jumpHash;

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
        }

        // Lock cursor for third person camera
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
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

        // Apply gravity
        ApplyGravity();

        // Update animations
        UpdateAnimations(direction.magnitude);
    }

    private void MovePlayer(Vector3 direction)
    {
        // Calculate the target angle based on input direction and camera
        float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
        
        // Smoothly rotate player to the movement direction
        float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
        transform.rotation = Quaternion.Euler(0f, angle, 0f);

        // Calculate move direction
        Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

        // Determine movement speed (walk or run)
        float targetSpeed = isRunning ? runSpeed : walkSpeed;
        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedSmoothVelocity, speedSmoothTime);

        // Move the player
        controller.Move(moveDir.normalized * currentSpeed * Time.deltaTime);
    }

    private void Jump()
    {
        velocityY = jumpForce;
        
        // Trigger jump animation
        if (animator != null)
        {
            animator.SetTrigger(jumpHash);
        }
    }

    private void ApplyGravity()
    {
        // Reset Y velocity when grounded
        if (isGrounded && velocityY < 0)
        {
            velocityY = -2f; // Small negative value to keep player grounded
        }

        // Apply gravity
        velocityY += gravity * Time.deltaTime;
        
        // Apply vertical movement
        Vector3 verticalMovement = new Vector3(0, velocityY * Time.deltaTime, 0);
        controller.Move(verticalMovement);
    }

    private void UpdateAnimations(float inputMagnitude)
    {
        if (animator == null) return;

        // Update speed parameter (normalized between walk and run)
        float animationSpeedPercent = isRunning ? inputMagnitude : inputMagnitude * 0.5f;
        animator.SetFloat(speedHash, animationSpeedPercent, speedSmoothTime, Time.deltaTime);
        
        // Update grounded state
        animator.SetBool(groundedHash, isGrounded);
    }

    // Use this for visualization during development
    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawSphere(groundCheck.position, groundDistance);
        }
    }
}