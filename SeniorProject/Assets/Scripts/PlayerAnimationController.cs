using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimationController : MonoBehaviour
{
    private Animator animator;
    private bool isSpuding = false;
    private bool _carryBucketCached = false;
    private bool _isDancingCached = false;
    private bool _checkedTakeItemParam = false;
    private bool _hasTakeItemParam = true; // assume true; will verify on first use
    private bool _checkedWateringParam = false;
    private bool _hasWateringParam = false;
    private string _wateringParamName = null; // cache the actual trigger name found
    private bool _checkedDancingParam = false;
    private bool _hasDancingParam = false;
    // TakeItem movement lock tracking
    private bool isTakingItem = false;
    private Coroutine _takeItemCo;
    [SerializeField] private float takeItemFallbackDuration = 0.8f; // used if state name not found
    // Dancing: rely on Animator transitions; no forced loop to avoid flicker
    [Header("Dancing")]
    [SerializeField] private string danceStateName = "CatDance"; // optional: state name for debug helpers

    // Animasyon parametre hash'leri (Performans için)
    private int speedHash;
    private int groundedHash;
    private int spudingTriggerHash;
    private int takeItemTriggerHash;
    private int isCarryBucketHash;
    private int isDancingHash;
    // Watering trigger uses string lookup (supports multiple possible names)

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
    isCarryBucketHash = Animator.StringToHash("isCarryBucket");
    isDancingHash = Animator.StringToHash("isDancing");
    }

    /// <summary>
    /// Hareket hızı animasyonunu günceller.
    /// </summary>
    public void UpdateSpeed(float currentSpeed, float maxSpeed, float smoothTime)
    {
    if (animator == null) return;
    if (_isDancingCached) return; // don't fight dancing state
        float normalizedSpeed = (maxSpeed > 0) ? currentSpeed / maxSpeed : 0f;
        animator.SetFloat(speedHash, normalizedSpeed, smoothTime, Time.deltaTime);
    }

    /// <summary>
    /// Yerde olma durumu animasyonunu günceller.
    /// </summary>
    public void UpdateGrounded(bool grounded)
    {
    if (animator == null) return;
    if (_isDancingCached) return; // avoid transitions while dancing
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
    // Block movement while TakeItem plays (similar to Spuding)
    if (_takeItemCo != null) StopCoroutine(_takeItemCo);
    isTakingItem = true;
    _takeItemCo = StartCoroutine(CheckTakeItemAnimation());
    }

    /// <summary>
    /// Alınan item'a göre doğru pickup animasyonunu tetikler.
    /// Balık (isFish=true) ise TakeItem, değilse Spuding oynatır.
    /// </summary>
    public void TriggerPickupForItem(SCItem item)
    {
        bool isFish = (item != null && item.isFish);
        TriggerPickup(isFish);
    }

    /// <summary>
    /// isFish parametresine göre pickup animasyonunu tetikler.
    /// true => TakeItem, false => Spuding.
    /// </summary>
    public void TriggerPickup(bool isFish)
    {
        if (isFish) TriggerTakeItem();
        else TriggerSpuding();
    }

    /// <summary>
    /// Watering animasyonunu tetikler (kova ile sulama anında). Parametre adını otomatik bulur: "Watering" | "WateringTrigger" | "Water".
    /// </summary>
    public void TriggerWatering()
    {
        if (animator == null) return;

        if (!_checkedWateringParam)
        {
            // Try common names in order
            string[] candidates = { "Watering", "WateringTrigger", "Water" };
            foreach (var name in candidates)
            {
                if (AnimatorHasParameter(name, AnimatorControllerParameterType.Trigger))
                {
                    _wateringParamName = name;
                    _hasWateringParam = true;
                    break;
                }
            }
            _checkedWateringParam = true;
            if (!_hasWateringParam)
            {
                Debug.LogWarning("Animator watering trigger not found. Add a Trigger named 'Watering' (or 'WateringTrigger'/'Water') and a transition to your watering animation.", this);
            }
        }

        if (_hasWateringParam && !string.IsNullOrEmpty(_wateringParamName))
        {
            animator.SetTrigger(_wateringParamName);
        }
    }

    /// <summary>
    /// Dans animasyonunu aç/kapatmak için Animator'daki 'isDancing' bool parametresini ayarlar.
    /// </summary>
    public void SetDancing(bool isDancing)
    {
        if (animator == null) return;
        if (!_checkedDancingParam)
        {
            _hasDancingParam = AnimatorHasParameter("isDancing", AnimatorControllerParameterType.Bool);
            _checkedDancingParam = true;
            if (!_hasDancingParam)
            {
                Debug.LogWarning("Animator 'isDancing' bool param missing. Please add a Bool named 'isDancing' and wire it to your dance state.", this);
            }
        }
        if (!_hasDancingParam) return;
        if (_isDancingCached == isDancing) return; // no change
        _isDancingCached = isDancing;
        animator.SetBool(isDancingHash, isDancing);
    }

    // No dance loop coroutine; animator state machine handles single-play or clip-looping.

    /// <summary>
    /// Kovanın taşınma durumuna göre taşıma (CatCarryBucket) animasyonunu aç/kapat.
    /// </summary>
    public void SetCarryBucket(bool isCarrying)
    {
        if (animator == null) return;
        if (_carryBucketCached == isCarrying) return; // no-op if unchanged
        _carryBucketCached = isCarrying;
        animator.SetBool(isCarryBucketHash, isCarrying);
    }

    private void Update()
    {
        // BucketManager.CurrentCarried durumunu izleyip animator bool’unu senkronla
    if (_isDancingCached) return; // keep animator stable while dancing
    bool shouldCarry = BucketManager.CurrentCarried != null;
        if (shouldCarry != _carryBucketCached)
        {
            SetCarryBucket(shouldCarry);
        }
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

    // TakeItem state tracking similar to Spuding but with a fallback timeout
    private System.Collections.IEnumerator CheckTakeItemAnimation()
    {
        // allow trigger to register
        yield return new WaitForEndOfFrame();
        // Try to wait until we enter CatTakeItem (with a small timeout)
        float t = 0f;
        bool entered = false;
        while (t < 0.6f)
        {
            if (IsInState("CatTakeItem")) { entered = true; break; }
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        if (entered)
        {
            // Wait while in CatTakeItem
            while (IsInState("CatTakeItem"))
            {
                yield return null;
            }
            isTakingItem = false;
        }
        else
        {
            // Fallback: clear after a fixed duration
            yield return new WaitForSeconds(takeItemFallbackDuration);
            isTakingItem = false;
        }
        _takeItemCo = null;
    }

    public bool IsTakingItem()
    {
        return isTakingItem;
    }

    public bool IsDancing()
    {
        return _isDancingCached;
    }
}