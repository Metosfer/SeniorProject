using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimationController : MonoBehaviour
{
    private Animator animator;
    private bool isSpuding = false;

    // Animasyon parametre hash'leri (Performans için)
    private int speedHash;
    private int groundedHash;
    private int isJumpingHash;
    private int jumpTriggerHash;
    private int spudingTriggerHash;

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
        isJumpingHash = Animator.StringToHash("IsJumping");
        jumpTriggerHash = Animator.StringToHash("JumpTrigger");
        spudingTriggerHash = Animator.StringToHash("SpudingTrigger");
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

    /// <summary>
    /// Zıplama durumu animasyonunu günceller (Havada kalma durumu için bool).
    /// </summary>
    public void UpdateJumping(bool jumping)
    {
        if (animator == null) return;
        animator.SetBool(isJumpingHash, jumping);
    }

    /// <summary>
    /// Zıplama animasyonunu başlatmak için Trigger'ı ayarlar.
    /// </summary>
    public void TriggerJumpAnimation()
    {
        if (animator == null) return;
        animator.SetTrigger(jumpTriggerHash);
    }

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
               stateInfo.IsName("CatJump") ? "CatJump" : 
               stateInfo.IsName("MovementBlendTree") ? "MovementBlendTree" : "Unknown";
    }
    
    /// <summary>
    /// Debug için - konsola current state'i yazdır
    /// </summary>
    public void DebugCurrentState()
    {
        Debug.Log($"Current Animator State: {GetCurrentStateName()}, IsSpuding: {IsSpuding()}");
    }
}