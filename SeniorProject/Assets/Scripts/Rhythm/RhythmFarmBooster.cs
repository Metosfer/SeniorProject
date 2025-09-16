using UnityEngine;
using System.Collections;
using TMPro;

// Attach this to your farm rhythm station object. When player is near and presses E,
// it opens the rhythm mini-game, and on finish applies a growth-time boost to the
// nearest FarmingAreaManager based on score/maxCombo.
public class RhythmFarmBooster : MonoBehaviour
{
    [Header("Refs")]
    public RythmGameManager rhythm;
    public FarmingAreaManager farmingArea; // optional; if null, finds nearest in scene
    public Transform player; // optional; auto-find by tag "Player"
    private PlayerAnimationController _playerAnim;
    [Tooltip("If true and no area is assigned, bind to the nearest FarmingAreaManager on Awake")] public bool bindNearestOnAwake = true;
    [Tooltip("Max distance to bind when searching nearest on Awake (0 = unlimited)")] public float bindMaxDistance = 0f;
    [Tooltip("TMP_Text to display the applied boost result")] public TMP_Text boostResultText;

    [Header("Interaction")]
    public float interactRange = 2.5f;
    public KeyCode interactKey = KeyCode.E;
    public GameObject interactPromptUI; // e.g., "Press E to play"
    [Tooltip("Text component used inside the interact prompt UI to show messages")] public TMP_Text interactPromptText;
    [Tooltip("Optional text to show errors/info like 'No seeds planted'.")] public TMP_Text infoText;
    [Tooltip("Error message text to show when no seeds are planted (assign TextMeshPro)")] public TMP_Text errorText;

    [Header("Boost Mapping")] 
    [Tooltip("Seconds added per 1000 score")] public float secondsPerThousandScore = 2.0f;
    [Tooltip("Extra seconds per 10 max combo (only used if includeComboBonus=true)")] public float secondsPerTenCombo = 0.5f;
    [Tooltip("Total cap per run (s)")] public float maxTotalSeconds = 15f;
    [Tooltip("If true, add a small bonus from max combo; otherwise use score only")] public bool includeComboBonus = false;

    private bool _inRange;
    private bool _playing;

    private void Awake()
    {
        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) player = go.transform;
        }
        if (player != null)
        {
            _playerAnim = player.GetComponent<PlayerAnimationController>();
        }
        
        // Error text'i ve boost result text'i başlangıçta gizle
        if (errorText != null)
            errorText.gameObject.SetActive(false);
            
        if (boostResultText != null)
            boostResultText.gameObject.SetActive(false);
            
        // Bind to nearest area once if not assigned
        if (farmingArea == null && bindNearestOnAwake)
        {
            var nearest = FindNearestFarmingArea();
            if (nearest != null)
            {
                if (bindMaxDistance <= 0f || (nearest.transform.position - transform.position).sqrMagnitude <= bindMaxDistance * bindMaxDistance)
                {
                    farmingArea = nearest;
                }
            }
        }
        if (rhythm != null)
        {
            rhythm.OnFinished += OnRhythmFinished;
        }
        // Auto-assign prompt text from the prompt UI if not provided
        if (interactPromptText == null && interactPromptUI != null)
        {
            interactPromptText = interactPromptUI.GetComponentInChildren<TMP_Text>(true);
        }
        if (interactPromptUI != null) interactPromptUI.SetActive(false);
    }

    private void OnDestroy()
    {
        if (rhythm != null) rhythm.OnFinished -= OnRhythmFinished;
    }

    private void Update()
    {
        if (rhythm == null) return;

        // If rhythm UI is active, suppress prompt
        if (_playing)
        {
            // If UI was closed via ESC/E from rhythm manager, drop playing state
            if (rhythm.rootUI != null && !rhythm.rootUI.activeSelf)
            {
                _playing = false;
                if (_playerAnim != null) _playerAnim.SetDancing(false);
                if (interactPromptUI != null && _inRange) interactPromptUI.SetActive(true);
                return;
            }
            if (interactPromptUI != null && interactPromptUI.activeSelf) interactPromptUI.SetActive(false);
            return;
        }

        // Range check (XZ plane)
        if (player != null)
        {
            Vector3 a = new Vector3(transform.position.x, 0f, transform.position.z);
            Vector3 b = new Vector3(player.position.x, 0f, player.position.z);
            _inRange = (a - b).sqrMagnitude <= interactRange * interactRange;
        }
        else _inRange = false;

        if (interactPromptUI != null) interactPromptUI.SetActive(_inRange);

        // Update prompt message when in range
        if (_inRange && interactPromptText != null)
        {
            bool canOpen = farmingArea != null && farmingArea.HasAnyGrowingSeed();
            interactPromptText.text = canOpen ? "Press E to play" : "Add water first";
        }

        if (_inRange && Input.GetKeyDown(interactKey))
        {
            // Block if there is no planted seed in range/target area
            var targetArea = farmingArea; // must be bound/assigned
            bool canOpen = targetArea != null && targetArea.HasAnyGrowingSeed();
            if (!canOpen)
            {
                // Show error message for no seeds planted
                ShowNoSeedsError();
                
                // Ensure prompt shows the correct warning
                if (interactPromptText != null) interactPromptText.text = "Add water first";
                if (infoText != null) { infoText.text = ""; }
                return;
            }
            // Begin mini-game
            _playing = true;
            
            // Error mesajını ve boost result'ını gizle (eğer görünüyorsa)
            HideErrorMessage();
            HideBoostResult();
            
            if (rhythm.rootUI != null) rhythm.rootUI.SetActive(true);
            rhythm.StartGame();
            if (_playerAnim != null) _playerAnim.SetDancing(true);
        }
    }

    private IEnumerator ClearInfoAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (infoText != null) infoText.text = string.Empty;
    }

    private void OnRhythmFinished(int score, int maxCombo)
    {
        _playing = false;
        if (_playerAnim != null) _playerAnim.SetDancing(false);
        if (interactPromptUI != null && _inRange) interactPromptUI.SetActive(true);

        // Map score/combo to seconds
        float secFromScore = Mathf.Max(0f, score) * (secondsPerThousandScore / 1000f);
        float secFromCombo = includeComboBonus ? (Mathf.Max(0, maxCombo) * (secondsPerTenCombo / 10f)) : 0f;
        float total = Mathf.Min(maxTotalSeconds, secFromScore + secFromCombo);
        
        if (total <= 0.01f) 
        {
            // Çok düşük skor - boost uygulanmadı mesajı göster
            ShowBoostResult("No boost applied - Score too low!", Color.yellow);
            return;
        }

        // Find target farming area if missing
        var target = farmingArea;
        if (target == null) 
        {
            ShowBoostResult("No farming area found!", Color.red);
            return;
        }

        float applied = target.ApplyGrowthBoost(total);
        Debug.Log($"RhythmFarmBooster applied {applied:F1}s growth boost (requested {total:F1}s) to {target.name}");
        
        // Boost sonucunu her zaman göster
        if (applied > 0f)
        {
            ShowBoostResult($"🌱 Boost applied: {applied:F1}s! 🌱", Color.green);
        }
        else
        {
            ShowBoostResult("Boost could not be applied!", new Color(1f, 0.5f, 0f)); // Orange color
        }
    }

    private FarmingAreaManager FindNearestFarmingArea()
    {
        var all = GameObject.FindObjectsOfType<FarmingAreaManager>();
        if (all == null || all.Length == 0) return null;
        float best = float.MaxValue; FarmingAreaManager bestFA = null;
        for (int i = 0; i < all.Length; i++)
        {
            var fa = all[i]; if (fa == null) continue;
            float sqr = (fa.transform.position - transform.position).sqrMagnitude;
            if (sqr < best) { best = sqr; bestFA = fa; }
        }
        return bestFA;
    }

    // -------- Boost Result Display System --------
    
    /// <summary>
    /// Show boost result message with specified text and color
    /// </summary>
    private void ShowBoostResult(string message, Color textColor)
    {
        if (boostResultText != null)
        {
            boostResultText.gameObject.SetActive(true);
            boostResultText.text = message;
            boostResultText.color = textColor;
            
            // 4 saniye sonra boost mesajını gizle
            StartCoroutine(HideBoostResultAfterDelay(4f));
            
            Debug.Log($"Boost result shown: {message}");
        }
        else
        {
            Debug.LogWarning("BoostResultText not assigned!");
        }
    }
    
    /// <summary>
    /// Hide boost result message after specified delay
    /// </summary>
    private System.Collections.IEnumerator HideBoostResultAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (boostResultText != null)
        {
            boostResultText.gameObject.SetActive(false);
            Debug.Log("Boost result message hidden.");
        }
    }
    
    /// <summary>
    /// Manually hide boost result message
    /// </summary>
    private void HideBoostResult()
    {
        if (boostResultText != null)
        {
            boostResultText.gameObject.SetActive(false);
        }
    }

    // -------- Error Message System --------
    
    /// <summary>
    /// Show error message when no seeds are planted
    /// </summary>
    private void ShowNoSeedsError()
    {
        // FishingManager çakışmasını önlemek için kontrol
        // Eğer text component'i başka bir script tarafından kullanılıyorsa güvenli hata göster
        if (errorText != null)
        {
            // Text'in parent GameObject'inin adını kontrol et - FishingManager ile çakışma kontrolü
            bool isSafeToUse = true;
            if (errorText.transform.parent != null)
            {
                string parentName = errorText.transform.parent.name.ToLower();
                // FishingManager tarafından kullanılan UI elementlerini kontrol et
                if (parentName.Contains("fishing") || parentName.Contains("fish"))
                {
                    isSafeToUse = false;
                    Debug.LogWarning("ErrorText appears to be used by FishingManager. Using fallback.");
                }
            }
            
            if (isSafeToUse)
            {
                errorText.gameObject.SetActive(true);
                errorText.text = "⚠️ No seeds planted! Plant and water seeds first. ⚠️";
                errorText.color = Color.red;
                
                // 3 saniye sonra error mesajını gizle
                StartCoroutine(HideErrorAfterDelay(3f));
                
                Debug.Log("No seeds error message shown!");
            }
            else
            {
                // Güvenli değilse fallback kullan
                UseFallbackErrorDisplay();
            }
        }
        else
        {
            // ErrorText yoksa fallback kullan
            UseFallbackErrorDisplay();
        }
    }
    
    /// <summary>
    /// Fallback error display when main errorText is not available or unsafe to use
    /// </summary>
    private void UseFallbackErrorDisplay()
    {
        if (infoText != null)
        {
            infoText.text = "⚠️ No seeds planted!";
            infoText.color = Color.red;
            StartCoroutine(ClearInfoAfter(3f));
            Debug.LogWarning("ErrorText not available, using InfoText as fallback.");
        }
        else
        {
            Debug.LogWarning("No ErrorText or InfoText assigned for error messages!");
        }
    }
    
    /// <summary>
    /// Hide error message after specified delay
    /// </summary>
    private System.Collections.IEnumerator HideErrorAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (errorText != null)
        {
            errorText.gameObject.SetActive(false);
            Debug.Log("Error message hidden.");
        }
    }
    
    /// <summary>
    /// Manually hide error message (e.g., when player plants seeds)
    /// </summary>
    private void HideErrorMessage()
    {
        if (errorText != null)
        {
            errorText.gameObject.SetActive(false);
        }
    }
}
