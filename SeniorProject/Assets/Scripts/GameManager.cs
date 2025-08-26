using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Player Reference")]
    [Tooltip("Takip edilecek oyuncu Transform'u. Boşsa otomatik bulunabilir (tag: Player).")]
    public Transform player;
    [Tooltip("Boşsa Player tag'lı objeyi otomatik bul.")]
    public bool autoFindPlayerByTag = true;

    [Header("Safe Area (Editörden Ayarlanır)")]
    [Tooltip("Alan merkezi için bu objenin Transform'unu kullan.")]
    public bool useTransformAsCenter = true;
    [Tooltip("Alan merkezi (useTransformAsCenter kapalı ise dünya koordinatı).")]
    public Vector3 areaCenter = Vector3.zero;
    [Tooltip("Alan boyutu (X/Z genişlik-derinlik, Y yükseklik).")]
    public Vector3 areaSize = new Vector3(100f, 50f, 100f);

    [Header("Respawn Settings")]
    [Tooltip("Oyuncunun spawn olacağı nokta.")]
    public Transform respawnPoint;
    [Tooltip("Bu Y değerinin altına düşerse otomatik respawn yapılır.")]
    public float killY = -50f;
    [Tooltip("Respawn sonrasında zemine oturt (raycast ile).")]
    public bool snapToGround = true;
    public LayerMask groundMask = ~0;
    public float rayStartHeight = 2f;
    public float rayMaxDistance = 100f;
    public float groundClearance = 0.05f;
    [Tooltip("Kontrol sıklığı (saniye). 0 = her frame.")]
    public float checkInterval = 0.2f;

    [Header("Gizmos")] 
    public bool drawAreaGizmos = true;
    public Color areaFillColor = new Color(0.2f, 0.6f, 1f, 0.12f);
    public Color areaWireColor = new Color(0.2f, 0.6f, 1f, 0.9f);
    public Color respawnColor = new Color(0.2f, 1f, 0.2f, 0.9f);

    private float _nextCheckTime;

    private void Awake()
    {
        if (autoFindPlayerByTag && player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) player = go.transform;
        }
    }

    private void Start()
    {
        // Sahne başında da güvenli alanda değilse respawn
        TryRespawnIfOutside();
    }

    private void Update()
    {
        if (checkInterval <= 0f || Time.time >= _nextCheckTime)
        {
            TryRespawnIfOutside();
            _nextCheckTime = Time.time + Mathf.Max(0.01f, checkInterval);
        }
    }

    private void TryRespawnIfOutside()
    {
        if (player == null) return;
        var b = GetAreaBounds();
        var pos = player.position;
        bool outside = !b.Contains(pos) || pos.y <= killY;
        if (outside)
        {
            RespawnPlayer();
        }
    }

    private Bounds GetAreaBounds()
    {
        Vector3 center = useTransformAsCenter ? transform.position + areaCenter : areaCenter;
        Vector3 size = new Vector3(Mathf.Max(1f, areaSize.x), Mathf.Max(1f, areaSize.y), Mathf.Max(1f, areaSize.z));
        return new Bounds(center, size);
    }

    private void RespawnPlayer()
    {
        if (player == null) return;
        Vector3 target = respawnPoint != null ? respawnPoint.position : (useTransformAsCenter ? transform.position : areaCenter);

        if (snapToGround)
        {
            Vector3 start = target + Vector3.up * Mathf.Max(0.01f, rayStartHeight);
            if (Physics.Raycast(start, Vector3.down, out var hit, rayMaxDistance, groundMask))
            {
                target = hit.point + Vector3.up * groundClearance;
            }
        }

        var cc = player.GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = false;
            player.position = target;
            cc.enabled = true;
        }
        else
        {
            player.position = target;
        }

        var rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawAreaGizmos) return;
        var b = GetAreaBounds();
        Gizmos.color = areaFillColor;
        Gizmos.DrawCube(b.center, b.size);
        Gizmos.color = areaWireColor;
        Gizmos.DrawWireCube(b.center, b.size);

        if (respawnPoint != null)
        {
            Gizmos.color = respawnColor;
            Gizmos.DrawSphere(respawnPoint.position, 0.25f);
        }
    }
}
