using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Save sistemini test etmek için örnek script
/// Bu component'i sahneye ekleyerek save/load butonları oluşturabilirsiniz
/// </summary>
public class SaveSystemTester : MonoBehaviour
{
    [Header("UI References")]
    public Button saveButton;
    public Button loadButton;
    public Button clearSavesButton;
    
    private void Start()
    {
        SetupUI();
    }
    
    private void SetupUI()
    {
        // Eğer butonlar atanmamışsa otomatik oluştur
        if (saveButton == null || loadButton == null || clearSavesButton == null)
        {
            CreateTestUI();
        }
        
        // Button event'lerini bağla
        if (saveButton != null)
            saveButton.onClick.AddListener(TestSave);
            
        if (loadButton != null)
            loadButton.onClick.AddListener(TestLoad);
            
        if (clearSavesButton != null)
            clearSavesButton.onClick.AddListener(ClearAllSaves);
    }
    
    private void CreateTestUI()
    {
        // Canvas oluştur
        GameObject canvasGO = new GameObject("SaveTestCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();
        
        // Save Button
        if (saveButton == null)
        {
            GameObject saveButtonGO = CreateButton("Save Game", new Vector2(-200, -50));
            saveButtonGO.transform.SetParent(canvasGO.transform, false);
            saveButton = saveButtonGO.GetComponent<Button>();
        }
        
        // Load Button
        if (loadButton == null)
        {
            GameObject loadButtonGO = CreateButton("Load Game", new Vector2(0, -50));
            loadButtonGO.transform.SetParent(canvasGO.transform, false);
            loadButton = loadButtonGO.GetComponent<Button>();
        }
        
        // Clear Saves Button
        if (clearSavesButton == null)
        {
            GameObject clearButtonGO = CreateButton("Clear Saves", new Vector2(200, -50));
            clearButtonGO.transform.SetParent(canvasGO.transform, false);
            clearSavesButton = clearButtonGO.GetComponent<Button>();
        }
    }
    
    private GameObject CreateButton(string text, Vector2 position)
    {
        GameObject buttonGO = new GameObject("Button_" + text.Replace(" ", ""));
        
        // RectTransform
        RectTransform rectTransform = buttonGO.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(180, 40);
        rectTransform.anchoredPosition = position;
        
        // Image (Button background)
        Image image = buttonGO.AddComponent<Image>();
        image.color = Color.white;
        
        // Button component
        Button button = buttonGO.AddComponent<Button>();
        
        // Text child object
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(buttonGO.transform, false);
        
        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.sizeDelta = Vector2.zero;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        Text textComponent = textGO.AddComponent<Text>();
        textComponent.text = text;
        textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textComponent.fontSize = 14;
        textComponent.color = Color.black;
        textComponent.alignment = TextAnchor.MiddleCenter;
        
        return buttonGO;
    }
    
    public void TestSave()
    {
        if (GameSaveManager.Instance != null)
        {
            GameSaveManager.Instance.SaveGame();
            Debug.Log("Save test tamamlandı!");
        }
        else
        {
            Debug.LogError("GameSaveManager bulunamadı! SaveSystemManager'ı sahneye ekleyin.");
        }
    }
    
    public void TestLoad()
    {
        if (GameSaveManager.Instance != null)
        {
            var saveTimes = GameSaveManager.Instance.GetSaveTimes();
            if (saveTimes.Count > 0)
            {
                string latestSave = saveTimes[saveTimes.Count - 1];
                GameSaveManager.Instance.LoadGame(latestSave);
                Debug.Log($"Load test tamamlandı! Yüklenen save: {latestSave}");
            }
            else
            {
                Debug.Log("Yüklenecek save bulunamadı!");
            }
        }
        else
        {
            Debug.LogError("GameSaveManager bulunamadı!");
        }
    }
    
    public void ClearAllSaves()
    {
        if (GameSaveManager.Instance != null)
        {
            var saveTimes = GameSaveManager.Instance.GetSaveTimes();
            foreach (string saveTime in saveTimes)
            {
                GameSaveManager.Instance.DeleteSave(saveTime);
            }
            Debug.Log("Tüm save'ler temizlendi!");
        }
        else
        {
            Debug.LogError("GameSaveManager bulunamadı!");
        }
    }
    
    private void Update()
    {
        // Test için kısayol tuşları
    if (InputHelper.GetKeyDown(KeyCode.F5))
        {
            TestSave();
        }
        
    if (InputHelper.GetKeyDown(KeyCode.F9))
        {
            TestLoad();
        }
    }
}
