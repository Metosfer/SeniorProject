using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BookManager : MonoBehaviour
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
    }

    private void OnMouseExit()
    {
        RestoreAlphaOnRenderers();
        _hoverApplied = false;
    }

    private void OnDisable()
    {
        if (_hoverApplied)
        {
            RestoreAlphaOnRenderers();
            _hoverApplied = false;
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