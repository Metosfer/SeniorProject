using UnityEngine;
using UnityEngine.EventSystems;

// Attach this to each bookmark object (UI Button/Image with raycast or 3D object with collider)
// Set the 'key' to the corresponding letter (e.g., "D").
// On click, it calls BookManager.OnBookmarkClicked(key).
public class BookmarkLink : MonoBehaviour, IPointerClickHandler
{
    public BookManager bookManager;
    [Tooltip("Hedef sayfanın anahtarı/harfi (örn: D)")] public string key;

    // UI click
    public void OnPointerClick(PointerEventData eventData)
    {
        Click();
    }

    // Button.onClick için rahat kullanım
    public void Invoke()
    {
        Click();
    }

    // World object click (needs collider + Camera with PhysicsRaycaster for UI; for 3D, regular OnMouseDown works)
    private void OnMouseDown()
    {
        Click();
    }

    private void Click()
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            Debug.LogWarning($"BookmarkLink key boş: {name}");
            return;
        }

        if (bookManager == null)
        {
            bookManager = FindObjectOfType<BookManager>();
        }

        if (bookManager != null)
        {
            bookManager.OnBookmarkClicked(key);
        }
        else
        {
            Debug.LogWarning("BookmarkLink: BookManager bulunamadı.");
        }
    }
}
