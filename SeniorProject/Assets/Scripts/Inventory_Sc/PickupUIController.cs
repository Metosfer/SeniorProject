using UnityEngine;
using TMPro;

public class PickupUIController : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI pickupText;
    public KeyCode pickupKey = KeyCode.E;
    
    [Header("Settings")]
    public string pickupMessage = "Press {0} to pickup";
    public float fadeSpeed = 2f;
    public Vector3 offset = Vector3.up * 2f;
    
    private Camera playerCamera;
    private CanvasGroup canvasGroup;

    private void Start()
    {
        playerCamera = Camera.main;
        if (playerCamera == null)
        {
            playerCamera = FindObjectOfType<Camera>();
        }
        
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        
        // Pickup text'i ayarla
        if (pickupText != null)
        {
            pickupText.text = string.Format(pickupMessage, pickupKey.ToString());
        }
        
        // Başlangıçta görünmez yap
        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    private void Update()
    {
        // Kamerayı takip et (World Space Canvas için)
        if (playerCamera != null)
        {
            transform.LookAt(transform.position + playerCamera.transform.rotation * Vector3.forward,
                           playerCamera.transform.rotation * Vector3.up);
        }
    }

    public void ShowPickupUI(bool show)
    {
        gameObject.SetActive(show);
        
        if (show)
        {
            StartCoroutine(FadeIn());
        }
        else
        {
            StartCoroutine(FadeOut());
        }
    }

    private System.Collections.IEnumerator FadeIn()
    {
        while (canvasGroup.alpha < 1f)
        {
            canvasGroup.alpha += fadeSpeed * Time.deltaTime;
            yield return null;
        }
        canvasGroup.alpha = 1f;
    }

    private System.Collections.IEnumerator FadeOut()
    {
        while (canvasGroup.alpha > 0f)
        {
            canvasGroup.alpha -= fadeSpeed * Time.deltaTime;
            yield return null;
        }
        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    public void UpdatePickupKey(KeyCode newKey)
    {
        pickupKey = newKey;
        if (pickupText != null)
        {
            pickupText.text = string.Format(pickupMessage, pickupKey.ToString());
        }
    }
}
