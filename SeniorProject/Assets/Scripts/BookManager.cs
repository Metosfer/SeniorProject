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
    }

    void Update()
    {
        // ESC tuşuna basıldığında ve panel açıksa, paneli kapat
        if (Input.GetKeyDown(KeyCode.Escape) && isPanelActive)
        {
            ClosePanel();
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
        if (MarketManager.IsAnyOpen)
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
            panel.SetActive(true);
            isPanelActive = true;
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
            panel.SetActive(false);
            isPanelActive = false;
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
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
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