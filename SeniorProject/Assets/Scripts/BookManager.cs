using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class BookManager : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    // Panel referansı
    [SerializeField] private GameObject panel;
    
    // Panelin aktif olup olmadığını kontrol eden değişken
    private bool isPanelActive = false;
    
    [Header("Bookmarks → Pages")]
    [Tooltip("Bookmark tuşu (örn: 'D') ile sahnedeki target sayfa objesi eşleşmesi")] 
    public List<BookmarkPage> bookmarkPages = new List<BookmarkPage>();
    
    [Tooltip("Sayfaları GameObject olarak kapat/aç yerine tek bir UI Image üzerinde sprite değişimi kullan")] 
    public bool useImageSwap = false;
    
    [Tooltip("useImageSwap=true ise hedef UI Image")]
    public UnityEngine.UI.Image pageImage;
    
    [Tooltip("useImageSwap=true ise harf → sprite eşleşmeleri")] 
    public List<BookmarkSprite> bookmarkSprites = new List<BookmarkSprite>();

    [Header("Dual Page (Image)")]
    [Tooltip("Her harfin iki yüzü (sol/sağ) olarak iki UI Image kullanılacaksa işaretleyin")] 
    public bool useDualPage = false;
    
    [Tooltip("useDualPage + useImageSwap: Sol sayfa Image")]
    public UnityEngine.UI.Image leftPageImage;
    
    [Tooltip("useDualPage + useImageSwap: Sağ sayfa Image")]
    public UnityEngine.UI.Image rightPageImage;
    
    [System.Serializable]
    public class BookmarkDualSprite
    {
        [Tooltip("Harf veya anahtar (örn: D)")] public string key;
        [Tooltip("Sol sayfa sprite'ı")] public Sprite left;
        [Tooltip("Sağ sayfa sprite'ı")] public Sprite right;
    }
    
    [Tooltip("useDualPage aktifken harf → (sol, sağ) sprite eşleşmeleri")] 
    public List<BookmarkDualSprite> bookmarkDualSprites = new List<BookmarkDualSprite>();


    [Tooltip("Sayfaları SpriteRenderer üzerinde sprite değişimi ile göster")] 
    public bool useSpriteRendererSwap = false;
    
    [Tooltip("useSpriteRendererSwap=true ise hedef SpriteRenderer")]
    public SpriteRenderer pageSpriteRenderer;
    
    [Tooltip("GameObject sayfa modu için tüm sayfaların bulunduğu parent (opsiyonel)")] 
    public Transform pagesRoot;
    
    private readonly Dictionary<string, GameObject> _pageMap = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, Sprite> _spriteMap = new Dictionary<string, Sprite>();
    private readonly Dictionary<string, (Sprite left, Sprite right)> _dualSpriteMap = new Dictionary<string, (Sprite left, Sprite right)>();
    
    [Header("Panel Animations")]
    [Tooltip("Panel açılırken animasyon oynat")] public bool animatePanelOpen = true;
    [Tooltip("Panel kapanırken animasyon oynat")] public bool animatePanelClose = true;
    [Tooltip("Panel açılış süresi (sn)")] public float panelOpenDuration = 0.2f;
    [Tooltip("Panel kapanış süresi (sn)")] public float panelCloseDuration = 0.15f;
    [Tooltip("Açılırken panel ölçeğinin başlangıç çarpanı (1=orijinal)")] public float panelOpenScaleFrom = 0.9f;
    [Tooltip("Panel animasyonu easing eğrisi")] public AnimationCurve panelEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [Tooltip("Panelin Y ekseninde hareket mesafesi (px)")] public float panelTravelY = 80f;
    [Tooltip("Açılışta panel aşağıdan gelsin")] public bool panelOpenFromBottom = true;

    [Header("Page Animations")]
    [Tooltip("Sayfa görünürken alpha ile fade-in uygula")] public bool animatePageFade = true;
    [Tooltip("Sayfa fade-in süresi (sn)")] public float pageFadeInDuration = 0.15f;
    [Tooltip("Dual sayfalar arasında küçük gecikme (sn)")] public float dualPageStagger = 0.04f;

    private CanvasGroup _panelCg;
    private Vector3 _panelInitialScale = Vector3.one;
    private Coroutine _panelAnimCo;
    private Coroutine _fadeSingleImageCo, _fadeLeftImageCo, _fadeRightImageCo, _fadeSpriteRendererCo;
    private RectTransform _panelRt;
    private Vector2 _panelInitialAnchoredPos;
    private Vector3 _panelInitialLocalPos;
    private bool _panelHasRectTransform;

    
    [Header("Hover Feedback")]
    [Tooltip("Mouse ile kitap üzerindeyken uygulanacak opaklık (0-1 arası)")]
    [Range(0f, 1f)] public float hoverOpacity = 0.9f;
    [Tooltip("Alt objelerdeki renderer'lara da uygula")] public bool includeChildren = true;
    [Tooltip("Renk özelliği için denenecek property adları (örn: _Color, _BaseColor)")]
    public string[] colorPropertyNames = new[] { "_Color", "_BaseColor" };

    private Renderer[] _renderers;
    private MaterialPropertyBlock _mpb;
    private Dictionary<Renderer, float> _originalAlpha = new Dictionary<Renderer, float>();
    private bool _hoverApplied;
    
    [Header("Hover Scale (UI/Image)")]
    [Tooltip("Hover'da büyütme efektini etkinleştir")] public bool enableHoverScale = true;
    [Tooltip("Hedef ölçek çarpanı (1 = orijinal)")] public float hoverScale = 1.08f;
    [Tooltip("Hover'a geçiş süresi (sn)")] public float scaleInDuration = 0.12f;
    [Tooltip("Hover'dan çıkış süresi (sn)")] public float scaleOutDuration = 0.12f;
    private Vector3 _initialScale;
    private Coroutine _scaleCo;
    private bool _isPointerOver;

    [Header("Hover Cursor")]
    [Tooltip("Objenin üstüne gelince değişecek cursor görseli")] public Texture2D hoverCursor;
    [Tooltip("Cursor hotspot (piksel)")] public Vector2 cursorHotspot = Vector2.zero;
    [Tooltip("Cursor boyutu (px). (0,0) ise orijinal boyut kullanılır")] public Vector2 cursorSize = Vector2.zero;
    
    void Start()
    {
        // Başlangıçta paneli kapalı duruma getir
        if (panel != null)
        {
            panel.SetActive(false);
        }

    // Renderers'ı topla
    _renderers = includeChildren ? GetComponentsInChildren<Renderer>(true) : GetComponents<Renderer>();
    _mpb = new MaterialPropertyBlock();
    CacheOriginalAlphas();
        _initialScale = transform.localScale;

        // Panel başlangıç scale'ını hatırla
        if (panel != null)
        {
            _panelInitialScale = panel.transform.localScale;
            _panelCg = panel.GetComponent<CanvasGroup>();
            _panelRt = panel.transform as RectTransform;
            _panelHasRectTransform = _panelRt != null;
            if (_panelHasRectTransform)
            {
                _panelInitialAnchoredPos = _panelRt.anchoredPosition;
            }
            else
            {
                _panelInitialLocalPos = panel.transform.localPosition;
            }
        }

        InitBookmarkMaps();
        if (!useImageSwap && !useSpriteRendererSwap)
        {
            HideAllPages();
        }
        else if (useImageSwap)
        {
            // Image-swap modunda başlangıçta gizle
            if (!useDualPage && pageImage != null)
            {
                pageImage.enabled = false;
            }
            if (useDualPage)
            {
                if (leftPageImage != null) leftPageImage.enabled = false;
                if (rightPageImage != null) rightPageImage.enabled = false;
            }
        }
        else if (useSpriteRendererSwap)
        {
            // SpriteRenderer-swap modunda başlangıçta gizle
            if (pageSpriteRenderer != null)
            {
                pageSpriteRenderer.enabled = false;
            }
        }
    }

    void Update()
    {
        // ESC tuşuna basıldığında ve panel açıksa, paneli kapat
    if (InputHelper.GetKeyDown(KeyCode.Escape))
        {
            // If any drag is in progress, cancel it first and consume ESC
            if (DragAndDropHandler.TryCancelCurrentDragAndConsumeEsc())
            {
                return;
            }
            // If drag-and-drop consumed ESC for cancel, ignore here
            if (DragAndDropHandler.DidConsumeEscapeThisFrame())
            {
                return;
            }
            if (isPanelActive)
            {
                ClosePanel();
                // ESC bu frame tüketilsin ki PauseMenu açılmasın
                MarketManager.s_lastEscapeConsumedFrame = Time.frameCount;
                return;
            }
        }
    }
    
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
        // Reset scale on disable to avoid stuck scale
        if (enableHoverScale)
        {
            if (_scaleCo != null) StopCoroutine(_scaleCo);
            transform.localScale = _initialScale;
            // Güvenlik: açıkken disable olursa modal bayrağı temizle
            if (isPanelActive)
            {
                isPanelActive = false;
                if (panel != null) panel.SetActive(false);
                ModalPanelManager.Close();
            }
        }
    }

    // Mouse tıklaması algılandığında çağrılır
    private void OnMouseDown()
    {
    StartClickScale();
        // Block world clicks while Pause menu is open
        if (PauseMenuController.IsPausedGlobally)
        {
            return;
        }
        // If Market or another modal UI is open, ignore world clicks
        if (MarketManager.IsAnyOpen || ModalPanelManager.IsAnyOpen)
        {
            return;
        }
        OpenPanel();
    }

    private void OnMouseUp()
    {
        ReleaseClickScale();
    }
    
    // Paneli açan metod
    private void OpenPanel()
    {
        if (panel != null)
        {
            // Eğer animasyon açıksa, görünür yapıp animasyonu başlat
            if (animatePanelOpen)
            {
                if (_panelAnimCo != null) StopCoroutine(_panelAnimCo);
                panel.SetActive(true);
                EnsureCanvasGroup();
                if (_panelCg != null) _panelCg.alpha = 0f;
                panel.transform.localScale = _panelInitialScale * Mathf.Max(0.01f, panelOpenScaleFrom);
                _panelAnimCo = StartCoroutine(AnimatePanel(true, panelOpenDuration));
            }
            else
            {
                panel.SetActive(true);
                isPanelActive = true;
            }
            isPanelActive = true;
            ModalPanelManager.Open();
        }
        else
        {
            Debug.LogError("Panel atanmamış! Inspector'da panel referansını ayarlayın.");
        }
    }
    
    // Paneli kapatan metod
    private void ClosePanel()
    {
        if (panel != null)
        {
            if (animatePanelClose)
            {
                if (_panelAnimCo != null) StopCoroutine(_panelAnimCo);
                EnsureCanvasGroup();
                _panelAnimCo = StartCoroutine(AnimatePanel(false, panelCloseDuration));
                return;
            }
            // Animasyon yoksa anında kapa
            DoHidePagesAndDisablePanel();
            ModalPanelManager.Close();
        }
    }

    // Panel durumunu kontrol etmek için public metod
    public bool IsPanelActive()
    {
        return isPanelActive;
    }

    // UI EventSystem handlers (for Image-based objects)
    public void OnPointerEnter(PointerEventData eventData)
    {
    StartHoverScale();
    StartHoverCursor();
    _isPointerOver = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
    EndHoverScale();
    EndHoverCursor();
        _isPointerOver = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        StartClickScale();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        ReleaseClickScale();
    }

    // ========== Bookmark/Page Setup & Control ==========
    [System.Serializable]
    public class BookmarkPage
    {
        [Tooltip("Harf veya anahtar (örn: D, A, B...)")] public string key;
        [Tooltip("Bu harfe karşılık gelen sayfa GameObject'i")] public GameObject pageObject;
    }

    [System.Serializable]
    public class BookmarkSprite
    {
        [Tooltip("Harf veya anahtar (örn: D, A, B...)")] public string key;
        [Tooltip("Bu harfe karşılık gelen sayfa sprite'ı")] public Sprite sprite;
    }

    private void InitBookmarkMaps()
    {
        _pageMap.Clear();
        _spriteMap.Clear();
        _dualSpriteMap.Clear();
        

        // Normalize keys to upper-invariant
        for (int i = 0; i < bookmarkPages.Count; i++)
        {
            var bp = bookmarkPages[i];
            if (bp == null || string.IsNullOrWhiteSpace(bp.key)) continue;
            string k = bp.key.Trim().ToUpperInvariant();
            if (!_pageMap.ContainsKey(k) && bp.pageObject != null)
            {
                _pageMap.Add(k, bp.pageObject);
            }
        }

        // Single page sprite mapping
        for (int i = 0; i < bookmarkSprites.Count; i++)
        {
            var bs = bookmarkSprites[i];
            if (bs == null || string.IsNullOrWhiteSpace(bs.key)) continue;
            string k = bs.key.Trim().ToUpperInvariant();
            if (!_spriteMap.ContainsKey(k) && bs.sprite != null)
            {
                _spriteMap.Add(k, bs.sprite);
            }
        }

        // Dual page sprite mapping (Image modu için)
        for (int i = 0; i < bookmarkDualSprites.Count; i++)
        {
            var ds = bookmarkDualSprites[i];
            if (ds == null || string.IsNullOrWhiteSpace(ds.key)) continue;
            string k = ds.key.Trim().ToUpperInvariant();
            if (!_dualSpriteMap.ContainsKey(k))
            {
                _dualSpriteMap.Add(k, (ds.left, ds.right));
            }
        }

        
    }

    private void HideAllPages()
    {
        if (useImageSwap) return;
        if (pagesRoot != null)
        {
            // Eğer parent belirtildiyse sadece onun altındakileri kapat
            for (int i = 0; i < pagesRoot.childCount; i++)
            {
                var child = pagesRoot.GetChild(i);
                if (child != null) child.gameObject.SetActive(false);
            }
        }
        else
        {
            // Aksi halde listede olanları kapat
            foreach (var kvp in _pageMap)
            {
                if (kvp.Value != null) kvp.Value.SetActive(false);
            }
        }
    }

    private void ShowPageByKey(string rawKey)
    {
        if (string.IsNullOrWhiteSpace(rawKey)) return;
        string key = rawKey.Trim().ToUpperInvariant();

        // Panel kapalıysa aç
        if (!isPanelActive)
        {
            OpenPanel();
        }

        if (useImageSwap && !useDualPage)
        {
            if (pageImage == null)
            {
                Debug.LogWarning("useImageSwap etkin, fakat pageImage atanmadı.");
                return;
            }
            if (_spriteMap.TryGetValue(key, out var sprite))
            {
                if (_fadeSingleImageCo != null) StopCoroutine(_fadeSingleImageCo);
                pageImage.enabled = true;
                pageImage.sprite = sprite;
                if (animatePageFade)
                {
                    _fadeSingleImageCo = StartCoroutine(FadeImage(pageImage, 0f, 1f, Mathf.Max(0.01f, pageFadeInDuration)));
                }
                else
                {
                    var c = pageImage.color; c.a = 1f; pageImage.color = c;
                }
            }
            else
            {
                Debug.LogWarning($"'{key}' için sprite bulunamadı.");
            }
        }
        else if (useImageSwap && useDualPage)
        {
            if (leftPageImage == null || rightPageImage == null)
            {
                Debug.LogWarning("useDualPage + useImageSwap etkin, fakat left/right Image atanmadı.");
                return;
            }
            if (_dualSpriteMap.TryGetValue(key, out var pair))
            {
                // Sol
                leftPageImage.enabled = pair.left != null;
                if (pair.left != null)
                {
                    if (_fadeLeftImageCo != null) StopCoroutine(_fadeLeftImageCo);
                    leftPageImage.sprite = pair.left;
                    if (animatePageFade)
                        _fadeLeftImageCo = StartCoroutine(FadeImage(leftPageImage, 0f, 1f, Mathf.Max(0.01f, pageFadeInDuration)));
                    else
                    {
                        var cl = leftPageImage.color; cl.a = 1f; leftPageImage.color = cl;
                    }
                }
                // Sağ (küçük gecikme ile)
                rightPageImage.enabled = pair.right != null;
                if (pair.right != null)
                {
                    if (_fadeRightImageCo != null) StopCoroutine(_fadeRightImageCo);
                    rightPageImage.sprite = pair.right;
                    if (animatePageFade)
                        _fadeRightImageCo = StartCoroutine(FadeImage(rightPageImage, 0f, 1f, Mathf.Max(0.01f, pageFadeInDuration), dualPageStagger));
                    else
                    {
                        var cr = rightPageImage.color; cr.a = 1f; rightPageImage.color = cr;
                    }
                }
            }
            else
            {
                Debug.LogWarning($"'{key}' için (sol, sağ) sprite bulunamadı.");
            }
        }
        else if (useSpriteRendererSwap)
        {
            if (pageSpriteRenderer == null)
            {
                Debug.LogWarning("useSpriteRendererSwap etkin, fakat pageSpriteRenderer atanmadı.");
                return;
            }
            if (_spriteMap.TryGetValue(key, out var sprite))
            {
                if (_fadeSpriteRendererCo != null) StopCoroutine(_fadeSpriteRendererCo);
                pageSpriteRenderer.enabled = true;
                pageSpriteRenderer.sprite = sprite;
                if (animatePageFade)
                {
                    _fadeSpriteRendererCo = StartCoroutine(FadeSprite(pageSpriteRenderer, 0f, 1f, Mathf.Max(0.01f, pageFadeInDuration)));
                }
                else
                {
                    var c = pageSpriteRenderer.color; c.a = 1f; pageSpriteRenderer.color = c;
                }
            }
            else
            {
                Debug.LogWarning($"'{key}' için sprite bulunamadı.");
            }
        }
        else
        {
            // Önce tüm sayfaları gizle
            HideAllPages();
            if (_pageMap.TryGetValue(key, out var pageObj))
            {
                if (pageObj != null)
                {
                    pageObj.SetActive(true);
                }
            }
            else
            {
                Debug.LogWarning($"'{key}' için sayfa GameObject bulunamadı.");
            }
        }
    }

    // UI Button veya 3D/2D Bookmark objelerinden bağlanacak genel handler
    public void OnBookmarkClicked(string key)
    {
        // World click engellemeleri varsa burada da saygı göster
        if (PauseMenuController.IsPausedGlobally) return;
        if (MarketManager.IsAnyOpen) return;

        ShowPageByKey(key);
    }

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
        if (duration <= 0.0001f)
        {
            transform.localScale = target; yield break;
        }
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime; // UI feel independent of timescale
            float u = Mathf.Clamp01(t / duration);
            transform.localScale = Vector3.Lerp(start, target, u);
            yield return null;
        }
        transform.localScale = target;
        _scaleCo = null;
    }

    [Header("Click Scale (Press)")]
    [Tooltip("Tıklanırken küçülme efekti")] public bool enableClickScale = true;
    [Tooltip("Tıklama sırasında çarpan (1'den küçük önerilir, örn: 0.96)")] public float clickScale = 0.96f;
    [Tooltip("Basılıya geçiş süresi (sn)")] public float clickScaleInDuration = 0.06f;
    [Tooltip("Basılıdan çıkış süresi (sn)")] public float clickScaleOutDuration = 0.08f;

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
        // Restore to hover or normal depending on pointer state
        Vector3 target = _isPointerOver && enableHoverScale ? _initialScale * Mathf.Max(0.01f, hoverScale) : _initialScale;
        _scaleCo = StartCoroutine(ScaleTo(target, Mathf.Max(0.01f, clickScaleOutDuration)));
    }

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
        if (cm != null)
        {
            cm.UseDefaultNow();
        }
        else
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
    }

    // ========== Animations ==========
    private void EnsureCanvasGroup()
    {
        if (panel == null) return;
        if (_panelCg == null)
        {
            _panelCg = panel.GetComponent<CanvasGroup>();
            if (_panelCg == null)
            {
                _panelCg = panel.AddComponent<CanvasGroup>();
            }
        }
    }

    private IEnumerator AnimatePanel(bool opening, float duration)
    {
        if (panel == null)
        {
            yield break;
        }
        EnsureCanvasGroup();
        float d = Mathf.Max(0.01f, duration);
        float t = 0f;
        float fromA = opening ? 0f : (_panelCg != null ? _panelCg.alpha : 1f);
        float toA = opening ? 1f : 0f;
        Vector3 fromS = opening ? _panelInitialScale * Mathf.Max(0.01f, panelOpenScaleFrom) : panel.transform.localScale;
        Vector3 toS = opening ? _panelInitialScale : _panelInitialScale * Mathf.Max(0.01f, panelOpenScaleFrom);
        // Position (Y) motion
        float travel = Mathf.Abs(panelTravelY);
        float dir = panelOpenFromBottom ? -1f : 1f; // opening from below means start lower (negative y)
        Vector2 fromAP = _panelHasRectTransform
            ? (opening ? _panelInitialAnchoredPos + new Vector2(0f, dir * travel) : _panelRt.anchoredPosition)
            : Vector2.zero;
        Vector2 toAP = _panelHasRectTransform
            ? (opening ? _panelInitialAnchoredPos : _panelInitialAnchoredPos + new Vector2(0f, dir * travel))
            : Vector2.zero;
        Vector3 fromLP = !_panelHasRectTransform
            ? (opening ? _panelInitialLocalPos + new Vector3(0f, dir * travel, 0f) : panel.transform.localPosition)
            : Vector3.zero;
        Vector3 toLP = !_panelHasRectTransform
            ? (opening ? _panelInitialLocalPos : _panelInitialLocalPos + new Vector3(0f, dir * travel, 0f))
            : Vector3.zero;
        // Set initial position at start
        if (opening)
        {
            if (_panelHasRectTransform) _panelRt.anchoredPosition = fromAP;
            else panel.transform.localPosition = fromLP;
        }
        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / d);
            float e = panelEase != null ? panelEase.Evaluate(u) : u;
            if (_panelCg != null) _panelCg.alpha = Mathf.Lerp(fromA, toA, e);
            panel.transform.localScale = Vector3.Lerp(fromS, toS, e);
            if (_panelHasRectTransform)
            {
                _panelRt.anchoredPosition = Vector2.Lerp(fromAP, toAP, e);
            }
            else
            {
                panel.transform.localPosition = Vector3.Lerp(fromLP, toLP, e);
            }
            yield return null;
        }
        if (_panelCg != null) _panelCg.alpha = toA;
        panel.transform.localScale = toS;
        if (_panelHasRectTransform) _panelRt.anchoredPosition = toAP;
        else panel.transform.localPosition = toLP;
        if (!opening)
        {
            DoHidePagesAndDisablePanel();
        }
        _panelAnimCo = null;
    }

    private void DoHidePagesAndDisablePanel()
    {
        // Panel kapanınca sayfaları gizle/görseli kapat
        if (!useImageSwap && !useSpriteRendererSwap)
        {
            HideAllPages();
        }
        if (useImageSwap && !useDualPage && pageImage != null)
        {
            pageImage.enabled = false;
        }
        if (useImageSwap && useDualPage)
        {
            if (leftPageImage != null) leftPageImage.enabled = false;
            if (rightPageImage != null) rightPageImage.enabled = false;
        }
        if (useSpriteRendererSwap && pageSpriteRenderer != null)
        {
            pageSpriteRenderer.enabled = false;
        }

        // Reset transform for next open
        if (_panelHasRectTransform) _panelRt.anchoredPosition = _panelInitialAnchoredPos;
        else panel.transform.localPosition = _panelInitialLocalPos;
        panel.transform.localScale = _panelInitialScale;

        panel.SetActive(false);
        isPanelActive = false;
        ModalPanelManager.Close();
    }

    private IEnumerator FadeImage(UnityEngine.UI.Image img, float from, float to, float duration, float delay = 0f)
    {
        if (img == null) yield break;
        if (delay > 0f)
        {
            float dt = 0f; while (dt < delay) { dt += Time.unscaledDeltaTime; yield return null; }
        }
        float d = Mathf.Max(0.01f, duration);
        Color c = img.color; c.a = from; img.color = c;
        float t = 0f;
        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / d);
            c.a = Mathf.Lerp(from, to, u);
            img.color = c;
            yield return null;
        }
        c.a = to; img.color = c;
    }

    private IEnumerator FadeSprite(SpriteRenderer sr, float from, float to, float duration)
    {
        if (sr == null) yield break;
        float d = Mathf.Max(0.01f, duration);
        Color c = sr.color; c.a = from; sr.color = c;
        float t = 0f;
        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / d);
            c.a = Mathf.Lerp(from, to, u);
            sr.color = c;
            yield return null;
        }
        c.a = to; sr.color = c;
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
                float u = (float)x / newWidth;
                float v = (float)y / newHeight;
                Color pixel = readable.GetPixelBilinear(u, v);
                scaled.SetPixel(x, y, pixel);
            }
        }
        scaled.Apply();
        if (readable != originalCursor)
        {
            DestroyImmediate(readable);
        }
        return scaled;
    }

    private Texture2D MakeTextureReadable(Texture2D texture)
    {
        if (texture == null) return null;
        try
        {
            texture.GetPixel(0, 0);
            return texture;
        }
        catch
        {
            RenderTexture rt = RenderTexture.GetTemporary(texture.width, texture.height);
            Graphics.Blit(texture, rt);
            RenderTexture.active = rt;
            Texture2D readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            readable.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            return readable;
        }
    }

    private void CacheOriginalAlphas()
    {
        _originalAlpha.Clear();
        if (_renderers == null) return;
        for (int i = 0; i < _renderers.Length; i++)
        {
            var r = _renderers[i];
            if (r == null) continue;
            float a = GetRendererAlpha(r);
            _originalAlpha[r] = a;
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
                    if (m.HasProperty(prop))
                    {
                        Color c = m.GetColor(prop);
                        return c.a;
                    }
                }
            }
        }
        // SpriteRenderer özel durumu
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
            var r = _renderers[i];
            if (r == null) continue;
            bool applied = false;
            var mats = r.sharedMaterials;
            if (mats != null && mats.Length > 0)
            {
                for (int pi = 0; pi < colorPropertyNames.Length; pi++)
                {
                    string prop = colorPropertyNames[pi];
                    if (mats[0] != null && mats[0].HasProperty(prop))
                    {
                        r.GetPropertyBlock(_mpb);
                        Color c = mats[0].GetColor(prop);
                        c.a = alpha;
                        _mpb.SetColor(prop, c);
                        r.SetPropertyBlock(_mpb);
                        applied = true;
                        break;
                    }
                }
            }
            if (!applied)
            {
                var sr = r.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    var c = sr.color; c.a = alpha; sr.color = c; applied = true;
                }
            }
        }
    }

    private void RestoreAlphaOnRenderers()
    {
        if (_renderers == null) return;
        for (int i = 0; i < _renderers.Length; i++)
        {
            var r = _renderers[i];
            if (r == null) continue;
            float alpha = 1f;
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
                        Color c = mats[0].GetColor(prop);
                        c.a = alpha;
                        _mpb.SetColor(prop, c);
                        r.SetPropertyBlock(_mpb);
                        restored = true;
                        break;
                    }
                }
            }
            if (!restored)
            {
                var sr = r.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    var c = sr.color; c.a = alpha; sr.color = c; restored = true;
                }
            }
        }
    }
}