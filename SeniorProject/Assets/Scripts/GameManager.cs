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

    [Header("Stability & Safety")]
    [Tooltip("Expand safe bounds by this padding (world units) to avoid edge flicker respawns.")]
    public float boundsPadding = 1.0f;
    [Tooltip("Require being outside continuously for at least this many seconds before respawn.")]
    public float minOutsideDuration = 0.75f;
    [Tooltip("If the player is outside but closer than this to the bounds, treat as inside (hysteresis).")]
    public float minDistanceOutside = 0.5f;
    [Tooltip("Use CharacterController/Collider bounds center instead of raw transform position when available.")]
    public bool preferColliderBounds = true;
    [Tooltip("If grounded and horizontally inside, do not respawn due to tiny vertical jitter above killY.")]
    public bool ignoreWhenGroundedInsideXZ = true;

    [Header("Gizmos")] 
    public bool drawAreaGizmos = true;
    public Color areaFillColor = new Color(0.2f, 0.6f, 1f, 0.12f);
    public Color areaWireColor = new Color(0.2f, 0.6f, 1f, 0.9f);
    public Color respawnColor = new Color(0.2f, 1f, 0.2f, 0.9f);

    private float _nextCheckTime;
    private float _outsideSince = -1f;
    private bool _wasOutside;

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
        var pos = GetBestCheckPosition();

        // Optional: ignore respawn if grounded and horizontally inside (prevents tiny jitter false positives)
        if (ignoreWhenGroundedInsideXZ)
        {
            var cc = player.GetComponent<CharacterController>();
            bool grounded = cc != null ? cc.isGrounded : (player.GetComponent<Rigidbody>() == null || Physics.Raycast(player.position + Vector3.up * 0.1f, Vector3.down, 0.2f));
            if (grounded)
            {
                Vector3 horiz = new Vector3(pos.x, b.center.y, pos.z);
                var bNoY = new Bounds(b.center, new Vector3(b.size.x, Mathf.Max(0.1f, b.size.y), b.size.z));
                if (bNoY.Contains(horiz) && pos.y > killY)
                {
                    _outsideSince = -1f; _wasOutside = false; return;
                }
            }
        }

        bool outside = IsOutside(b, pos);
        if (!outside)
        {
            _outsideSince = -1f;
            _wasOutside = false;
            return;
        }

        // Debounce: require sustained outside state
        if (_outsideSince < 0f) _outsideSince = Time.time;
        _wasOutside = true;
        if (Time.time - _outsideSince >= Mathf.Max(0f, minOutsideDuration))
        {
            RespawnPlayer();
            _outsideSince = -1f;
            _wasOutside = false;
        }
    }

    private Bounds GetAreaBounds()
    {
        Vector3 center = useTransformAsCenter ? transform.position + areaCenter : areaCenter;
        Vector3 size = new Vector3(Mathf.Max(1f, areaSize.x), Mathf.Max(1f, areaSize.y), Mathf.Max(1f, areaSize.z));
        var b = new Bounds(center, size);
        if (boundsPadding > 0f)
        {
            b.Expand(boundsPadding * 2f); // Expand equally in all directions
        }
        return b;
    }

    private Vector3 GetBestCheckPosition()
    {
        if (!preferColliderBounds) return player.position;
        // Prefer CharacterController bounds center
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) return cc.bounds.center;
        var col = player.GetComponent<Collider>();
        if (col != null) return col.bounds.center;
        return player.position;
    }

    private bool IsOutside(Bounds b, Vector3 pos)
    {
        bool outsideBounds = !b.Contains(pos) || pos.y <= killY;
        if (!outsideBounds) return false;
        // Hysteresis: if only slightly outside, allow it
        Vector3 nearest = b.ClosestPoint(pos);
        float dist = Vector3.Distance(nearest, pos);
        if (dist < Mathf.Max(0f, minDistanceOutside) && pos.y > killY)
        {
            return false;
        }
        return true;
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
