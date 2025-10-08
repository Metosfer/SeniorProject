using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Simple rhythm game manager: analyzes an AudioSource for beats and spawns notes towards 4 arrow targets.
public class RythmGameManager : MonoBehaviour
{
    // Global input lock for player movement
    public static bool RhythmInputLock { get; private set; }
    [Header("Audio")]
    public AudioSource musicSource; // Assign your music clip here
    [Tooltip("Sensitivity for beat detection. Lower = more beats.")]
    [Range(0.01f, 2f)] public float beatSensitivity = 0.25f;
    [Tooltip("Minimum seconds between detected beats to avoid double-detection.")]
    [Range(0.05f, 0.5f)] public float beatCooldown = 0.12f;
    [Header("Playlist")]
    [Tooltip("Optional list of music clips. If provided, a random one is picked on StartGame().")]
    public List<AudioClip> musicClips = new List<AudioClip>();
    [Tooltip("Pick a random track on each StartGame() call.")]
    public bool pickRandomTrackEachStart = true;
    [Tooltip("Avoid picking the same track twice in a row when randomizing.")]
    public bool avoidImmediateRepeat = true;
    [Tooltip("Reset detection and auto-tune timing to the selected track.")]
    public bool autoTuneToTrack = true;

    [Header("Lanes & UI Targets")] 
    [Tooltip("Parent RectTransform that contains the 4 arrow images (Up,Down,Left,Right)")]
    public RectTransform arrowsPanel; // Your panel under the Canvas
    public RectTransform upTarget;
    public RectTransform downTarget;
    public RectTransform leftTarget;
    public RectTransform rightTarget;

    [Header("Note Prefabs")] 
    [Tooltip("UI Image prefab for a moving note (WorldSpace or ScreenSpace - Overlay Canvas).")]
    public GameObject noteUIPrefab; // A simple Image under Canvas
    [Tooltip("Color per direction (optional)")]
    public Color upColor = new Color(0.4f,0.8f,1f,1f);
    public Color downColor = new Color(1f,0.6f,0.2f,1f);
    public Color leftColor = new Color(0.6f,1f,0.4f,1f);
    public Color rightColor = new Color(1f,0.4f,0.8f,1f);

    [Header("Gameplay")] 
    [Tooltip("Where notes spawn (within the panel). Typically a line above the targets.")]
    public RectTransform spawnLine; // A RectTransform above the arrows
    [Tooltip("Time for notes to reach their target (seconds)")]
    public float travelTime = 0.8f;
    [Tooltip("Hit window in pixels near the target")]
    public float hitWindowPixels = 50f;
    [Tooltip("Reduce combo gain when playing fast: 1=normal, >1 means stricter combo gain at higher speed")]
    [Range(0.5f, 3f)] public float comboStrictnessAtHighSpeed = 1.4f;

    [Header("Input")] 
    public KeyCode keyUp = KeyCode.UpArrow;
    public KeyCode keyDown = KeyCode.DownArrow;
    public KeyCode keyLeft = KeyCode.LeftArrow;
    public KeyCode keyRight = KeyCode.RightArrow;

    [Header("Integration")]
    [Tooltip("Lock player movement while the rhythm game is active")] public bool lockPlayerWhilePlaying = true;
    [Tooltip("Automatically deactivate the rhythm UI on finish")] public bool autoDeactivateOnFinish = true;
    [Tooltip("UI root (panel/canvas) used to show/hide the mini-game")] public GameObject rootUI;
    [Tooltip("UI objects to hide while the rhythm game is open (e.g., Inventory panel)")]
    public List<GameObject> hideWhilePlaying = new List<GameObject>();
    [Tooltip("Additionally force-hide any UI by name while playing (e.g., 'InventoryPanel'). State is restored on close.")]
    public List<string> forceHideNames = new List<string> { "InventoryPanel" };
    [Tooltip("Allow closing with ESC while the rhythm game is open")] public bool allowEscClose = true;
    [Tooltip("Allow closing with E while the rhythm game is open")] public bool allowEKeyClose = true;

    [Header("UI Feedback")] 
    public TMP_Text scoreText;
    public TMP_Text comboText;
    public TMP_Text judgmentText; // shows Perfect/Great/Good/Miss briefly
    [Tooltip("How long to show judgment text (seconds)")]
    public float judgmentShowTime = 0.5f;
    [Tooltip("Flash color on hit")] public Color hitFlashColor = new Color(1f,1f,1f,0.7f);
    [Tooltip("Flash color on miss")] public Color missFlashColor = new Color(1f,0.2f,0.2f,0.7f);
    [Tooltip("Flash fade time")] public float flashFadeTime = 0.15f;
    [Tooltip("Score penalty on miss (enter a negative value)")]
    public int missPenalty = 50;
    [Header("Judgment Style")]
    public Color perfectColor = new Color(1f, 0.95f, 0.4f, 1f); // gold-ish
    public Color greatColor = new Color(0.4f, 1f, 0.7f, 1f);
    public Color goodColor = new Color(0.6f, 0.8f, 1f, 1f);
    public Color missColor = new Color(1f, 0.4f, 0.4f, 1f);
    [Tooltip("Initial pop scale")] public float judgmentPopScale = 1.25f;
    [Tooltip("Wiggle speed")] public float judgmentWiggleSpeed = 14f;
    [Tooltip("Shake angle amplitude (degrees)")] public float judgmentShakeAngle = 8f;
    [Tooltip("Add hype words based on combo")] public bool enableHypeWords = true;

    [Header("SFX (Optional)")]
    public AudioSource sfxSource;
    public AudioClip sfxHit;
    public AudioClip sfxMiss;

    private float _lastBeatTime = -999f;
    private float[] _spectrum = new float[1024];
    // Moving average/variance for robust, continuous beat detection
    private const int EnergyWindow = 48; // ~1s @ ~50fps Update
    private readonly Queue<float> _energyBuf = new Queue<float>(EnergyWindow);
    private float _energySum = 0f;
    private float _energySqSum = 0f;
    private System.Random _rng = new System.Random();
    // Beat interval tracking for auto-tuning
    private readonly List<float> _beatIntervals = new List<float>(16);
    private float _avgBeatInterval = 0f;
    private int _lastClipIndex = -1;
    private float _initialTravelTime;

    private readonly List<RhythmNote> _notes = new List<RhythmNote>();
    private int _score;
    private int _combo;
    private int _maxCombo;
    private Coroutine _judgmentRoutine;
    private RectTransform _judgmentRect;
    private Vector3 _judgmentBaseScale = Vector3.one;
    private bool _wasPlaying;
    private bool _finishedInvoked;
    private readonly Dictionary<GameObject, bool> _hiddenPrevActive = new Dictionary<GameObject, bool>();
    private readonly Dictionary<GameObject, bool> _forceHidePrevActive = new Dictionary<GameObject, bool>();
    private readonly List<GameObject> _forceHideTargets = new List<GameObject>();
    // Flash management per Image to avoid color getting stuck
    private readonly Dictionary<Image, Coroutine> _flashRoutines = new Dictionary<Image, Coroutine>();
    private readonly Dictionary<Image, Color> _imageBaseColors = new Dictionary<Image, Color>();

    // External integration
    public System.Action<int,int> OnFinished; // (score, maxCombo)
    public int Score => _score;
    public int MaxCombo => _maxCombo;

    private void Start()
    {
        // Do not auto-play; use StartGame() to run on demand
        if (rootUI != null) rootUI.SetActive(false);
    UpdateScoreUI();
        if (judgmentText != null)
        {
            judgmentText.text = "";
            _judgmentRect = judgmentText.rectTransform;
            if (_judgmentRect != null) _judgmentBaseScale = _judgmentRect.localScale;
        }
    // Initialize input lock
    RhythmInputLock = false;
    // Remember inspector-assigned travel time and keep it fixed
    _initialTravelTime = travelTime;
    }

    private void Update()
    {
        if (musicSource == null) return;
        // Maintain input lock state while music plays
        RhythmInputLock = lockPlayerWhilePlaying && musicSource.isPlaying;
        DetectBeatAndSpawn();
        UpdateNotes();
        HandleInputs();

        // Allow closing with ESC or E while UI is open
        if (rootUI != null && rootUI.activeSelf)
        {
            if ((allowEscClose && InputHelper.GetKeyDown(KeyCode.Escape)) ||
                (allowEKeyClose && InputHelper.GetKeyDown(KeyCode.E)))
            {
                StopGame();
                return;
            }
        }

        // Detect finish (song ended)
        bool playing = musicSource.isPlaying;
        if (_wasPlaying && !playing && !_finishedInvoked)
        {
            // Allow tiny delay to ensure song actually ended
            if (musicSource.clip == null || musicSource.time >= (musicSource.clip.length - 0.05f) || musicSource.time <= 0.02f)
            {
                FinishGame();
            }
        }
        _wasPlaying = playing;
    }

    private void LateUpdate()
    {
        // As a stronger guard, also enforce in LateUpdate in case other systems toggled after Update
        if (rootUI != null && rootUI.activeSelf)
        {
            EnforceExternalUIHidden();
        }
    }

    private void OnDisable()
    {
        RhythmInputLock = false;
    }

    public void StartGame()
    {
        // Reset state
        _score = 0; _combo = 0; _maxCombo = 0;
        UpdateScoreUI();
        if (judgmentText != null) judgmentText.text = string.Empty;
        ClearAllNotes();
        _finishedInvoked = false;
        
        // Hide external UI first (respect previous active states), then show rhythm UI
        HideExternalUI(true);
        if (rootUI != null) 
        {
            rootUI.SetActive(true);
            
            // Panel'i en öne çıkar - UI hierarchy'de en üstte olması için
            BringRhythmPanelToFront();
        }
        
        // Start audio
        if (musicSource != null)
        {
            // Choose a track if a playlist is provided
            if (musicClips != null && musicClips.Count > 0)
            {
                int idx = SelectTrackIndex();
                if (idx >= 0 && idx < musicClips.Count)
                {
                    musicSource.clip = musicClips[idx];
                    _lastClipIndex = idx;
                }
            }
            if (musicSource.clip != null)
            {
                ResetBeatDetectionState();
                musicSource.time = 0f;
                musicSource.Play();
            }
        }
        RhythmInputLock = lockPlayerWhilePlaying;
    }

    public void StopGame()
    {
        if (musicSource != null && musicSource.isPlaying) musicSource.Stop();
        RhythmInputLock = false;
        // Treat manual close as a finish and notify listeners with current score/combo
        if (!_finishedInvoked)
        {
            _finishedInvoked = true;
            OnFinished?.Invoke(_score, _maxCombo);
        }
        if (rootUI != null) rootUI.SetActive(false);
        ClearAllNotes();
    HideExternalUI(false);
    }

    private void FinishGame()
    {
        _finishedInvoked = true;
        RhythmInputLock = false;
        OnFinished?.Invoke(_score, _maxCombo);
        if (autoDeactivateOnFinish && rootUI != null) rootUI.SetActive(false);
        ClearAllNotes();
    HideExternalUI(false);
    }

    private void ClearAllNotes()
    {
        for (int i = 0; i < _notes.Count; i++)
        {
            var n = _notes[i]; if (n.go != null) Destroy(n.go);
        }
        _notes.Clear();
    }

    private void DetectBeatAndSpawn()
    {
        if (!musicSource.isPlaying) return;

        // Energy-based detection with moving average + std-dev threshold
        AudioListener.GetSpectrumData(_spectrum, 0, FFTWindow.BlackmanHarris);
        float energy = 0f;
        for (int i = 0; i < _spectrum.Length; i++) energy += _spectrum[i];

        // Update windowed stats
        if (_energyBuf.Count == EnergyWindow)
        {
            float oldest = _energyBuf.Dequeue();
            _energySum -= oldest;
            _energySqSum -= oldest * oldest;
        }
        _energyBuf.Enqueue(energy);
        _energySum += energy;
        _energySqSum += energy * energy;

        float n = Mathf.Max(1, _energyBuf.Count);
        float mean = _energySum / n;
        float variance = Mathf.Max(0f, _energySqSum / n - mean * mean);
        float std = Mathf.Sqrt(variance);

        float now = musicSource.time;
        float thresh = mean + std * Mathf.Lerp(1.2f, 3.0f, Mathf.Clamp01(beatSensitivity));
        if (energy > thresh && (now - _lastBeatTime) > beatCooldown)
        {
            // Track beat interval for auto-tune
            if (_lastBeatTime > 0f)
            {
                float interval = now - _lastBeatTime;
                if (interval > 0.05f && interval < 3.0f)
                {
                    if (_beatIntervals.Count >= 12) _beatIntervals.RemoveAt(0);
                    _beatIntervals.Add(interval);
                    // compute average
                    float sum = 0f; for (int i = 0; i < _beatIntervals.Count; i++) sum += _beatIntervals[i];
                    _avgBeatInterval = sum / _beatIntervals.Count;
                    if (autoTuneToTrack && _beatIntervals.Count >= 3)
                    {
                        // Adjust cooldown and travel time to track tempo
                        float targetCooldown = Mathf.Clamp(_avgBeatInterval * 0.45f, 0.06f, 0.5f);
                        beatCooldown = targetCooldown;
                        // Do not change travelTime; keep inspector value
                    }
                }
            }
            _lastBeatTime = now;
            SpawnRandomDirectionalNote(now + travelTime);
        }
    }

    private int SelectTrackIndex()
    {
        if (musicClips == null || musicClips.Count == 0) return -1;
        if (!pickRandomTrackEachStart)
        {
            // Default to first clip when not randomizing
            return 0;
        }
        if (musicClips.Count == 1) return 0;
        int attempt = 0;
        int idx;
        do
        {
            idx = _rng.Next(0, musicClips.Count);
            attempt++;
        } while (avoidImmediateRepeat && idx == _lastClipIndex && attempt < 8);
        return idx;
    }

    private void ResetBeatDetectionState()
    {
        _lastBeatTime = -999f;
        _energyBuf.Clear();
        _energySum = 0f;
        _energySqSum = 0f;
        _beatIntervals.Clear();
        _avgBeatInterval = 0f;
    }

    private void SpawnRandomDirectionalNote(float expectedHitTime)
    {
        if (noteUIPrefab == null || arrowsPanel == null || spawnLine == null) return;
        // Pick random lane 0..3
        int lane = _rng.Next(0, 4);
        RectTransform target = GetLaneTarget(lane);
        if (target == null) return;

        // Instantiate UI note
        var go = Instantiate(noteUIPrefab, arrowsPanel);
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        // Start at spawnLine X aligned with target, Y at spawnLine
        Vector2 start = new Vector2(target.anchoredPosition.x, spawnLine.anchoredPosition.y);
        rt.anchoredPosition = start;
        // Colorize
        var img = go.GetComponent<Image>();
        if (img != null)
        {
            img.color = LaneColor(lane);
        }

        // Optional animated visuals
        var anim = go.GetComponent<NoteUIAnimator>();
        if (anim == null) anim = go.AddComponent<NoteUIAnimator>();

        var note = new RhythmNote
        {
            go = go,
            rect = rt,
            lane = lane,
            target = target,
            startPos = start,
            endPos = target.anchoredPosition,
            spawnTime = Time.unscaledTime,
            expectedHitAudioTime = expectedHitTime,
            travelTime = Mathf.Max(0.05f, travelTime),
            visual = anim
        };
        _notes.Add(note);
    }

    private void UpdateNotes()
    {
        float tNow = Time.unscaledTime;
            // Ensure hidden UIs (e.g., inventory) stay hidden during play
            EnforceExternalUIHidden();
        for (int i = _notes.Count - 1; i >= 0; i--)
        {
            var n = _notes[i];
            float u = Mathf.InverseLerp(n.spawnTime, n.spawnTime + n.travelTime, tNow);
            Vector2 pos = Vector2.LerpUnclamped(n.startPos, n.endPos, u);
            if (n.visual != null)
            {
                Vector2 ofs = n.visual.GetPositionOffset(tNow - n.spawnTime, _combo);
                n.rect.anchoredPosition = pos + ofs;
                n.visual.ApplyVisuals(tNow - n.spawnTime, _combo);
            }
            else
            {
                n.rect.anchoredPosition = pos;
            }
            // Reached target without being hit -> Miss and cleanup
            if (u >= 1.0f)
            {
                ApplyJudgment(Judgment.Miss, n.lane);
                Destroy(n.go);
                _notes.RemoveAt(i);
            }
            else
            {
                _notes[i] = n;
            }
        }
    }

    private void HandleInputs()
    {
    if (InputHelper.GetKeyDown(keyUp)) TryHitLane(0);
    if (InputHelper.GetKeyDown(keyDown)) TryHitLane(1);
    if (InputHelper.GetKeyDown(keyLeft)) TryHitLane(2);
    if (InputHelper.GetKeyDown(keyRight)) TryHitLane(3);
    }

    public void TryHitLane(int lane)
    {
        RectTransform target = GetLaneTarget(lane);
        if (target == null) return;
        // Find closest note in this lane inside window
        int bestIdx = -1; float bestDist = float.MaxValue;
        for (int i = 0; i < _notes.Count; i++)
        {
            if (_notes[i].lane != lane) continue;
            float d = Mathf.Abs(_notes[i].rect.anchoredPosition.y - target.anchoredPosition.y);
            if (d < bestDist)
            {
                bestDist = d; bestIdx = i;
            }
        }
        // Make combo stricter at higher perceived speed by narrowing effective hit window for combo gain
        float effectiveWindow = hitWindowPixels;
        if (comboStrictnessAtHighSpeed > 1f && musicSource != null && travelTime > 0.05f)
        {
            // Estimate flow speed from spawn-to-target travel; faster travel -> tighter combo window
            float speedFactor = Mathf.Clamp((1f / travelTime), 0.5f, 3f); // 1/travelTime ~ speed
            float tighten = Mathf.Lerp(1f, comboStrictnessAtHighSpeed, Mathf.InverseLerp(1f, 2.5f, speedFactor));
            effectiveWindow = hitWindowPixels / tighten;
        }

        if (bestIdx >= 0 && bestDist <= effectiveWindow)
        {
            // Hit!
            var noteGO = _notes[bestIdx].go;
            Destroy(noteGO);
            _notes.RemoveAt(bestIdx);
            var j = Judge(bestDist);
            ApplyJudgment(j, lane);
        }
        else
        {
            ApplyJudgment(Judgment.Miss, lane);
        }
    }

    private enum Judgment { Perfect, Great, Good, Miss }

    private Judgment Judge(float pixelDistance)
    {
        float w = Mathf.Max(1f, hitWindowPixels);
        if (pixelDistance <= w * 0.3f) return Judgment.Perfect;
        if (pixelDistance <= w * 0.6f) return Judgment.Great;
        return Judgment.Good; // else Good; outside window handled earlier as Miss
    }

    private void ApplyJudgment(Judgment j, int lane)
    {
        int add = 0;
        switch (j)
        {
            case Judgment.Perfect: add = 300; _combo++; break;
            case Judgment.Great: add = 200; _combo++; break;
            case Judgment.Good: add = 100; _combo++; break;
            case Judgment.Miss: add = -Mathf.Abs(missPenalty); _combo = 0; break;
        }
        _score += add;
        if (_combo > _maxCombo) _maxCombo = _combo;
        UpdateScoreUI();
        ShowJudgment(j);
        FlashTarget(lane, j != Judgment.Miss);
        PlaySfx(j != Judgment.Miss);
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null) scoreText.text = $"Score: {_score}";
        if (comboText != null) comboText.text = _combo > 1 ? $"Combo: {_combo}" : "";
    }

    private void ShowJudgment(Judgment j)
    {
        if (judgmentText == null) return;
        string primary = j.ToString();
        string hype = enableHypeWords ? GetHypeWord(_combo, j) : string.Empty;
        string txt = string.IsNullOrEmpty(hype) ? primary : primary + "  •  " + hype;
        judgmentText.text = txt;
        judgmentText.color = GetJudgmentColor(j, _combo);
        if (_judgmentRoutine != null) StopCoroutine(_judgmentRoutine);
        _judgmentRoutine = StartCoroutine(RunJudgmentEffect(judgmentShowTime));
    }

    private IEnumerator RunJudgmentEffect(float duration)
    {
        if (_judgmentRect == null) _judgmentRect = judgmentText != null ? judgmentText.rectTransform : null;
        float t = 0f;
        // Instant pop
        if (_judgmentRect != null) _judgmentRect.localScale = _judgmentBaseScale * Mathf.Max(1f, judgmentPopScale);
        // Animate back with wiggle/rotation
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / Mathf.Max(0.001f, duration));
            if (_judgmentRect != null)
            {
                // Scale ease back
                float ease = 1f - Mathf.Exp(-6f * u);
                float wobble = 1f + 0.05f * Mathf.Sin(judgmentWiggleSpeed * t);
                _judgmentRect.localScale = Vector3.Lerp(_judgmentBaseScale * Mathf.Max(1f, judgmentPopScale), _judgmentBaseScale, ease) * wobble;
                // Small Z-rotation shake
                float ang = Mathf.Sin(judgmentWiggleSpeed * 0.7f * t) * judgmentShakeAngle * (1f - u);
                _judgmentRect.localRotation = Quaternion.Euler(0f, 0f, ang);
            }
            yield return null;
        }
        if (_judgmentRect != null)
        {
            _judgmentRect.localScale = _judgmentBaseScale;
            _judgmentRect.localRotation = Quaternion.identity;
        }
        if (judgmentText != null) judgmentText.text = "";
    }

    private string GetHypeWord(int combo, Judgment j)
    {
        if (combo < 5) return string.Empty;
        if (combo >= 100)
        {
            string[] pool = { "LEGENDARY!", "GODLIKE!", "UNSTOPPABLE!" };
            return pool[UnityEngine.Random.Range(0, pool.Length)];
        }
        if (combo >= 75)
        {
            string[] pool = { "UNBELIEVABLE!", "INCREDIBLE!", "SPECTACULAR!" };
            return pool[UnityEngine.Random.Range(0, pool.Length)];
        }
        if (combo >= 50)
        {
            string[] pool = { "INSANE!", "WILD!", "NEXT LEVEL!" };
            return pool[UnityEngine.Random.Range(0, pool.Length)];
        }
        if (combo >= 40)
        {
            string[] pool = { "AMAZING!", "STUNNING!", "PHENOMENAL!" };
            return pool[UnityEngine.Random.Range(0, pool.Length)];
        }
        if (combo >= 30)
        {
            string[] pool = { "FANTASTIC!", "OUTSTANDING!", "EPIC!" };
            return pool[UnityEngine.Random.Range(0, pool.Length)];
        }
        if (combo >= 20)
        {
            string[] pool = { "AWESOME!", "EXCELLENT!", "FLAWLESS!" };
            return pool[UnityEngine.Random.Range(0, pool.Length)];
        }
        if (combo >= 15)
        {
            string[] pool = { "THE HYPE IS REAL!", "KEEP IT UP!", "BUILDING MOMENTUM!" };
            return pool[UnityEngine.Random.Range(0, pool.Length)];
        }
        if (combo >= 10)
        {
            string[] pool = { "NICE!", "ON FIRE!", "WARMING UP!" };
            return pool[UnityEngine.Random.Range(0, pool.Length)];
        }
        string[] basePool = { "NICE!", "KEEP GOING!", "LETS GO!" };
        return basePool[UnityEngine.Random.Range(0, basePool.Length)];
    }

    private Color GetJudgmentColor(Judgment j, int combo)
    {
        Color baseC = goodColor;
        switch (j)
        {
            case Judgment.Perfect: baseC = perfectColor; break;
            case Judgment.Great: baseC = greatColor; break;
            case Judgment.Good: baseC = goodColor; break;
            case Judgment.Miss: baseC = missColor; break;
        }
        // Tilt towards gold at higher combos
        if (combo >= 20)
        {
            var gold = new Color(1f, 0.9f, 0.5f, 1f);
            float t = Mathf.Clamp01((combo - 20) / 60f); // 20..80
            baseC = Color.Lerp(baseC, gold, t);
        }
        return baseC;
    }

    private void FlashTarget(int lane, bool hit)
    {
        var tr = GetLaneTarget(lane); if (tr == null) return;
        var img = tr.GetComponent<Image>();
        if (img == null) return;
        Color c = hit ? hitFlashColor : missFlashColor;
        StartManagedFlash(img, c, Mathf.Max(0.01f, flashFadeTime));
    }

    private void StartManagedFlash(Image img, Color flash, float fade)
    {
        if (img == null) return;
        if (!_imageBaseColors.ContainsKey(img))
        {
            _imageBaseColors[img] = img.color; // remember default once
        }
        // Cancel any running flash on this image
        if (_flashRoutines.TryGetValue(img, out var co) && co != null)
        {
            StopCoroutine(co);
            _flashRoutines[img] = null;
        }
        var routine = StartCoroutine(FlashImageManaged(img, flash, fade));
        _flashRoutines[img] = routine;
    }

    private IEnumerator FlashImageManaged(Image img, Color flash, float fade)
    {
        if (img == null) yield break;
        if (!_imageBaseColors.TryGetValue(img, out var baseCol)) baseCol = img.color;
        img.color = flash;
        float t = 0f; float dur = Mathf.Max(0.01f, fade);
        while (t < dur && img != null)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dur);
            img.color = Color.Lerp(flash, baseCol, u);
            yield return null;
        }
        if (img != null) img.color = baseCol;
        _flashRoutines[img] = null;
    }

    private void HideExternalUI(bool hide)
    {
        if (hide)
        {
            _hiddenPrevActive.Clear();
            _forceHidePrevActive.Clear();
            BuildForceHideTargets();
            for (int i = 0; i < hideWhilePlaying.Count; i++)
            {
                var go = hideWhilePlaying[i]; if (go == null) continue;
                _hiddenPrevActive[go] = go.activeSelf;
                if (go.activeSelf) go.SetActive(false);
            }
            for (int i = 0; i < _forceHideTargets.Count; i++)
            {
                var go = _forceHideTargets[i]; if (go == null) continue;
                _forceHidePrevActive[go] = go.activeSelf;
                if (go.activeSelf) go.SetActive(false);
            }
        }
        else
        {
            foreach (var kv in _hiddenPrevActive)
            {
                var go = kv.Key; if (go == null) continue;
                bool prev = kv.Value;
                if (go.activeSelf != prev) go.SetActive(prev);
            }
            _hiddenPrevActive.Clear();

            foreach (var kv in _forceHidePrevActive)
            {
                var go = kv.Key; if (go == null) continue;
                bool prev = kv.Value;
                if (go.activeSelf != prev) go.SetActive(prev);
            }
            _forceHidePrevActive.Clear();
            _forceHideTargets.Clear();
        }
    }

    // Guard against other systems toggling UI back on while playing
    private void EnforceExternalUIHidden()
    {
        for (int i = 0; i < hideWhilePlaying.Count; i++)
        {
            var go = hideWhilePlaying[i]; if (go == null) continue;
            if (go.activeSelf)
            {
                go.SetActive(false);
            }
        }
        for (int i = 0; i < _forceHideTargets.Count; i++)
        {
            var go = _forceHideTargets[i]; if (go == null) continue;
            if (go.activeSelf)
            {
                go.SetActive(false);
            }
        }
    }

    private void BuildForceHideTargets()
    {
        _forceHideTargets.Clear();
        if (forceHideNames == null || forceHideNames.Count == 0) return;
        // Include inactive scene objects too; filter by scene validity to avoid assets
        var all = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < all.Length; i++)
        {
            var go = all[i]; if (go == null) continue;
            if (!go.scene.IsValid()) continue;
            string nm = go.name;
            for (int j = 0; j < forceHideNames.Count; j++)
            {
                var key = forceHideNames[j]; if (string.IsNullOrEmpty(key)) continue;
                if (nm.Equals(key) || nm.Contains(key))
                {
                    if (!_forceHideTargets.Contains(go)) _forceHideTargets.Add(go);
                    break;
                }
            }
        }
    }

    private void PlaySfx(bool hit)
    {
        if (sfxSource == null) return;
        var clip = hit ? sfxHit : sfxMiss;
        if (clip != null) sfxSource.PlayOneShot(clip);
    }

    private RectTransform GetLaneTarget(int lane)
    {
        switch (lane)
        {
            case 0: return upTarget;
            case 1: return downTarget;
            case 2: return leftTarget;
            case 3: return rightTarget;
        }
        return null;
    }

    private Color LaneColor(int lane)
    {
        switch (lane)
        {
            case 0: return upColor;
            case 1: return downColor;
            case 2: return leftColor;
            case 3: return rightColor;
        }
        return Color.white;
    }
    
    /// <summary>
    /// RhythmGamePanel'i UI hierarchy'de en öne çıkarır
    /// Diğer UI elementlerinin üstünde görünmesi için
    /// </summary>
    private void BringRhythmPanelToFront()
    {
        if (rootUI == null) return;
        
        // SetAsLastSibling ile UI hierarchy'de en sona (en üste) taşı
        rootUI.transform.SetAsLastSibling();
        
        // Eğer Canvas component'i varsa sort order'ı yükselt
        Canvas rhythmCanvas = rootUI.GetComponent<Canvas>();
        if (rhythmCanvas != null)
        {
            // Mevcut sort order'ı al ve yükselt
            rhythmCanvas.sortingOrder = 100; // Yüksek değer = en önde
            rhythmCanvas.overrideSorting = true;
            Debug.Log($"RhythmGamePanel Canvas sort order set to: {rhythmCanvas.sortingOrder}");
        }
        
        // Alternatif: Parent Canvas'ı kontrol et
        Canvas parentCanvas = rootUI.GetComponentInParent<Canvas>();
        if (parentCanvas != null && rhythmCanvas == null)
        {
            Debug.Log($"RhythmGamePanel is under parent Canvas: {parentCanvas.name} (Sort Order: {parentCanvas.sortingOrder})");
        }
        
        Debug.Log("RhythmGamePanel brought to front!");
    }

    private struct RhythmNote
    {
        public GameObject go;
        public RectTransform rect;
        public int lane; // 0=Up,1=Down,2=Left,3=Right
        public RectTransform target;
        public Vector2 startPos;
        public Vector2 endPos;
        public float spawnTime; // unscaled time
        public float expectedHitAudioTime; // not used yet, can be used for tighter scoring
        public float travelTime;
    public NoteUIAnimator visual;
    }
}
