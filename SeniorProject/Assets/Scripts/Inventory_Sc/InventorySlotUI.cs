using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class InventorySlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Image itemIcon;
    public TextMeshProUGUI itemCountText;
    public Image slotBackground;

    public Color normalColor = Color.white;
    public Color occupiedColor = Color.gray;
    public Color readyColor = Color.green;
    public Color hoverColor = new Color(1f, 1f, 0.8f, 1f);

    [Header("Cursor")]
    [Tooltip("İmleç slot üzerine geldiğinde değişecek texture")]
    public Texture2D hoverCursor;
    [Tooltip("İmleç hotspot (genelde texture'ın sol üst köşesi)")]
    public Vector2 cursorHotspot = Vector2.zero;
    [Tooltip("İmleç boyutu (piksel cinsinden). (0,0) ise texture'ın orijinal boyutu kullanılır")]
    public Vector2 cursorSize = Vector2.zero;

    private bool _isHovered = false;

    private void Awake()
    {
        // Transform.Find ile bileşenleri bul ve null kontrolü yap
        Transform iconTransform = transform.Find("Icon");
        Transform countTransform = transform.Find("Count");
        Transform bgTransform = transform.Find("Background");

        if (iconTransform != null)
        {
            itemIcon = iconTransform.GetComponent<Image>();
            if (itemIcon != null)
            {
                Debug.Log($"InventorySlotUI: Icon component found for {gameObject.name}");
            }
        }
        else
        {
            Debug.LogWarning($"InventorySlotUI: 'Icon' child bulunamadı! GameObject: {gameObject.name}");
        }

        if (countTransform != null)
        {
            itemCountText = countTransform.GetComponent<TextMeshProUGUI>();
            if (itemCountText != null)
            {
                Debug.Log($"InventorySlotUI: Count component found for {gameObject.name}");
            }
        }
        else
        {
            Debug.LogWarning($"InventorySlotUI: 'Count' child bulunamadı! GameObject: {gameObject.name}");
        }

        if (bgTransform != null)
        {
            slotBackground = bgTransform.GetComponent<Image>();
            if (slotBackground != null)
            {
                Debug.Log($"InventorySlotUI: Background component found for {gameObject.name}");
            }
        }
        else
        {
            Debug.LogWarning($"InventorySlotUI: 'Background' child bulunamadı! GameObject: {gameObject.name}");
        }
    }

    // Cursor management
    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovered = true;
        if (hoverCursor != null)
        {
            // Boyut ayarı varsa ölçeklenmiş cursor kullan
            if (cursorSize != Vector2.zero && (cursorSize.x != hoverCursor.width || cursorSize.y != hoverCursor.height))
            {
                var scaledCursor = ScaleCursor(hoverCursor, (int)cursorSize.x, (int)cursorSize.y);
                Cursor.SetCursor(scaledCursor, cursorHotspot, CursorMode.Auto);
            }
            else
            {
                Cursor.SetCursor(hoverCursor, cursorHotspot, CursorMode.Auto);
            }
        }
        UpdateHoverVisuals();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        UpdateHoverVisuals();
    }

    private void UpdateHoverVisuals()
    {
        if (slotBackground == null) return;
        
        // Slot'un dolu olup olmadığını kontrol et
        bool hasItem = itemIcon != null && itemIcon.enabled && itemIcon.sprite != null;
        
        Color targetColor = normalColor;
        if (hasItem) targetColor = occupiedColor;
        if (_isHovered) targetColor = hoverColor;
        
        slotBackground.color = targetColor;
    }

    private Texture2D ScaleCursor(Texture2D originalCursor, int newWidth, int newHeight)
    {
        if (originalCursor == null) return null;
        
        Texture2D readableTexture = MakeTextureReadable(originalCursor);
        if (readableTexture == null) return originalCursor;
        
        Texture2D scaledTexture = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
        
        for (int x = 0; x < newWidth; x++)
        {
            for (int y = 0; y < newHeight; y++)
            {
                float u = (float)x / newWidth;
                float v = (float)y / newHeight;
                Color pixel = readableTexture.GetPixelBilinear(u, v);
                scaledTexture.SetPixel(x, y, pixel);
            }
        }
        
        scaledTexture.Apply();
        
        if (readableTexture != originalCursor)
        {
            DestroyImmediate(readableTexture);
        }
        
        return scaledTexture;
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
            Texture2D readableTexture = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            readableTexture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            readableTexture.Apply();
            
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            
            return readableTexture;
        }
    }
}