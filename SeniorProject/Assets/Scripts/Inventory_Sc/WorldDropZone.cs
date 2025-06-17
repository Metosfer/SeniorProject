using UnityEngine;

public class WorldDropZone : DropZone
{
    [Header("World Drop Zone Settings")]
    public LayerMask groundLayer = 1; // Zemin layer'ı
    public float maxDropDistance = 20f; // Maksimum drop mesafesi
    
    private Camera playerCamera;

    private void Start()
    {
        // Player camera'yı bul
        playerCamera = Camera.main;
        if (playerCamera == null)
        {
            playerCamera = FindObjectOfType<Camera>();
        }
    }

    public override bool CanAcceptDrop()
    {
        return true; // World drop zone her zaman kabul eder
    }

    public Vector3 GetDropPosition(Vector3 screenPosition)
    {
        if (playerCamera == null)
        {
            Debug.LogWarning("WorldDropZone: Player camera bulunamadı!");
            return Vector3.zero;
        }

        // Screen pozisyonundan ray çek
        Ray ray = playerCamera.ScreenPointToRay(screenPosition);
        RaycastHit hit;

        // Zemin ile çarpışma kontrolü
        if (Physics.Raycast(ray, out hit, maxDropDistance, groundLayer))
        {
            return hit.point + Vector3.up * 0.5f; // Yere değmemesi için biraz yukarı
        }
        else
        {
            // Eğer zemin ile çarpışma yoksa, ray üzerinde varsayılan mesafede bir nokta
            Vector3 worldPos = ray.GetPoint(10f);
            worldPos.y = 0.5f; // Varsayılan yükseklik
            return worldPos;
        }
    }

    // Bu method UI'dan çağrılabilir
    public bool IsValidDropPosition(Vector3 worldPosition)
    {
        // Pozisyonun geçerli olup olmadığını kontrol et
        // Örneğin: engellerle çarpışma kontrolü
        Collider[] overlapping = Physics.OverlapSphere(worldPosition, 0.5f);
        
        foreach (Collider col in overlapping)
        {
            // Eğer başka bir world item veya engel varsa
            if (col.CompareTag("WorldItem") || col.CompareTag("Obstacle"))
            {
                return false;
            }
        }
        
        return true;
    }
}
