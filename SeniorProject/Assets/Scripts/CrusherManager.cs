using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Crusher (havan) dünya objesinde tıklama/hover + panel açma ve paneldeki image'e tıklamada animasyon
public class CrusherManager : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Panel")]
    [SerializeField] private GameObject panel;                 // Açılacak panel
    [SerializeField] private Image panelImage;                  // Panelde tıklanacak Image (Button olarak da kullanılabilir)
    [SerializeField] private Animator panelImageAnimator;       // Image üzerinde Animator
    [SerializeField] private string imageClickTrigger = "Play"; // Tıklamada tetiklenecek trigger adı

    private bool isPanelActive = false;
    private CanvasGroup _panelCg;
    private Coroutine _panelAnimCo;
    private RectTransform _panelRt;
    private Vector2 _panelInitialAnchoredPos;
    private Vector3 _panelInitialLocalPos;
    private bool _panelHasRectTransform;

    [Header("Hover Feedback (World)")]
    [Tooltip("Hover'da opaklık (0-1)")] [Range(0f, 1f)] public float hoverOpacity = 0.9f;
    [Tooltip("Alt objelerin renderer'larını da dahil et")] public bool includeChildren = true;
    [Tooltip("Renk property adları")] public string[] colorPropertyNames = new[] { "_Color", "_BaseColor" };

    private Renderer[] _renderers;
    private MaterialPropertyBlock _mpb;
    private readonly Dictionary<Renderer, float> _originalAlpha = new Dictionary<Renderer, float>();
    private bool _hoverApplied;

    [Header("Hover Scale (UI/Image)")]
    public bool enableHoverScale = true;
    public float hoverScale = 1.08f;
    public float scaleInDuration = 0.12f;
    public float scaleOutDuration = 0.12f;
    private Vector3 _initialScale;
    private Coroutine _scaleCo;
    private bool _isPointerOver;

    [Header("Hover Cursor")]
    public Texture2D hoverCursor;
    public Vector2 cursorHotspot = Vector2.zero;
    public Vector2 cursorSize = Vector2.zero;

    [Header("Click Scale (Press)")]
    public bool enableClickScale = true;
    public float clickScale = 0.96f;
    public float clickScaleInDuration = 0.06f;
    public float clickScaleOutDuration = 0.08f;

    [Header("Panel Animations")]
    public bool animatePanelOpen = true;
    public bool animatePanelClose = true;
    public float panelOpenDuration = 0.18f;
    public float panelCloseDuration = 0.14f;
    public float panelOpenScaleFrom = 0.9f;
    public AnimationCurve panelEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public float panelTravelY = 80f;
    public bool panelOpenFromBottom = true;

    void Start()
    {
        if (panel != null) panel.SetActive(false);
        if (panel != null)
        {
            _panelRt = panel.GetComponent<RectTransform>();
            _panelHasRectTransform = _panelRt != null;
            if (_panelHasRectTransform)
            {
                _panelInitialAnchoredPos = _panelRt.anchoredPosition;
            }
            else
            {
                _panelInitialLocalPos = panel.transform.localPosition;
            }
            EnsureCanvasGroup();
        }
        _renderers = includeChildren ? GetComponentsInChildren<Renderer>(true) : GetComponents<Renderer>();
        _mpb = new MaterialPropertyBlock();
        CacheOriginalAlphas();
        _initialScale = transform.localScale;

        // Panel Image üzerindeki Button varsa otomatik bağla
        if (panelImage != null)
        {
            var btn = panelImage.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveListener(OnPanelImageClicked);
                btn.onClick.AddListener(OnPanelImageClicked);
            }
        }
    }

    void Update()
    {
    if (InputHelper.GetKeyDown(KeyCode.Escape))
        {
            // Önce sürükleme varsa iptal edip ESC'yi tüket
            if (DragAndDropHandler.TryCancelCurrentDragAndConsumeEsc()) return;
            if (DragAndDropHandler.DidConsumeEscapeThisFrame()) return;

            // Panel açıksa önce onu kapat ve ESC'yi bu frame için tükendi olarak işaretle
            if (isPanelActive)
            {
                ClosePanel();
                MarketManager.s_lastEscapeConsumedFrame = Time.frameCount;
                return;
            }
        }
    }

    // World hover
    private void OnMouseEnter()
    {
        ApplyAlphaToRenderers(hoverOpacity);
        _hoverApplied = true;
        StartHoverScale();
        StartHoverCursor();
        _isPointerOver = true;
    }

    private void OnMouseExit()
    {
        RestoreAlphaOnRenderers();
        _hoverApplied = false;
        EndHoverScale();
        EndHoverCursor();
        _isPointerOver = false;
    }

    private void OnDisable()
    {
        if (_hoverApplied)
        {
            RestoreAlphaOnRenderers();
            _hoverApplied = false;
        }
        if (enableHoverScale)
        {
            if (_scaleCo != null) StopCoroutine(_scaleCo);
            transform.localScale = _initialScale;
        }
        // Güvenlik: açıkken disable olursa modal bayrağı temizle
        if (isPanelActive)
        {
            isPanelActive = false;
            if (panel != null) panel.SetActive(false);
            ModalPanelManager.Close();
        }
    }

    // World click to open panel
    private void OnMouseDown()
    {
        StartClickScale();
        if (PauseMenuController.IsPausedGlobally) return;
        // Market veya başka bir modal panel açıksa dünya tıklamalarını yok say
        if (MarketManager.IsAnyOpen || ModalPanelManager.IsAnyOpen) return;
        OpenPanel();
    }

    private void OnMouseUp()
    {
        ReleaseClickScale();
    }

    // Panel open/close
    private void OpenPanel()
    {
        if (panel == null)
        {
            Debug.LogError("Crusher panel atanmamış!");
            return;
        }
        panel.SetActive(true);
        isPanelActive = true;
        ModalPanelManager.Open();

        if (animatePanelOpen)
        {
            if (_panelAnimCo != null) StopCoroutine(_panelAnimCo);
            _panelAnimCo = StartCoroutine(AnimatePanel(true, panelOpenDuration));
        }
    }

    private void ClosePanel()
    {
        if (panel == null) return;
        if (animatePanelClose)
        {
            if (_panelAnimCo != null) StopCoroutine(_panelAnimCo);
            _panelAnimCo = StartCoroutine(AnimatePanel(false, panelCloseDuration));
        }
        else
        {
            panel.SetActive(false);
            isPanelActive = false;
            ModalPanelManager.Close();
        }
    }

    public bool IsPanelActive() => isPanelActive;

    public void OnPanelImageClicked()
    {
        if (panelImageAnimator != null && !string.IsNullOrEmpty(imageClickTrigger))
        {
            panelImageAnimator.ResetTrigger(imageClickTrigger); // temiz başlat
            panelImageAnimator.SetTrigger(imageClickTrigger);
        }
        else
        {
            // Animator yoksa, basit bir puls efekti uygula
            if (panelImage != null)
            {
                StartCoroutine(ImagePulse(panelImage.rectTransform));
            }
        }
    }

    private IEnumerator ImagePulse(RectTransform rt)
    {
        if (rt == null) yield break;
        Vector3 baseScale = rt.localScale;
        Vector3 target = baseScale * 1.06f;
        float dIn = 0.06f, dOut = 0.08f;
        float t = 0f;
        while (t < dIn) { t += Time.unscaledDeltaTime; rt.localScale = Vector3.Lerp(baseScale, target, Mathf.Clamp01(t / dIn)); yield return null; }
        t = 0f; while (t < dOut) { t += Time.unscaledDeltaTime; rt.localScale = Vector3.Lerp(target, baseScale, Mathf.Clamp01(t / dOut)); yield return null; }
        rt.localScale = baseScale;
    }

    // UI EventSystem handlers (if this script is used on UI elements too)
    public void OnPointerEnter(PointerEventData eventData) { StartHoverScale(); StartHoverCursor(); _isPointerOver = true; }
    public void OnPointerExit(PointerEventData eventData) { EndHoverScale(); EndHoverCursor(); _isPointerOver = false; }
    public void OnPointerDown(PointerEventData eventData) { StartClickScale(); }
    public void OnPointerUp(PointerEventData eventData) { ReleaseClickScale(); }

    // Hover/Click scale helpers
    private void StartHoverScale()
    {
        if (!enableHoverScale) return;
        if (_scaleCo != null) StopCoroutine(_scaleCo);
        _scaleCo = StartCoroutine(ScaleTo(_initialScale * Mathf.Max(0.01f, hoverScale), Mathf.Max(0.01f, scaleInDuration)));
    }
    private void EndHoverScale()
    {
        if (!enableHoverScale) return;
        if (_scaleCo != null) StopCoroutine(_scaleCo);
        _scaleCo = StartCoroutine(ScaleTo(_initialScale, Mathf.Max(0.01f, scaleOutDuration)));
    }
    private IEnumerator ScaleTo(Vector3 target, float duration)
    {
        Vector3 start = transform.localScale;
        if (duration <= 0.0001f) { transform.localScale = target; yield break; }
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / duration);
            transform.localScale = Vector3.Lerp(start, target, u);
            yield return null;
        }
        transform.localScale = target; _scaleCo = null;
    }
    private void StartClickScale()
    {
        if (!enableClickScale) return;
        if (_scaleCo != null) StopCoroutine(_scaleCo);
        float mult = Mathf.Clamp(clickScale, 0.5f, 1.5f);
        Vector3 target = transform.localScale * mult;
        _scaleCo = StartCoroutine(ScaleTo(target, Mathf.Max(0.01f, clickScaleInDuration)));
    }
    private void ReleaseClickScale()
    {
        if (!enableClickScale) return;
        if (_scaleCo != null) StopCoroutine(_scaleCo);
        Vector3 target = _isPointerOver && enableHoverScale ? _initialScale * Mathf.Max(0.01f, hoverScale) : _initialScale;
        _scaleCo = StartCoroutine(ScaleTo(target, Mathf.Max(0.01f, clickScaleOutDuration)));
    }

    // Cursor helpers
    private void StartHoverCursor()
    {
        if (hoverCursor == null) return;
        if (cursorSize != Vector2.zero && (hoverCursor.width != (int)cursorSize.x || hoverCursor.height != (int)cursorSize.y))
        {
            var scaled = ScaleCursor(hoverCursor, (int)cursorSize.x, (int)cursorSize.y);
            Cursor.SetCursor(scaled, cursorHotspot, CursorMode.Auto);
        }
        else
        {
            Cursor.SetCursor(hoverCursor, cursorHotspot, CursorMode.Auto);
        }
    }
    private void EndHoverCursor()
    {
        var cm = CursorManager.Instance;
        if (cm != null) cm.UseDefaultNow();
        else Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    private void EnsureCanvasGroup()
    {
        if (panel == null) return;
        _panelCg = panel.GetComponent<CanvasGroup>();
        if (_panelCg == null)
        {
            _panelCg = panel.AddComponent<CanvasGroup>();
        }
    }

    private IEnumerator AnimatePanel(bool opening, float duration)
    {
        if (panel == null) yield break;
        EnsureCanvasGroup();
        float d = Mathf.Max(0.01f, duration);
        float t = 0f;
        float startA = opening ? 0f : 1f;
        float endA = opening ? 1f : 0f;
        Vector3 startScale = opening ? Vector3.one * Mathf.Max(0.01f, panelOpenScaleFrom) : Vector3.one;
        Vector3 endScale = opening ? Vector3.one : Vector3.one * Mathf.Max(0.01f, panelOpenScaleFrom);
    Vector2 startPosA = Vector2.zero;
    Vector2 endPosA = Vector2.zero;
    Vector3 startPosL = Vector3.zero;
    Vector3 endPosL = Vector3.zero;
        float dir = panelOpenFromBottom ? -1f : 1f; // aşağıdan gelsin: ilk pozisyon aşağıda
        if (_panelHasRectTransform)
        {
            startPosA = opening ? _panelInitialAnchoredPos + new Vector2(0f, dir * panelTravelY) : _panelInitialAnchoredPos;
            endPosA = opening ? _panelInitialAnchoredPos : _panelInitialAnchoredPos + new Vector2(0f, dir * panelTravelY);
        }
        else
        {
            startPosL = opening ? _panelInitialLocalPos + new Vector3(0f, dir * panelTravelY, 0f) : _panelInitialLocalPos;
            endPosL = opening ? _panelInitialLocalPos : _panelInitialLocalPos + new Vector3(0f, dir * panelTravelY, 0f);
        }

        // initialize start state
        _panelCg.alpha = startA;
        if (_panelHasRectTransform)
            _panelRt.anchoredPosition = startPosA;
        else
            panel.transform.localPosition = startPosL;
        panel.transform.localScale = startScale;

        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / d);
            float e = panelEase != null ? panelEase.Evaluate(u) : u;
            _panelCg.alpha = Mathf.Lerp(startA, endA, e);
            panel.transform.localScale = Vector3.Lerp(startScale, endScale, e);
            if (_panelHasRectTransform)
                _panelRt.anchoredPosition = Vector2.Lerp(startPosA, endPosA, e);
            else
                panel.transform.localPosition = Vector3.Lerp(startPosL, endPosL, e);
            yield return null;
        }
        _panelCg.alpha = endA;
        if (_panelHasRectTransform)
            _panelRt.anchoredPosition = endPosA;
        else
            panel.transform.localPosition = endPosL;
        panel.transform.localScale = endScale;

        if (!opening)
        {
            panel.SetActive(false);
            isPanelActive = false;
            ModalPanelManager.Close();
        }
        _panelAnimCo = null;
    }

    private Texture2D ScaleCursor(Texture2D originalCursor, int newWidth, int newHeight)
    {
        if (originalCursor == null) return null;
        Texture2D readable = MakeTextureReadable(originalCursor);
        if (readable == null) return originalCursor;
        Texture2D scaled = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
        for (int x = 0; x < newWidth; x++)
        {
            for (int y = 0; y < newHeight; y++)
            {
                float u = (float)x / newWidth; float v = (float)y / newHeight;
                Color pixel = readable.GetPixelBilinear(u, v);
                scaled.SetPixel(x, y, pixel);
            }
        }
        scaled.Apply();
        if (readable != originalCursor) Destroy(readable);
        return scaled;
    }
    private Texture2D MakeTextureReadable(Texture2D texture)
    {
        if (texture == null) return null;
        try { texture.GetPixel(0, 0); return texture; }
        catch
        {
            RenderTexture rt = RenderTexture.GetTemporary(texture.width, texture.height);
            Graphics.Blit(texture, rt); RenderTexture.active = rt;
            Texture2D readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            readable.Apply(); RenderTexture.active = null; RenderTexture.ReleaseTemporary(rt);
            return readable;
        }
    }

    // Alpha helpers
    private void CacheOriginalAlphas()
    {
        _originalAlpha.Clear();
        if (_renderers == null) return;
        for (int i = 0; i < _renderers.Length; i++)
        {
            var r = _renderers[i]; if (r == null) continue;
            _originalAlpha[r] = GetRendererAlpha(r);
        }
    }
    private float GetRendererAlpha(Renderer r)
    {
        if (r == null) return 1f;
        var mats = r.sharedMaterials;
        if (mats != null && mats.Length > 0)
        {
            var m = mats[0];
            if (m != null)
            {
                for (int i = 0; i < colorPropertyNames.Length; i++)
                {
                    string prop = colorPropertyNames[i];
                    if (m.HasProperty(prop)) { Color c = m.GetColor(prop); return c.a; }
                }
            }
        }
        var sr = r.GetComponent<SpriteRenderer>();
        if (sr != null) return sr.color.a;
        return 1f;
    }
    private void ApplyAlphaToRenderers(float alpha)
    {
        if (_renderers == null) return;
        alpha = Mathf.Clamp01(alpha);
        for (int i = 0; i < _renderers.Length; i++)
        {
            var r = _renderers[i]; if (r == null) continue; bool applied = false;
            var mats = r.sharedMaterials;
            if (mats != null && mats.Length > 0)
            {
                for (int pi = 0; pi < colorPropertyNames.Length; pi++)
                {
                    string prop = colorPropertyNames[pi];
                    if (mats[0] != null && mats[0].HasProperty(prop))
                    {
                        r.GetPropertyBlock(_mpb);
                        Color c = mats[0].GetColor(prop); c.a = alpha;
                        _mpb.SetColor(prop, c); r.SetPropertyBlock(_mpb);
                        applied = true; break;
                    }
                }
            }
            if (!applied)
            {
                var sr = r.GetComponent<SpriteRenderer>();
                if (sr != null) { var c = sr.color; c.a = alpha; sr.color = c; applied = true; }
            }
        }
    }
    private void RestoreAlphaOnRenderers()
    {
        if (_renderers == null) return;
        for (int i = 0; i < _renderers.Length; i++)
        {
            var r = _renderers[i]; if (r == null) continue; float alpha = 1f;
            if (_originalAlpha.TryGetValue(r, out var a)) alpha = a;
            bool restored = false;
            var mats = r.sharedMaterials;
            if (mats != null && mats.Length > 0)
            {
                for (int pi = 0; pi < colorPropertyNames.Length; pi++)
                {
                    string prop = colorPropertyNames[pi];
                    if (mats[0] != null && mats[0].HasProperty(prop))
                    {
                        r.GetPropertyBlock(_mpb);
                        Color c = mats[0].GetColor(prop); c.a = alpha;
                        _mpb.SetColor(prop, c); r.SetPropertyBlock(_mpb);
                        restored = true; break;
                    }
                }
            }
            if (!restored)
            {
                var sr = r.GetComponent<SpriteRenderer>();
                if (sr != null) { var c = sr.color; c.a = alpha; sr.color = c; restored = true; }
            }
        }
    }
}
