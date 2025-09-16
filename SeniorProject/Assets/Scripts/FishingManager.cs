using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FishingManager : MonoBehaviour
{
    [Header("UI Elements")]
    [Tooltip("Balık tutma mini oyunu panel'ı")]
    public GameObject fishingPanel;
    [Tooltip("Etkileşim metni gösterecek GameObject (Press E to Open Fishing)")]
    public GameObject promptText;
    [Tooltip("UI Text component referansı (Canvas üzerindeki text için)")]
    public Text promptTextComponent; // UI Text için
    [Tooltip("3D dünyada gösterilecek TextMeshPro component referansı")]
    public TMPro.TextMeshPro promptTextMeshPro; // 3D TextMeshPro için
    [Tooltip("Balık bekleme mesajı için TextMeshPro component'i (Waiting to catch fish..)")]
    public TMPro.TextMeshProUGUI waitingText; // Balık bekleme mesajı için
    [Tooltip("Panel içindeki balığın Transform component'i (hareket için)")]
    public Transform fishTransform;
    [Tooltip("Panel içindeki oltanın Transform component'i (hareket için)")]
    public Transform bobberTransform;
    [Tooltip("Yakalama ilerlemesini gösteren Slider component'i")]
    public Slider fishingProgressBar;
    [Tooltip("Balığın görselini gösterecek Image component'i (balık ikonu için)")]
    public Image fishImage; // Balık ikonunu gösterecek Image component
    [Tooltip("Oyun durumunu gösteren TextMeshPro component'i (Caught, Missed etc.)")]
    public TMPro.TextMeshProUGUI statusText; // Durum mesajları için
    
    [Header("Fishing Settings")]
    [Tooltip("Balığın dikey hareket hızı (1-5 arası önerilir)")]
    
        // Cached base transforms to avoid cumulative scaling/rotation across sessions
        private Vector3 fishImageBaseScale = Vector3.one;
        private Quaternion fishImageBaseRotation = Quaternion.identity;
        private Vector3 panelBaseScale = Vector3.one;
        private Quaternion panelBaseRotation = Quaternion.identity;
    public float fishSpeed = 2f;
    [Tooltip("Balığın oltadan kaçma kuvveti (Seçilen balığın zorluğuna göre otomatik ayarlanır)")]
    public float fishEscapeForce = 1f; // Balığın bobber'dan kaçma kuvveti (düşük tutuldu)
    [Tooltip("Space tuşu ile oltanın yukarı çıkma hızı (100-200 arası)")]
    public float bobberMoveSpeed = 150f; // Space ile yukarı çıkma hızı
    [Tooltip("Oltayı aşağı çeken yerçekimi kuvveti (50-150 arası)")]
    public float gravityForce = 100f; // Yerçekimi kuvveti
    [Tooltip("Balığı yakalamak için gereken süre (saniye cinsinden)")]
    public float catchTimeRequired = 3f;
    [Tooltip("Başarılı yakalama için olta-balık arası maksimum mesafe")]
    public float successZoneSize = 50f;
    [Tooltip("Bu süre boyunca balık yakalanamazsa oyun biter (saniye)")]
    public float maxFishingTime = 15f; // Maksimum balık tutma süresi
    
    [Header("Movement Ranges")]
    [Tooltip("Balığın panel içinde hareket edebileceği minimum Y pozisyonu")]
    public float fishMinY = -100f; // Balığın minimum Y pozisyonu
    [Tooltip("Balığın panel içinde hareket edebileceği maksimum Y pozisyonu")]
    public float fishMaxY = 100f;  // Balığın maksimum Y pozisyonu
    [Tooltip("Oltanın panel içinde inebileceği minimum Y pozisyonu")]
    public float bobberMinY = -100f; // Bobber'ın minimum Y pozisyonu
    [Tooltip("Oltanın panel içinde çıkabileceği maksimum Y pozisyonu")]
    public float bobberMaxY = 100f;  // Bobber'ın maksimum Y pozisyonu
    
    [Header("Player Detection")]
    [Tooltip("Oyuncunun balık tutma noktasına yaklaşma mesafesi (E tuşu aktif olur)")]
    public float interactionRange = 3f;
    [Tooltip("Oyuncunun uzaklaşma mesafesi (prompt gizlenir, flicker önlenir)")]
    public float exitRange = 4f; // Range'den çıkış mesafesi (flickering önlemek için)
    [Tooltip("Oyuncu karakterinin Transform component'i")]
    public Transform playerTransform;
    
    [Header("Fish Rewards")]
    [Tooltip("Yakalanabilecek balık türlerinin dizisi (otomatik oluşturulur)")]
    public SCItem[] fishItems; // Yakalanabilecek balık türleri
    [Tooltip("Her balığın yakalanma olasılığı (otomatik hesaplanır)")]
    public float[] fishProbabilities; // Her balığın yakalanma olasılığı (0-1 arası)
    
    [Header("Available Fish List")]
    [Tooltip("Bu listeye tüm balık ScriptableObject'lerini ekleyin (isFish=true olanlar)")]
    public List<SCItem> availableFishList = new List<SCItem>(); // Inspector'dan assign edilecek balık listesi
    
    // Inventory System Reference (bu referansı inspector'dan atayacaksınız)
    [Header("Inventory System")]
    [Tooltip("Oyuncunun envanter yönetici script'i (AddItem metodu olan MonoBehaviour)")]
    public MonoBehaviour inventoryManager; // Inventory manager referansı
    
    [Header("Animation")]
    [Tooltip("Balık yakalama animasyonu için Animator component'i")]
    public Animator fishingAnimator; // Balık yakalama animasyonu için
    
    [Header("Fish Spawn Settings")]
    [Tooltip("Yakalanan balığın dünyada spawn edileceği konum (Transform)")]
    public Transform fishSpawnPoint; // Balığın spawn edileceği nokta
    [Tooltip("Spawn edilirken balığa uygulanacak kuvvet miktarı (0-10 arası)")]
    public float spawnForce = 5f; // Spawn edilirken uygulanan kuvvet
    [Tooltip("Spawn edilirken balığa uygulanacak yukarı doğru kuvvet")]
    public float spawnUpwardForce = 2f; // Yukarı doğru kuvvet
    [Tooltip("Balık yakalandığında dünyada spawn edilsin mi?")]
    public bool spawnFishOnCatch = true; // Balık yakalandığında spawn edilsin mi?
    
    [Header("Waiting Spin (Slot Machine)")]
    [Tooltip("'Waiting to catch fish' aşamasında ikonlar dönsün mü?")]
    public bool enableWaitingSpin = true;
    [Tooltip("Dönüş başındaki ikon değiştirme aralığı (saniye)")]
    public float spinStartInterval = 0.08f;
    [Tooltip("Dönüş sonuna doğru ikon değiştirme aralığı (saniye)")]
    public float spinEndInterval = 0.25f;
    [Tooltip("Dönüş sırasında ikonun ölçek nabzı (0-0.5 önerilir)")]
    public float spinScalePulse = 0.1f;
    [Tooltip("Dönüş sırasında ikonun Z rotasyon salınımı (derece)")]
    public float spinRotationAmplitude = 12f;
    [Tooltip("Finalde dururken küçük bir vurgu skalası")]
    public float settleScalePunch = 0.15f;
    [Tooltip("Final vurgu süresi (saniye)")]
    public float settleDuration = 0.25f;

    [Header("Interaction Tuning")]
    [Tooltip("Oyuncu mesafesi kontrol sıklığı (saniye)")]
    public float distanceCheckInterval = 0.1f;
    [Tooltip("Menzilden çıkınca etkileşim için ek tolerans süresi (saniye)")]
    public float interactLinger = 0.3f;
    [Tooltip("HasFishFeed sonucu cache süresi (saniye)")]
    public float hasFeedCacheDuration = 0.1f;

    private bool playerInRange = false;
    private bool isFishingActive = false;
    private bool isWaitingForFish = false; // Balık bekleme durumu
    private float waitStartTime = 0f; // Bekleme başlangıç zamanı
    private float currentWaitTime = 0f; // Mevcut bekleme süresi
    private float currentCatchTime = 0f;
    private float fishDirection = 1f;
    private float bobberVelocity = 0f; // Bobber'ın dikey hızı
    private RectTransform fishRect;
    private RectTransform bobberRect;
    private SCItem currentTargetFish; // Şu an yakalanacak olan balık
    private bool escapeKeyConsumed = false; // ESC tuşunun bu frame'de tüketilip tüketilmediği
    private float escapeKeyConsumedTime = 0f; // ESC tuşunun tüketildiği zaman
    private float fishingStartTime = 0f; // Balık tutma başlangıç zamanı
    private string lastStatusMessage = ""; // Son durum mesajı (tekrar göstermemek için)
    private Coroutine waitingSpinCoroutine; // Waiting ikon döndürme coroutine'i
    private List<SCItem> waitingSpinCandidates = new List<SCItem>();
    private float lastDistanceCheckTime = -999f;
    private float lastTimeInRange = -999f;
    private bool cachedHasFeed = false;
    private float lastHasFeedCheckTime = -999f;
    
    void Start()
    {
        if (promptText != null)
            promptText.SetActive(false);
            
        if (fishingPanel != null)
            fishingPanel.SetActive(false);
            
        // Auto-find player transform if not assigned
        if (playerTransform == null)
        {
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null)
            {
                playerTransform = playerGO.transform;
                Debug.Log("FishingManager: Auto-assigned playerTransform via tag 'Player'.");
            }
            else
            {
                Debug.LogWarning("FishingManager: playerTransform is not set and no GameObject with tag 'Player' was found. Proximity detection will be disabled.");
            }
        }

        if (fishTransform != null)
            fishRect = fishTransform.GetComponent<RectTransform>();
            
        if (bobberTransform != null)
            bobberRect = bobberTransform.GetComponent<RectTransform>();
            
        if (fishingProgressBar != null)
        {
            fishingProgressBar.value = 0f;
            fishingProgressBar.maxValue = catchTimeRequired;
        }
        
        // Status text'i temizle
        if (statusText != null)
        {
            statusText.text = "";
        }
        
        // Balık olasılıklarını kontrol et
        ValidateFishProbabilities();
        
        // Available fish listesinden fishItems ve probabilities dizilerini oluştur
        InitializeFishArraysFromList();

        // Cache default transforms (scale/rotation) for UI elements to prevent drift
        if (fishImage != null)
        {
            var rt = fishImage.rectTransform;
            fishImageBaseScale = rt.localScale;
            fishImageBaseRotation = rt.localRotation;
        }
        if (fishingPanel != null)
        {
            panelBaseScale = fishingPanel.transform.localScale;
            panelBaseRotation = fishingPanel.transform.localRotation;
        }
    }

    void Update()
    {
        // ESC key consumed flag'ini kontrol et ve belirli bir süre sonra resetle
        if (escapeKeyConsumed && Time.time - escapeKeyConsumedTime > 0.1f)
        {
            escapeKeyConsumed = false;
        }
        
        CheckPlayerDistance();
        HandleInput();
        
        if (isWaitingForFish)
        {
            UpdateWaitingForFish();
        }
        else if (isFishingActive)
        {
            UpdateFishingGame();
        }
    }
    
    void CheckPlayerDistance()
    {
        if (playerTransform == null) return;
        if (Time.time - lastDistanceCheckTime < distanceCheckInterval) return;
        lastDistanceCheckTime = Time.time;

        float distance = Vector3.Distance(transform.position, playerTransform.position);

        bool wasInRange = playerInRange;
        // Hysteresis sistemi: Giriş ve çıkış için farklı mesafeler
        if (!playerInRange && distance <= interactionRange)
        {
            playerInRange = true;
            lastTimeInRange = Time.time;
            ShowPrompt();
        }
        else if (playerInRange && distance > exitRange)
        {
            playerInRange = false;
            HidePrompt();
        }
        else if (playerInRange)
        {
            // Menzil içindeyken zaman damgasını güncelle (linger penceresini taze tutar)
            lastTimeInRange = Time.time;
        }
    }
    
    void HandleInput()
    {
        bool canInteract = playerInRange || (Time.time - lastTimeInRange <= interactLinger);
        if (canInteract && Input.GetKeyDown(KeyCode.E) && !isFishingActive && !isWaitingForFish)
        {
            // Check if player has Fish Feed before allowing fishing
            bool hasFeed = GetHasFishFeedCached();
            Debug.Log($"FishingManager: E pressed. canInteract={canInteract}, inRange={playerInRange}, hasFeed={hasFeed}, isActive={isFishingActive}, isWaiting={isWaitingForFish}");
            if (hasFeed)
            {
                ConsumeFishFeed();
                StartWaitingForFish();
            }
            else
            {
                // Show message that Fish Feed is required
                UpdateStatusText("Need Fish Feed to fish!", Color.red);
                Debug.Log("Fish Feed required to start fishing!");
            }
        }
        
        if (isFishingActive && Input.GetKey(KeyCode.Space))
        {
            MoveBobberUp();
        }
        
        if ((isFishingActive || isWaitingForFish) && Input.GetKeyDown(KeyCode.Escape))
        {
            EndFishing(false);
            escapeKeyConsumed = true; // ESC tuşunu tüket
            escapeKeyConsumedTime = Time.time; // Tüketilme zamanını kaydet
            
            // Input event'ini tamamen durdurmak için bir coroutine başlat
            StartCoroutine(ConsumeEscapeInputForOneFrame());
        }
    }
    
    void ShowPrompt()
    {
        if (promptText != null)
        {
            promptText.SetActive(true);
            
            // UI Text için
            bool hasFeed = GetHasFishFeedCached();
            if (promptTextComponent != null)
                promptTextComponent.text = hasFeed ? "Press E to Fish" : "Need Fish Feed";
                
            // 3D TextMeshPro için
            if (promptTextMeshPro != null)
                promptTextMeshPro.text = hasFeed ? "Press E to Fish" : "Need Fish Feed";
        }
    }
    
    void HidePrompt()
    {
        if (promptText != null)
            promptText.SetActive(false);
    }
    
    void StartWaitingForFish()
    {
        isWaitingForFish = true;
        HidePrompt();
        
        // Rastgele bir balık seç (bekleme süresi hesaplamak için)
        currentTargetFish = GetRandomFishFromList();
        
        // Balığın zorluğuna göre bekleme süresi belirle (difficulty 1-5 arası)
        // Difficulty 1: 2 saniye, Difficulty 5: 6 saniye
        if (currentTargetFish != null && currentTargetFish.isFish)
        {
            currentWaitTime = 1f + currentTargetFish.fishDifficulty; // 2-6 saniye arası
        }
        else
        {
            currentWaitTime = 3f; // Varsayılan bekleme süresi
        }
        
        waitStartTime = Time.time;
        
        // Panel'i göster ve waiting text'i güncelle
        if (fishingPanel != null)
            fishingPanel.SetActive(true);
            
        // Reset UI transforms to their cached defaults when opening panel
        ResetFishingUITransform();

        if (waitingText != null)
            waitingText.text = "Waiting to catch fish..";

        // Waiting spin hazırlıkları
        if (enableWaitingSpin)
        {
            BuildWaitingSpinCandidates();
            StartWaitingVisualSpin();
        }
        
        Debug.Log($"Balık bekleniyor... Süre: {currentWaitTime} saniye (Balık: {currentTargetFish?.itemName}, Zorluk: {currentTargetFish?.fishDifficulty})");
    }
    
    void UpdateWaitingForFish()
    {
        float elapsedTime = Time.time - waitStartTime;
        
        if (elapsedTime >= currentWaitTime)
        {
            // Bekleme süresi doldu, balık tutmaya başla
            StartFishingAfterWait();
        }
    }
    
    void StartFishingAfterWait()
    {
        isWaitingForFish = false;
        isFishingActive = true;
        
        // Waiting text'i temizle
        if (waitingText != null)
            waitingText.text = "";

        // Spin'i final balıkta durdur
        if (enableWaitingSpin)
        {
            StopWaitingVisualSpin(true);
        }
        
        // Balık tutma başlama animasyonu trigger'ını tetikle
        TriggerFishingStartAnimation();
        
        ResetFishingGame();
    }

    // DEPRECATED: Bu method artık kullanılmıyor. Yeni sistem StartWaitingForFish() kullanıyor.
    void StartFishing()
    {
        isFishingActive = true;
        HidePrompt();
        
        // Balık tutma başlama animasyonu trigger'ını tetikle
        TriggerFishingStartAnimation();
        
        if (fishingPanel != null)
            fishingPanel.SetActive(true);
            
        ResetFishingGame();
    }
    
    void ResetFishingGame()
    {
        currentCatchTime = 0f;
        bobberVelocity = 0f;
        fishingStartTime = Time.time; // Balık tutma başlangıç zamanını kaydet
        lastStatusMessage = "";
        
        // Eğer zaten bir balık seçilmemişse (eski sistem için), random balık seç
        if (currentTargetFish == null)
        {
            currentTargetFish = GetRandomFishFromList();
        }
        
        // Seçilen balığın görselini güncelle
        UpdateFishVisual();
        
        // Seçilen balığın zorluğuna göre Fish Escape Force'u ayarla
        AdjustDifficultyBasedOnFish();
        
        if (fishRect != null)
            fishRect.anchoredPosition = new Vector2(fishRect.anchoredPosition.x, Random.Range(fishMinY, fishMaxY));
            
        if (bobberRect != null)
            bobberRect.anchoredPosition = new Vector2(bobberRect.anchoredPosition.x, 0f);
            
        if (fishingProgressBar != null)
            fishingProgressBar.value = 0f;
            
        fishDirection = Random.Range(0, 2) == 0 ? -1f : 1f;
        
        // Başlangıç status mesajı
        UpdateStatusText("Catch the Fish!", Color.white);
    }
    
    void UpdateFishingGame()
    {
        MoveFish();
        UpdateBobberPhysics();
        CheckCatchProgress();
        CheckFishingTimeout(); // Zaman aşımı kontrolü
        UpdateGameStatus(); // Durum mesajları
    }

    // -------- Waiting Spin Helpers --------
    private void BuildWaitingSpinCandidates()
    {
        waitingSpinCandidates.Clear();
        if (availableFishList != null)
        {
            for (int i = 0; i < availableFishList.Count; i++)
            {
                var it = availableFishList[i];
                if (it != null && it.isFish && it.itemIcon != null)
                    waitingSpinCandidates.Add(it);
            }
        }
        // Fallback: include currentTargetFish if list empty
        if (waitingSpinCandidates.Count == 0 && currentTargetFish != null && currentTargetFish.itemIcon != null)
        {
            waitingSpinCandidates.Add(currentTargetFish);
        }
    }

    private void StartWaitingVisualSpin()
    {
        if (waitingSpinCoroutine != null)
        {
            StopCoroutine(waitingSpinCoroutine);
            waitingSpinCoroutine = null;
        }
        waitingSpinCoroutine = StartCoroutine(WaitingSpinCoroutine());
    }

    private void StopWaitingVisualSpin(bool settleOnFinal)
    {
        if (waitingSpinCoroutine != null)
        {
            StopCoroutine(waitingSpinCoroutine);
            waitingSpinCoroutine = null;
        }
        // Restore base rotation/scale in case coroutine was interrupted mid-pulse
        if (fishImage != null)
        {
            fishImage.rectTransform.localRotation = fishImageBaseRotation;
            fishImage.rectTransform.localScale = fishImageBaseScale;
        }
        if (settleOnFinal)
        {
            // Set final sprite to currentTargetFish
            if (fishImage != null && currentTargetFish != null && currentTargetFish.itemIcon != null)
            {
                fishImage.sprite = currentTargetFish.itemIcon;
                // Small settle effect
                StartCoroutine(SettlePunchEffect());
            }
        }
    }

    private IEnumerator WaitingSpinCoroutine()
    {
        if (fishImage == null || waitingSpinCandidates.Count == 0)
            yield break;

        float startTime = Time.time;
        float duration = currentWaitTime > 0 ? currentWaitTime : 2f;
        float t = 0f;
        int idx = 0;
        // Use cached base transform to avoid drift across sessions
        float baseScale = fishImageBaseScale.x;
        Quaternion baseRot = fishImageBaseRotation;

        while (t < duration && isWaitingForFish)
        {
            float normalized = Mathf.Clamp01(t / duration);
            // Lerp interval from fast to slow
            float interval = Mathf.Lerp(spinStartInterval, spinEndInterval, normalized * normalized);

            // Choose next sprite
            if (waitingSpinCandidates.Count > 0)
            {
                idx = (idx + 1) % waitingSpinCandidates.Count;
                var cand = waitingSpinCandidates[idx];
                if (cand != null && cand.itemIcon != null)
                {
                    fishImage.sprite = cand.itemIcon;
                }
            }

            // Visual effects: scale pulse and slight rotation sway
            if (spinScalePulse > 0f)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 20f) * spinScalePulse;
                fishImage.rectTransform.localScale = new Vector3(baseScale * pulse, baseScale * pulse, 1f);
            }
            if (spinRotationAmplitude != 0f)
            {
                float angle = Mathf.Sin(Time.time * 6f) * spinRotationAmplitude;
                fishImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            // wait interval then advance time
            float endWait = Time.time + interval;
            while (Time.time < endWait)
            {
                yield return null;
            }
            t = Time.time - startTime;
        }

        // Restore rotation/scale and set final sprite to selected target
        fishImage.rectTransform.localRotation = baseRot;
        fishImage.rectTransform.localScale = new Vector3(baseScale, baseScale, 1f);
        if (currentTargetFish != null && currentTargetFish.itemIcon != null)
            fishImage.sprite = currentTargetFish.itemIcon;

        // optional settle punch
        yield return SettlePunchEffect();
    }

    private IEnumerator SettlePunchEffect()
    {
        if (fishImage == null || settleScalePunch <= 0f || settleDuration <= 0f)
            yield break;
        var rt = fishImage.rectTransform;
        Vector3 start = rt.localScale;
        Vector3 peak = start * (1f + settleScalePunch);
        float half = settleDuration * 0.5f;
        float t = 0f;
        // up
        while (t < half)
        {
            float k = t / half;
            rt.localScale = Vector3.Lerp(start, peak, k);
            t += Time.deltaTime;
            yield return null;
        }
        rt.localScale = peak;
        // down
        t = 0f;
        while (t < half)
        {
            float k = t / half;
            rt.localScale = Vector3.Lerp(peak, start, k);
            t += Time.deltaTime;
            yield return null;
        }
        rt.localScale = start;
    }
    
    void MoveFish()
    {
        if (fishRect == null || bobberRect == null) return;
        
        float currentY = fishRect.anchoredPosition.y;
        float bobberY = bobberRect.anchoredPosition.y;
        
        // Temel dikey hareket
        float basicMovement = fishDirection * fishSpeed * Time.deltaTime * 50f;
        
        // Bobber'dan kaçma hareketi (yumuşak)
        float distanceToBobber = bobberY - currentY;
        float escapeMovement = 0f;
        
        // Eğer bobber çok yakınsa, ondan uzaklaş
        if (Mathf.Abs(distanceToBobber) < successZoneSize * 2f)
        {
            // Bobber yukarıdaysa aşağı kaç, aşağıdaysa yukarı kaç
            escapeMovement = -Mathf.Sign(distanceToBobber) * fishEscapeForce * Time.deltaTime * 30f;
        }
        
        // Toplam hareket
        float newY = currentY + basicMovement + escapeMovement;
        
        // Balığın dikey sınırları kontrol et
        if (newY > fishMaxY || newY < fishMinY)
        {
            fishDirection *= -1f;
            newY = Mathf.Clamp(newY, fishMinY, fishMaxY);
        }
        
        // Son pozisyonu ayarla
        newY = Mathf.Clamp(newY, fishMinY, fishMaxY);
        fishRect.anchoredPosition = new Vector2(fishRect.anchoredPosition.x, newY);
    }
    
    void MoveBobberUp()
    {
        bobberVelocity = bobberMoveSpeed;
    }
    
    void UpdateBobberPhysics()
    {
        if (bobberRect == null) return;
        
        // Yerçekimi uygula
        bobberVelocity -= gravityForce * Time.deltaTime;
        
        // Bobber'ı hareket ettir
        float currentY = bobberRect.anchoredPosition.y;
        float newY = currentY + (bobberVelocity * Time.deltaTime);
        
        // Sınırları kontrol et
        newY = Mathf.Clamp(newY, bobberMinY, bobberMaxY);
        
        // Sınırlara çarptığında hızı sıfırla
        if (newY <= bobberMinY || newY >= bobberMaxY)
        {
            bobberVelocity = 0f;
        }
        
        bobberRect.anchoredPosition = new Vector2(bobberRect.anchoredPosition.x, newY);
    }
    
    void CheckCatchProgress()
    {
        if (fishRect == null || bobberRect == null) return;
        
        float distance = Mathf.Abs(fishRect.anchoredPosition.y - bobberRect.anchoredPosition.y);
        
        if (distance <= successZoneSize)
        {
            currentCatchTime += Time.deltaTime;
            
            if (fishingProgressBar != null)
                fishingProgressBar.value = currentCatchTime;
                
            if (currentCatchTime >= catchTimeRequired)
            {
                EndFishing(true);
            }
        }
        else
        {
            currentCatchTime = Mathf.Max(0, currentCatchTime - Time.deltaTime * 0.5f);
            
            if (fishingProgressBar != null)
                fishingProgressBar.value = currentCatchTime;
        }
    }
    
    void EndFishing(bool success)
    {
        isFishingActive = false;
        isWaitingForFish = false; // Bekleme durumunu da sıfırla
        
        // Stop waiting spin if any
        if (enableWaitingSpin)
        {
            StopWaitingVisualSpin(true);
        }

        // Ensure UI transforms are restored to defaults on close
        ResetFishingUITransform();

        // Waiting text'i temizle
        if (waitingText != null)
            waitingText.text = "";
        
        if (fishingPanel != null)
            fishingPanel.SetActive(false);
            
        if (success)
        {
            // Yakalanan balık zaten currentTargetFish olarak belirlenmişti
            if (currentTargetFish != null)
            {
                // Balık yakalama animasyonu trigger'ını tetikle
                TriggerFishCaughtAnimation();
                
                // Başarı mesajı göster
                UpdateStatusText("Fish Caught!", Color.green);
                
                // Balığı dünyaya spawn et
                if (spawnFishOnCatch)
                {
                    SpawnCaughtFish(currentTargetFish);
                }
                
                // Balığı enventere ekle
                AddFishToInventory(currentTargetFish);
                Debug.Log($"Balık tutuldu! {currentTargetFish.itemName} yakalandı!");
            }
            else
            {
                UpdateStatusText("Failed to add to inventory!", Color.red);
                Debug.Log("Balık tutuldu ama envantere eklenemedi!");
            }
        }
        else
        {
            // Başarısızlık mesajı göster
            UpdateStatusText("Fish Escaped!", Color.red);
            Debug.Log("Balık kaçtı!");
            
            // Balık kaçma animasyonu trigger'ını tetikle
            TriggerFishEscapedAnimation();
        }
        
        if (playerInRange)
            ShowPrompt();
    }

    // Reset UI transforms (scale/rotation) for panel and fish image to cached defaults
    private void ResetFishingUITransform()
    {
        if (fishingPanel != null)
        {
            var tr = fishingPanel.transform;
            tr.localScale = panelBaseScale;
            tr.localRotation = panelBaseRotation;
        }
        if (fishImage != null)
        {
            var rt = fishImage.rectTransform;
            rt.localScale = fishImageBaseScale;
            rt.localRotation = fishImageBaseRotation;
        }
    }
    
    void InitializeFishArraysFromList()
    {
        if (availableFishList == null || availableFishList.Count == 0)
        {
            Debug.LogWarning("Available Fish List boş! Balık listesini doldurun.");
            return;
        }
        
        // Sadece isFish = true olan itemları filtrele
        List<SCItem> filteredFish = new List<SCItem>();
        foreach (SCItem item in availableFishList)
        {
            if (item != null && item.isFish)
            {
                filteredFish.Add(item);
            }
        }
        
        if (filteredFish.Count == 0)
        {
            Debug.LogWarning("Available Fish List'te isFish = true olan item yok!");
            return;
        }
        
        // Dizileri oluştur
        fishItems = filteredFish.ToArray();
        fishProbabilities = new float[fishItems.Length];
        
        // Balık türlerine göre otomatik olasılık ata
        for (int i = 0; i < fishItems.Length; i++)
        {
            switch (fishItems[i].fishType.ToLower())
            {
                case "common":
                    fishProbabilities[i] = 0.6f;
                    break;
                case "rare":
                    fishProbabilities[i] = 0.3f;
                    break;
                case "epic":
                    fishProbabilities[i] = 0.08f;
                    break;
                case "legendary":
                    fishProbabilities[i] = 0.02f;
                    break;
                default:
                    // Bilinmeyen türler için küçük bir varsayılan olasılık ver
                    fishProbabilities[i] = 0.1f;
                    break;
            }
        }
        
        Debug.Log($"Balık sistemi hazırlandı: {fishItems.Length} farklı balık türü");
    }
    
    SCItem GetRandomFishFromList()
    {
        if (availableFishList == null || availableFishList.Count == 0)
        {
            Debug.LogWarning("Available Fish List boş!");
            return null;
        }
        
        // Sadece isFish = true olan itemları filtrele
        List<SCItem> validFish = new List<SCItem>();
        foreach (SCItem item in availableFishList)
        {
            if (item != null && item.isFish)
            {
                validFish.Add(item);
            }
        }
        
        if (validFish.Count == 0)
        {
            Debug.LogWarning("Geçerli balık bulunamadı!");
            return null;
        }
        
        // Random balık seç
        int randomIndex = Random.Range(0, validFish.Count);
        return validFish[randomIndex];
    }
    
    void UpdateFishVisual()
    {
        if (currentTargetFish == null || fishImage == null)
        {
            Debug.LogWarning("Current target fish veya fish image null!");
            return;
        }
        
        if (currentTargetFish.itemIcon != null)
        {
            fishImage.sprite = currentTargetFish.itemIcon;
            Debug.Log($"Balık görseli güncellendi: {currentTargetFish.itemName}");
        }
        else
        {
            Debug.LogWarning($"{currentTargetFish.itemName} balığının ikonu yok!");
        }
    }
    
    void AdjustDifficultyBasedOnFish()
    {
        if (currentTargetFish == null)
        {
            Debug.LogWarning("Current target fish null, zorluğu ayarlanamıyor!");
            return;
        }
        
        // Balığın zorluğuna göre Fish Escape Force'u ayarla
        switch (currentTargetFish.fishDifficulty)
        {
            case 1: // Çok Kolay
                fishEscapeForce = 0.2f;
                Debug.Log($"{currentTargetFish.itemName} - Zorluk: Çok Kolay (Escape Force: {fishEscapeForce})");
                break;
            case 2: // Kolay
                fishEscapeForce = 0.5f;
                Debug.Log($"{currentTargetFish.itemName} - Zorluk: Kolay (Escape Force: {fishEscapeForce})");
                break;
            case 3: // Orta
                fishEscapeForce = 1.0f;
                Debug.Log($"{currentTargetFish.itemName} - Zorluk: Orta (Escape Force: {fishEscapeForce})");
                break;
            case 4: // Zor
                fishEscapeForce = 1.5f;
                Debug.Log($"{currentTargetFish.itemName} - Zorluk: Zor (Escape Force: {fishEscapeForce})");
                break;
            case 5: // Çok Zor
                fishEscapeForce = 2.0f;
                Debug.Log($"{currentTargetFish.itemName} - Zorluk: Çok Zor (Escape Force: {fishEscapeForce})");
                break;
            default:
                fishEscapeForce = 1.0f; // Varsayılan orta zorluk
                Debug.LogWarning($"Geçersiz balık zorluğu: {currentTargetFish.fishDifficulty}, varsayılan kullanılıyor");
                break;
        }
    }
    
    void TriggerFishCaughtAnimation()
    {
        if (fishingAnimator != null)
        {
            // FishCaught trigger'ını tetikle
            fishingAnimator.SetTrigger("FishCaught");
            Debug.Log("FishCaught animasyon trigger'ı tetiklendi!");
        }
        else
        {
            Debug.LogWarning("Fishing Animator atanmamış! Animasyon tetiklenemiyor.");
        }
    }
    
    void TriggerFishingStartAnimation()
    {
        if (fishingAnimator != null)
        {
            // FishingStart trigger'ını tetikle
            fishingAnimator.SetTrigger("FishingStart");
            Debug.Log("FishingStart animasyon trigger'ı tetiklendi!");
        }
        else
        {
            Debug.LogWarning("Fishing Animator atanmamış! Animasyon tetiklenemiyor.");
        }
    }
    
    void TriggerFishEscapedAnimation()
    {
        if (fishingAnimator != null)
        {
            // FishEscaped trigger'ını tetikle
            fishingAnimator.SetTrigger("FishEscaped");
            Debug.Log("FishEscaped animasyon trigger'ı tetiklendi!");
        }
        else
        {
            Debug.LogWarning("Fishing Animator atanmamış! Animasyon tetiklenemiyor.");
        }
    }
    
    void UpdateStatusText(string message, Color textColor = default)
    {
        if (statusText != null && message != lastStatusMessage)
        {
            statusText.text = message;
            if (textColor != default(Color))
            {
                statusText.color = textColor;
            }
            lastStatusMessage = message;
            Debug.Log($"Status güncellendi: {message}");
        }
    }
    
    void CheckFishingTimeout()
    {
        float elapsedTime = Time.time - fishingStartTime;
        
        if (elapsedTime >= maxFishingTime)
        {
            // Zaman doldu, balığı kaçır
            UpdateStatusText("Time's Up! Fish Escaped!", Color.red);
            
            // 2 saniye bekle ve oyunu kapat (oyuncu mesajı okuyabilsin)
            Invoke("EndFishingTimeoutCallback", 2.0f);
        }
    }
    
    void EndFishingTimeoutCallback()
    {
        EndFishing(false);
    }
    
    void UpdateGameStatus()
    {
        if (fishRect == null || bobberRect == null) return;
        
        float distance = Mathf.Abs(fishRect.anchoredPosition.y - bobberRect.anchoredPosition.y);
        float catchProgress = currentCatchTime / catchTimeRequired;
        float timeRemaining = maxFishingTime - (Time.time - fishingStartTime);
        
        // Durum mesajlarını belirle
        if (distance <= successZoneSize)
        {
            if (catchProgress >= 0.8f)
            {
                UpdateStatusText("Almost There!", Color.green);
            }
            else if (catchProgress >= 0.5f)
            {
                UpdateStatusText("About to Catch!", new Color(0.5f, 1f, 0.5f)); // Light green
            }
            else
            {
                UpdateStatusText("Good! Keep Going!", Color.yellow);
            }
        }
        else
        {
            if (timeRemaining <= 3f)
            {
                UpdateStatusText("Time Running Out!", new Color(1f, 0.5f, 0f)); // Orange
            }
            else if (distance > successZoneSize * 3f)
            {
                UpdateStatusText("Too Far Away!", Color.red);
            }
            else
            {
                UpdateStatusText("Get Closer to Fish!", new Color(1f, 0.8f, 0f)); // Light orange
            }
        }
    }
    
    void ValidateFishProbabilities()
    {
        if (fishItems == null || fishItems.Length == 0)
        {
            Debug.LogWarning("Balık türleri atanmamış! FishingManager'da fishItems dizisini doldurmanız gerekiyor.");
            return;
        }
        
        if (fishProbabilities == null || fishProbabilities.Length != fishItems.Length)
        {
            Debug.LogWarning("Balık olasılıkları yanlış! fishProbabilities dizisi fishItems dizisi ile aynı boyutta olmalı.");
            return;
        }
        
        float totalProbability = 0f;
        for (int i = 0; i < fishProbabilities.Length; i++)
        {
            totalProbability += fishProbabilities[i];
        }
        
        if (totalProbability <= 0)
        {
            Debug.LogWarning("Toplam balık olasılığı 0 veya negatif! En az bir balığın olasılığı 0'dan büyük olmalı.");
        }
    }
    
    SCItem GetRandomFish()
    {
        if (fishItems == null || fishItems.Length == 0 || fishProbabilities == null)
            return null;
        
        float totalWeight = 0f;
        for (int i = 0; i < fishProbabilities.Length; i++)
        {
            totalWeight += fishProbabilities[i];
        }
        
        if (totalWeight <= 0)
            return null;
        
        float randomValue = Random.Range(0f, totalWeight);
        float currentWeight = 0f;
        
        for (int i = 0; i < fishItems.Length; i++)
        {
            currentWeight += fishProbabilities[i];
            if (randomValue <= currentWeight)
            {
                return fishItems[i];
            }
        }
        
        // Fallback - son balığı döndür
        return fishItems[fishItems.Length - 1];
    }
    
    void SpawnCaughtFish(SCItem fish)
    {
        if (fish == null)
        {
            Debug.LogError("Spawn edilecek balık null!");
            return;
        }
        
        // Spawn prefabını belirle (dropPrefab varsa onu kullan, yoksa itemPrefab)
        GameObject prefabToSpawn = fish.dropPrefab != null ? fish.dropPrefab : fish.itemPrefab;
        
        if (prefabToSpawn == null)
        {
            Debug.LogWarning($"{fish.itemName} balığının spawn prefabı yok!");
            return;
        }
        
        // Spawn pozisyonunu belirle
        Vector3 spawnPosition;
        if (fishSpawnPoint != null)
        {
            spawnPosition = fishSpawnPoint.position;
        }
        else
        {
            // Spawn point yoksa fishing manager'ın yanında spawn et
            spawnPosition = transform.position + Vector3.up * 1f + Vector3.forward * 1f;
        }
        
        // Balığı spawn et
        GameObject spawnedFish = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);
        
        // Rigidbody varsa kuvvet uygula
        Rigidbody rb = spawnedFish.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Random yön hesapla
            Vector3 randomDirection = new Vector3(
                Random.Range(-1f, 1f),
                spawnUpwardForce,
                Random.Range(-1f, 1f)
            ).normalized;
            
            // Kuvvet uygula
            rb.AddForce(randomDirection * spawnForce, ForceMode.Impulse);
            
            Debug.Log($"{fish.itemName} spawn edildi ve kuvvet uygulandı!");
        }
        else
        {
            Debug.Log($"{fish.itemName} spawn edildi (Rigidbody yok)!");
        }
    }
    
    void AddFishToInventory(SCItem fish)
    {
        if (fish == null)
        {
            Debug.LogError("Null balık enventere eklenemez!");
            return;
        }
        
        // Inventory Manager ile etkileşim
        if (inventoryManager != null)
        {
            // Reflection ile AddItem metodunu çağırmaya çalış
            var addItemMethod = inventoryManager.GetType().GetMethod("AddItem");
            if (addItemMethod != null)
            {
                // AddItem(SCItem item, int quantity) formatında çağır
                addItemMethod.Invoke(inventoryManager, new object[] { fish, 1 });
                Debug.Log($"{fish.itemName} enventere eklendi!");
            }
            else
            {
                Debug.LogWarning("Inventory Manager'da AddItem metodu bulunamadı!");
            }
        }
        else
        {
            Debug.LogWarning("Inventory Manager atanmamış!");
        }
    }
    
    void OnDrawGizmosSelected()
    {
        // Etkileşim alanını görselleştir (yeşil)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
        
        // Çıkış alanını görselleştir (kırmızı)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, exitRange);
    }
    
    // Public method - Diğer scriptler ESC tuşunun tüketilip tüketilmediğini kontrol edebilir
    public bool IsEscapeKeyConsumed()
    {
        return escapeKeyConsumed;
    }
    
    // Public method - Balık tutma oyunu aktif mi kontrol et
    public bool IsFishingGameActive()
    {
        return isFishingActive;
    }
    
    // ESC tuşunu bir frame boyunca tamamen tüketmek için coroutine
    private System.Collections.IEnumerator ConsumeEscapeInputForOneFrame()
    {
        // Bir frame bekle
        yield return null;
        
        // Bir frame daha bekle güvenlik için
        yield return null;
        
        Debug.Log("ESC tuşu input'u tamamen tüketildi.");
    }

    // Check if player has any Fish Feed in inventory (by scanning inventory slots)
    private bool HasFishFeed()
    {
        if (!TryGetPlayerInventory(out var inv)) return false;
        return FindFishFeedInInventory(inv) != null;
    }

    // Cached check to reduce per-frame cost and flicker
    private bool GetHasFishFeedCached()
    {
        if (Time.time - lastHasFeedCheckTime > hasFeedCacheDuration)
        {
            cachedHasFeed = HasFishFeed();
            lastHasFeedCheckTime = Time.time;
        }
        return cachedHasFeed;
    }

    // Consume one Fish Feed from the player's inventory
    private void ConsumeFishFeed()
    {
        if (!TryGetPlayerInventory(out var inv)) return;
        var feedItem = FindFishFeedInInventory(inv);
        if (feedItem != null)
        {
            inv.RemoveItem(feedItem, 1);
            Debug.Log("Fish Feed consumed for fishing!");
        }
        else
        {
            Debug.LogWarning("Tried to consume Fish Feed but none found in inventory.");
        }
    }

    // Helper: get player's inventory via InventoryManager
    private bool TryGetPlayerInventory(out SCInventory inventory)
    {
        inventory = null;
        // Primary: use assigned InventoryManager reference via reflection
        if (inventoryManager != null)
        {
            var getInventoryMethod = inventoryManager.GetType().GetMethod("GetPlayerInventory");
            if (getInventoryMethod != null)
            {
                inventory = (SCInventory)getInventoryMethod.Invoke(inventoryManager, null);
                if (inventory != null) return true;
            }
        }

        // Fallback 1: Try InventoryManager.Instance
        var invManagerType = typeof(InventoryManager);
        var instanceProp = invManagerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        var instance = instanceProp != null ? instanceProp.GetValue(null) as InventoryManager : null;
        if (instance != null)
        {
            inventory = instance.GetPlayerInventory();
            if (inventory != null) return true;
        }

        // Fallback 2: FindObjectOfType in scene
        var found = GameObject.FindObjectOfType<InventoryManager>();
        if (found != null)
        {
            inventory = found.GetPlayerInventory();
            if (inventory != null) return true;
        }

        // Fallback 3: Persistent inventory direct access
        inventory = SCInventory.GetPersistentInventory();
        return inventory != null;
    }

    // Find the concrete Fish Feed item reference that exists in inventory
    private SCItem FindFishFeedInInventory(SCInventory inv)
    {
        if (inv == null || inv.slots == null) return null;
        foreach (var slot in inv.slots)
        {
            if (slot != null && slot.item != null && slot.item.isFishFeed && slot.itemCount > 0)
            {
                return slot.item; // Use the exact asset reference present in inventory
            }
        }
        return null;
    }
}
