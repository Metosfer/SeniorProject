using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

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
    [Tooltip("Panel içindeki balığın Transform component'i (hareket için)")]
    public Transform fishTransform;
    [Tooltip("Panel içindeki oltanın Transform component'i (hareket için)")]
    public Transform bobberTransform;
    [Tooltip("Yakalama ilerlemesini gösteren Slider component'i")]
    public Slider fishingProgressBar;
    [Tooltip("Balığın görselini gösterecek Image component'i (balık ikonu için)")]
    public Image fishImage; // Balık ikonunu gösterecek Image component
    
    [Header("Fishing Settings")]
    [Tooltip("Balığın dikey hareket hızı (1-5 arası önerilir)")]
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
    
    private bool playerInRange = false;
    private bool isFishingActive = false;
    private float currentCatchTime = 0f;
    private float fishDirection = 1f;
    private float bobberVelocity = 0f; // Bobber'ın dikey hızı
    private RectTransform fishRect;
    private RectTransform bobberRect;
    private SCItem currentTargetFish; // Şu an yakalanacak olan balık
    private bool escapeKeyConsumed = false; // ESC tuşunun bu frame'de tüketilip tüketilmediği
    private float escapeKeyConsumedTime = 0f; // ESC tuşunun tüketildiği zaman
    
    void Start()
    {
        if (promptText != null)
            promptText.SetActive(false);
            
        if (fishingPanel != null)
            fishingPanel.SetActive(false);
            
        if (fishTransform != null)
            fishRect = fishTransform.GetComponent<RectTransform>();
            
        if (bobberTransform != null)
            bobberRect = bobberTransform.GetComponent<RectTransform>();
            
        if (fishingProgressBar != null)
        {
            fishingProgressBar.value = 0f;
            fishingProgressBar.maxValue = catchTimeRequired;
        }
        
        // Balık olasılıklarını kontrol et
        ValidateFishProbabilities();
        
        // Available fish listesinden fishItems ve probabilities dizilerini oluştur
        InitializeFishArraysFromList();
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
        
        if (isFishingActive)
        {
            UpdateFishingGame();
        }
    }
    
    void CheckPlayerDistance()
    {
        if (playerTransform == null) return;
        
        float distance = Vector3.Distance(transform.position, playerTransform.position);
        
        // Hysteresis sistemi: Giriş ve çıkış için farklı mesafeler
        if (!playerInRange && distance <= interactionRange)
        {
            playerInRange = true;
            ShowPrompt();
        }
        else if (playerInRange && distance > exitRange)
        {
            playerInRange = false;
            HidePrompt();
        }
    }
    
    void HandleInput()
    {
        if (playerInRange && Input.GetKeyDown(KeyCode.E) && !isFishingActive)
        {
            StartFishing();
        }
        
        if (isFishingActive && Input.GetKey(KeyCode.Space))
        {
            MoveBobberUp();
        }
        
        if (isFishingActive && Input.GetKeyDown(KeyCode.Escape))
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
            if (promptTextComponent != null)
                promptTextComponent.text = "Press E to Open Fishing";
                
            // 3D TextMeshPro için
            if (promptTextMeshPro != null)
                promptTextMeshPro.text = "Press E to Open Fishing";
        }
    }
    
    void HidePrompt()
    {
        if (promptText != null)
            promptText.SetActive(false);
    }
    
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
        
        // Random bir balık seç ve görselini değiştir
        currentTargetFish = GetRandomFishFromList();
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
    }
    
    void UpdateFishingGame()
    {
        MoveFish();
        UpdateBobberPhysics();
        CheckCatchProgress();
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
        
        if (fishingPanel != null)
            fishingPanel.SetActive(false);
            
        if (success)
        {
            // Yakalanan balık zaten currentTargetFish olarak belirlenmişti
            if (currentTargetFish != null)
            {
                // Balık yakalama animasyonu trigger'ını tetikle
                TriggerFishCaughtAnimation();
                
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
                Debug.Log("Balık tutuldu ama envantere eklenemedi!");
            }
        }
        else
        {
            Debug.Log("Balık kaçtı!");
            
            // Balık kaçma animasyonu trigger'ını tetikle
            TriggerFishEscapedAnimation();
        }
        
        if (playerInRange)
            ShowPrompt();
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
                    fishProbabilities[i] = 0.5f; // Varsayılan
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
}
