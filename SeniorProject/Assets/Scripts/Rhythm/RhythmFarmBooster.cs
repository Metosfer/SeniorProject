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
                // Ensure prompt shows the correct warning
                if (interactPromptText != null) interactPromptText.text = "Add water first";
                if (infoText != null) { infoText.text = ""; }
                return;
            }
            // Begin mini-game
            _playing = true;
            if (rhythm.rootUI != null) rhythm.rootUI.SetActive(true);
            if (boostResultText != null) boostResultText.text = string.Empty;
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
        if (total <= 0.01f) return;

        // Find target farming area if missing
    var target = farmingArea;
    if (target == null) return;

        float applied = target.ApplyGrowthBoost(total);
        Debug.Log($"RhythmFarmBooster applied {applied:F1}s growth boost (requested {total:F1}s) to {target.name}");
        if (boostResultText != null && applied > 0f)
        {
            boostResultText.text = $"Boost applied: {applied:F1}s";
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
}
