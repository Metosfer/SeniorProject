using UnityEngine;
using UnityEngine.UI;

public class FishingUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject fishObject;
    public GameObject bobberObject;
    public Slider progressBar;
    public Text instructionText;
    public Text timerText;
    
    void Start()
    {
        if (instructionText != null)
            instructionText.text = "SPACE tuşuna basarak balığı takip et!";
    }
    
    public void UpdateTimer(float currentTime, float maxTime)
    {
        if (timerText != null)
        {
            timerText.text = $"Süre: {(maxTime - currentTime):F1}s";
        }
    }
    
    public void UpdateProgressBar(float progress, float maxProgress)
    {
        if (progressBar != null)
        {
            progressBar.value = progress;
            progressBar.maxValue = maxProgress;
        }
    }
}
