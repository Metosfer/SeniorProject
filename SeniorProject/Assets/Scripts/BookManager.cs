using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class BookManager : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
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
    }

    private void OnMouseExit()
    {
        RestoreAlphaOnRenderers();
        _hoverApplied = false;
    EndHoverScale();
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
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        EndHoverScale();
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