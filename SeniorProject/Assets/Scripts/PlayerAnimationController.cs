using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimationController : MonoBehaviour
{
    private Animator animator;
    private bool isSpuding = false;
    private bool _checkedTakeItemParam = false;
    private bool _hasTakeItemParam = true; // assume true; will verify on first use

    // Animasyon parametre hash'leri (Performans için)
    private int speedHash;
    private int groundedHash;
    private int spudingTriggerHash;
    private int takeItemTriggerHash;

    private void Awake()
    {
        animator = GetComponent<Animator>();

        if (animator == null)
        {
            Debug.LogError("Animator bileşeni bulunamadı!", this);
            return;
        }

        // Hash'leri başlat
        InitializeHashes();
    }

    /// <summary>
    /// Animator parametrelerinin hash değerlerini önbelleğe alır.
    /// </summary>
    private void InitializeHashes()
    {
        speedHash = Animator.StringToHash("Speed");
        groundedHash = Animator.StringToHash("Grounded");
        spudingTriggerHash = Animator.StringToHash("SpudingTrigger");
    takeItemTriggerHash = Animator.StringToHash("TakeItem");
    }

    /// <summary>
    /// Hareket hızı animasyonunu günceller.
    /// </summary>
    public void UpdateSpeed(float currentSpeed, float maxSpeed, float smoothTime)
    {
        if (animator == null) return;
        float normalizedSpeed = (maxSpeed > 0) ? currentSpeed / maxSpeed : 0f;
        animator.SetFloat(speedHash, normalizedSpeed, smoothTime, Time.deltaTime);
    }

    /// <summary>
    /// Yerde olma durumu animasyonunu günceller.
    /// </summary>
    public void UpdateGrounded(bool grounded)
    {
        if (animator == null) return;
        animator.SetBool(groundedHash, grounded);
    }

    // Jump animasyonu kaldırıldı: ilgili parametreler ve tetikleyiciler silindi.

    /// <summary>
    /// "Spuding" animasyonunu tetikler.
    /// </summary>
    public void TriggerSpuding()
    {
        if (animator == null || isSpuding) return;

        isSpuding = true;
        animator.SetTrigger(spudingTriggerHash);
        
        // Coroutine ile animasyon süresini takip et
        StartCoroutine(CheckSpudingAnimation());
    }

    /// <summary>
    /// "TakeItem" animasyonunu tetikler (bitki harici eşyaları yerden alma). Spuding oynarken tetiklenmez.
    /// </summary>
    public void TriggerTakeItem()
    {
        if (animator == null) return;
        if (isSpuding) return; // Spuding öncelikli
        // Validate parameter exists once to help diagnose missing Animator setup
        if (!_checkedTakeItemParam)
        {
            _hasTakeItemParam = AnimatorHasParameter("TakeItem", AnimatorControllerParameterType.Trigger);
            _checkedTakeItemParam = true;
            if (!_hasTakeItemParam)
            {
                Debug.LogWarning("Animator 'TakeItem' trigger param missing. Please add a Trigger named 'TakeItem' and transition to your pickup animation.", this);
            }
        }
        if (_hasTakeItemParam)
        {
            animator.SetTrigger(takeItemTriggerHash);
        }
    }

    // Note: Carry-bucket animation removed per request

    /// <summary>
    /// Spuding animasyonu durumunu sürekli kontrol eder
    /// </summary>
    private System.Collections.IEnumerator CheckSpudingAnimation()
    {
        // Önce animasyonun başlamasını bekle
        yield return new WaitForEndOfFrame();
        
        // Spuding state'ine geçmesini bekle
        while (!IsInState("CatSpuding"))
        {
            yield return null;
        }
        
        // Spuding state'inden çıkmasını bekle
        while (IsInState("CatSpuding"))
        {
            yield return null;
        }
        
        // Animasyon bittiğinde flag'i sıfırla
        OnSpudingAnimationEnd();
    }
    
    /// <summary>
    /// Belirtilen animasyon state'inde olup olmadığını kontrol eder
    /// </summary>
    private bool IsInState(string stateName)
    {
        if (animator == null) return false;
        
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.IsName(stateName);
    }

    /// <summary>
    /// "Spuding" animasyonu bittiğinde çağrılacak metod.
    /// Bu metodun Unity Animator penceresindeki animasyon klibine
    /// bir Animation Event olarak eklenmesi gerekir.
    /// </summary>
    public void OnSpudingAnimationEnd()
    {
        isSpuding = false;
    }

    /// <summary>
    /// Karakterin şu anda "Spuding" yapıp yapmadığını döndürür.
    /// </summary>
    public bool IsSpuding()
    {
        return isSpuding;
    }

    /// <summary>
    /// Animator'dan current state bilgisini al (debug için)
    /// </summary>
    public string GetCurrentStateName()
    {
        if (animator == null) return "No Animator";
        
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
    return stateInfo.IsName("CatSpuding") ? "CatSpuding" :
           stateInfo.IsName("MovementBlendTree") ? "MovementBlendTree" : "Unknown";
    }
    
    /// <summary>
    /// Debug için - konsola current state'i yazdır
    /// </summary>
    public void DebugCurrentState()
    {
        Debug.Log($"Current Animator State: {GetCurrentStateName()}, IsSpuding: {IsSpuding()}");
    }

    // Helpers
    private bool AnimatorHasParameter(string name, AnimatorControllerParameterType type)
    {
        if (animator == null) return false;
        foreach (var p in animator.parameters)
        {
            if (p.type == type && p.name == name) return true;
        }
        return false;
    }
}