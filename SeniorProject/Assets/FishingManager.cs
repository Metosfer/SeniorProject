using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

// Attach this to the lake/water object. Press E near the lake while holding the Fishing Rod
// to start a simple Stardew-like fishing minigame: keep the bobber window over the moving fish
// to fill the catch progress. When full, a fish is awarded.
public class FishingManager : MonoBehaviour
{
    [Header("Player & Start Conditions")]
    [Tooltip("Player transform used for distance checks. If null, uses tagged 'Player'.")]
    public Transform player;
    [Tooltip("Press this key near the lake to start fishing.")]
    public KeyCode startKey = KeyCode.E;
    [Tooltip("Maximum distance from this object to allow starting the minigame.")]
    public float interactRange = 3.0f;
    [Tooltip("Require that the Fishing Rod is currently carried to start.")]
    public bool requireRodEquipped = true;

    [Header("Minigame UI (Screen-space)")]
    [Tooltip("CanvasGroup that contains the minigame UI. Will be auto-created if empty and autoCreateRuntimeUI is enabled.")]
    public CanvasGroup uiGroup;
    [Tooltip("Vertical bar area in which fish/bobber move (RectTransform).")]
    public RectTransform barArea;
    [Tooltip("Our controllable catch window.")]
    public RectTransform bobberWindow;
    [Tooltip("Fish icon that moves up/down.")]
    public RectTransform fishIcon;
    [Tooltip("Progress fill image (fillAmount 0..1).")]
    public Image progressFill;
    [Tooltip("Automatically create a basic UI if references are not assigned.")]
    public bool autoCreateRuntimeUI = true;

    [Header("Minigame Tuning")]
    [Tooltip("How many seconds of overlap are required to catch the fish (cumulative).")]
    public float requiredOverlapSeconds = 3.0f;
    [Tooltip("Rate per second to decrease progress when not overlapping (0 = no decay).")]
    public float decayPerSecond = 0.5f;
    [Tooltip("Gravity pulling the bobber window down.")]
    public float bobberGravity = 6f;
    [Tooltip("Upward thrust when holding the action key (Space/Mouse0).")]
    public float bobberThrust = 10f;
    [Tooltip("Max bobber vertical speed.")]
    public float bobberMaxSpeed = 10f;
    [Tooltip("Fish base vertical speed.")]
    public float fishSpeed = 2.5f;
    [Tooltip("How often the fish changes its target position (seconds).")]
    public Vector2 fishRetargetInterval = new Vector2(0.6f, 1.6f);
    [Tooltip("How close to target before picking a new target.")]
    public float fishTargetTolerance = 0.05f;

    [Header("Catch Rewards")]
    [Tooltip("Fish item ID to award on success (hook up your inventory via event below).")]
    public string fishItemId = "Fish_Common";
    [Tooltip("Quantity awarded on success.")]
    public int fishQuantity = 1;

    [System.Serializable]
    public class StringEvent : UnityEvent<string> { }
    [System.Serializable]
    public class IntEvent : UnityEvent<int> { }
    [Header("Events")]
    [Tooltip("Invoked when the player successfully catches a fish (passes fishItemId and quantity).")]
    public StringEvent onFishCaughtId;
    public IntEvent onFishCaughtQuantity;

    [Header("Color Feedback")]
    [Tooltip("Neutral color (center of scale). White by default.")]
    public Color neutralColor = Color.white;
    [Tooltip("Color when very close/on target (goes from neutral to this as you overlap more).")]
    public Color nearGreenColor = new Color(0.1f, 0.6f, 0.1f, 1f);
    [Tooltip("Color when far (goes from neutral to this as you get farther away).")]
    public Color farRedColor = new Color(0.85f, 0.2f, 0.2f, 1f);
    [Tooltip("How far beyond the capture radius becomes fully red (multiplier of capture radius).")]
    public float farDistanceFactor = 2.5f;

    // Runtime state
    private bool _active;
    private float _progress; // 0..1
    private float _accumOverlap; // seconds overlapped
    private float _bobberY01; // normalized 0..1 inside bar
    private float _bobberVel;
    private float _fishY01;
    private float _fishTargetY01;
    private float _nextRetargetTime;

    private Camera _uiCamera;

    private void Start()
    {
        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) player = go.transform;
        }

        if ((uiGroup == null || barArea == null || bobberWindow == null || fishIcon == null || progressFill == null) && autoCreateRuntimeUI)
        {
            CreateRuntimeUI();
        }

        ShowUI(false);
    }

    private void Update()
    {
        if (!_active)
        {
            // Start when near and conditions met
            if (Input.GetKeyDown(startKey) && IsPlayerInRange() && IsRodEquippedIfRequired())
            {
                BeginMinigame();
            }
            return;
        }

        // Active minigame loop
        UpdateFishMovement();
        UpdateBobberMovement();
        UpdateProgress();

        // ESC to cancel
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            EndMinigame(false);
        }
    }

    private bool IsPlayerInRange()
    {
        if (player == null) return false;
        return Vector3.Distance(player.position, transform.position) <= interactRange;
    }

    private bool IsRodEquippedIfRequired()
    {
        if (!requireRodEquipped) return true;
        // FishingRodManager.CurrentCarried indicates the rod is held
        return FishingRodManager.CurrentCarried != null;
    }

    private void BeginMinigame()
    {
        _active = true;
        _progress = 0f;
        _accumOverlap = 0f;
        _bobberY01 = 0.5f;
        _bobberVel = 0f;
        _fishY01 = Random.Range(0.2f, 0.8f);
        _fishTargetY01 = Random.Range(0.1f, 0.9f);
        _nextRetargetTime = Time.time + Random.Range(fishRetargetInterval.x, fishRetargetInterval.y);
        ApplyUIPositions();
        ShowUI(true);
    }

    private void EndMinigame(bool success)
    {
        _active = false;
        ShowUI(false);
        if (success)
        {
            // Notify inventory via events
            onFishCaughtId?.Invoke(fishItemId);
            onFishCaughtQuantity?.Invoke(Mathf.Max(1, fishQuantity));
        }
    }

    private void UpdateFishMovement()
    {
        if (Time.time >= _nextRetargetTime || Mathf.Abs(_fishTargetY01 - _fishY01) < fishTargetTolerance)
        {
            _fishTargetY01 = Mathf.Clamp01(_fishY01 + Random.Range(-0.6f, 0.6f));
            _nextRetargetTime = Time.time + Random.Range(fishRetargetInterval.x, fishRetargetInterval.y);
        }
        float dir = Mathf.Sign(_fishTargetY01 - _fishY01);
        _fishY01 += dir * fishSpeed * Time.deltaTime;
        _fishY01 = Mathf.Clamp01(_fishY01);
        ApplyUIPositions();
    }

    private void UpdateBobberMovement()
    {
        // Input model: hold Space or Left Mouse to go up, otherwise gravity pulls down
        bool thrust = Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0);
        float acc = (thrust ? bobberThrust : 0f) - bobberGravity;
        _bobberVel += acc * Time.deltaTime;
        _bobberVel = Mathf.Clamp(_bobberVel, -bobberMaxSpeed, bobberMaxSpeed);
        _bobberY01 += _bobberVel * Time.deltaTime;
        if (_bobberY01 < 0f)
        {
            _bobberY01 = 0f; _bobberVel = 0f;
        }
        else if (_bobberY01 > 1f)
        {
            _bobberY01 = 1f; _bobberVel = 0f;
        }
        ApplyUIPositions();
    }

    private void UpdateProgress()
    {
        // Consider overlap if centers are within bobber window height range
        if (barArea == null || bobberWindow == null || fishIcon == null || progressFill == null) return;

        float h = barArea.rect.height;
        float bobberH = bobberWindow.rect.height;
        float fishH = fishIcon.rect.height;
        float bobberCenter = _bobberY01 * h;
        float fishCenter = _fishY01 * h;
        float halfBobber = bobberH * 0.5f;
        float halfFish = fishH * 0.5f;
        float distance = Mathf.Abs(bobberCenter - fishCenter);
        float captureRadius = (halfBobber + halfFish) * 0.5f;

        bool overlapping = distance <= captureRadius;
        if (overlapping)
        {
            _accumOverlap += Time.deltaTime;
            _progress = Mathf.Clamp01(_accumOverlap / Mathf.Max(0.1f, requiredOverlapSeconds));
        }
        else if (decayPerSecond > 0f)
        {
            _accumOverlap = Mathf.Max(0f, _accumOverlap - decayPerSecond * Time.deltaTime);
            _progress = Mathf.Clamp01(_accumOverlap / Mathf.Max(0.1f, requiredOverlapSeconds));
        }

        progressFill.fillAmount = _progress;
        UpdateProximityColor(distance, captureRadius);
        if (_progress >= 1f)
        {
            EndMinigame(true);
        }
    }

    private void UpdateProximityColor(float distance, float captureRadius)
    {
        if (progressFill == null) return;
        // Inside capture radius: white -> green based on closeness
        if (distance <= captureRadius)
        {
            float t = Mathf.InverseLerp(captureRadius, 0f, distance); // d=captureRadius -> 0, d=0 -> 1
            progressFill.color = Color.Lerp(neutralColor, nearGreenColor, t);
        }
        else
        {
            // Outside capture: white -> red as distance grows (up to captureRadius * farDistanceFactor)
            float maxFar = Mathf.Max(0.0001f, captureRadius * Mathf.Max(1f, farDistanceFactor));
            float t = Mathf.InverseLerp(captureRadius, maxFar, distance); // at edge -> 0, very far -> 1
            progressFill.color = Color.Lerp(neutralColor, farRedColor, t);
        }
    }

    private void ApplyUIPositions()
    {
        if (barArea == null) return;
        if (bobberWindow != null)
        {
            var p = bobberWindow.anchoredPosition;
            p.y = Mathf.Lerp(0f, barArea.rect.height, _bobberY01);
            bobberWindow.anchoredPosition = p;
        }
        if (fishIcon != null)
        {
            var p2 = fishIcon.anchoredPosition;
            p2.y = Mathf.Lerp(0f, barArea.rect.height, _fishY01);
            fishIcon.anchoredPosition = p2;
        }
    }

    private void ShowUI(bool show)
    {
        if (uiGroup == null) return;
        uiGroup.alpha = show ? 1f : 0f;
        uiGroup.interactable = show;
        uiGroup.blocksRaycasts = show;
    }

    // Create a minimal runtime UI so the system works out-of-the-box
    private void CreateRuntimeUI()
    {
        // Canvas
        var canvasGO = new GameObject("FishingUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        uiGroup = canvasGO.GetComponent<CanvasGroup>();

        // Panel background (optional semi-transparent)
        var panelGO = new GameObject("Panel", typeof(Image));
        panelGO.transform.SetParent(canvasGO.transform, false);
        var panelImg = panelGO.GetComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.2f);
        var panelRect = panelGO.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(300, 500);
        panelRect.anchoredPosition = Vector2.zero;

        // Bar area
        var barGO = new GameObject("BarArea", typeof(Image));
        barGO.transform.SetParent(panelGO.transform, false);
        var barImg = barGO.GetComponent<Image>();
        barImg.color = new Color(1f, 1f, 1f, 0.05f);
        barArea = barGO.GetComponent<RectTransform>();
        barArea.anchorMin = new Vector2(0.5f, 0.5f);
        barArea.anchorMax = new Vector2(0.5f, 0.5f);
        barArea.sizeDelta = new Vector2(80, 380);
        barArea.anchoredPosition = Vector2.zero;

        // Bobber window
        var bobGO = new GameObject("BobberWindow", typeof(Image));
        bobGO.transform.SetParent(barGO.transform, false);
        var bobImg = bobGO.GetComponent<Image>();
        bobImg.color = new Color(0.2f, 0.9f, 0.2f, 0.4f);
        bobberWindow = bobGO.GetComponent<RectTransform>();
        bobberWindow.pivot = new Vector2(0.5f, 0f);
        bobberWindow.sizeDelta = new Vector2(70, 110);
        bobberWindow.anchoredPosition = new Vector2(0, barArea.rect.height * 0.5f);

        // Fish icon
        var fishGO = new GameObject("FishIcon", typeof(Image));
        fishGO.transform.SetParent(barGO.transform, false);
        var fishImg = fishGO.GetComponent<Image>();
        fishImg.color = new Color(0.2f, 0.6f, 1f, 0.9f);
        fishIcon = fishGO.GetComponent<RectTransform>();
        fishIcon.pivot = new Vector2(0.5f, 0.5f);
        fishIcon.sizeDelta = new Vector2(40, 40);
        fishIcon.anchoredPosition = new Vector2(0, barArea.rect.height * 0.5f);

        // Progress fill (top)
        var progBG = new GameObject("ProgressBG", typeof(Image));
        progBG.transform.SetParent(panelGO.transform, false);
        var progBgImg = progBG.GetComponent<Image>();
        progBgImg.color = new Color(1f, 1f, 1f, 0.1f);
        var progBGRect = progBG.GetComponent<RectTransform>();
        progBGRect.anchorMin = new Vector2(0.5f, 1f);
        progBGRect.anchorMax = new Vector2(0.5f, 1f);
        progBGRect.sizeDelta = new Vector2(200, 20);
        progBGRect.anchoredPosition = new Vector2(0, -20);

        var progFillGO = new GameObject("ProgressFill", typeof(Image));
        progFillGO.transform.SetParent(progBG.transform, false);
        progressFill = progFillGO.GetComponent<Image>();
        progressFill.color = new Color(0.2f, 0.9f, 0.2f, 0.9f);
        var progFillRect = progressFill.GetComponent<RectTransform>();
        progFillRect.anchorMin = new Vector2(0f, 0f);
        progFillRect.anchorMax = new Vector2(0f, 1f);
        progFillRect.pivot = new Vector2(0f, 0.5f);
        progFillRect.sizeDelta = new Vector2(200, 20);

        // Use fillAmount by parenting under mask? Simpler: scale X by progress
        // We'll update width manually in UpdateProgress if no sprite is set for radial fills
        // To keep simple, we rely on Image.fillAmount by setting type to Filled
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        progressFill.fillAmount = 0f;

        // Initialize states
        _uiCamera = null;
    }
}
