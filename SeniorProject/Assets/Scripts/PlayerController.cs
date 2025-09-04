using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    
    private CharacterController controller;
    private Vector3 moveDirection;
    
    void Start()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null)
        {
            Debug.LogWarning("CharacterController component bulunamadı!");
        }
    }
    
    void Update()
    {
        HandleMovement();
    }
    
    void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        
        Vector3 direction = new Vector3(horizontal, 0, vertical).normalized;
        
        if (direction.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref rotationSpeed, 0.1f);
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.up);
            
            moveDirection = direction * moveSpeed;
        }
        else
        {
            moveDirection = Vector3.zero;
        }
        
        // Yerçekimi ekle
        if (!controller.isGrounded)
        {
            moveDirection.y -= 9.81f * Time.deltaTime;
        }
        
        if (controller != null)
        {
            controller.Move(moveDirection * Time.deltaTime);
        }
    }
}
