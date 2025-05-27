using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;  
public class AudioWaveEditor : EditorWindow
{
    private AudioClip currentClip;
    private AudioSource previewSource;
    private float[] audioSamples;
    private Rect waveformRect;
    private bool isPlaying = false;
    private bool isPaused = false;

    // Trim ayarları
    private float trimStart = 0f;
    private float trimEnd = 1f;
    private int selectedStartSample = 0;
    private int selectedEndSample = 0;

    // Görsel ayarlar
    private Color waveformColor = Color.cyan;
    private Color selectionColor = new Color(1f, 1f, 0f, 0.3f);
    private float waveformHeight = 200f;
    private Vector2 scrollPosition;

    // Oynatma kontrolü
    private float playbackPosition = 0f;
    private System.DateTime lastPlayTime;

    [MenuItem("Window/Audio Wave Editor")]
    public static void ShowWindow()
    {
        AudioWaveEditor window = GetWindow<AudioWaveEditor>("Audio Wave Editor");
        window.Show();
    }

    void OnEnable()
    {
        // AudioSource oluştur (preview için)
        GameObject tempGO = new GameObject("AudioPreviewSource");
        tempGO.hideFlags = HideFlags.HideAndDontSave;
        previewSource = tempGO.AddComponent<AudioSource>();
        previewSource.playOnAwake = false;
        previewSource.loop = false;
    }

    void OnDisable()
    {
        StopPreview();
        if (previewSource != null)
        {
            DestroyImmediate(previewSource.gameObject);
        }
    }

    void OnGUI()
    {
        EditorGUILayout.Space(10);

        // Audio Clip seçimi
        EditorGUILayout.LabelField("Audio Wave Editor", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        AudioClip newClip = (AudioClip)EditorGUILayout.ObjectField("Audio Clip", currentClip, typeof(AudioClip), false);

        if (newClip != currentClip)
        {
            LoadAudioClip(newClip);
        }

        if (currentClip == null)
        {
            EditorGUILayout.HelpBox("Lütfen bir AudioClip seçin.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space(10);

        // Clip bilgileri
        EditorGUILayout.LabelField($"Clip: {currentClip.name}");
        EditorGUILayout.LabelField($"Length: {currentClip.length:F2}s");
        EditorGUILayout.LabelField($"Frequency: {currentClip.frequency}Hz");
        EditorGUILayout.LabelField($"Channels: {currentClip.channels}");

        EditorGUILayout.Space(10);

        // Trim kontrolları
        EditorGUILayout.LabelField("Trim Settings", EditorStyles.boldLabel);

        float newTrimStart = EditorGUILayout.Slider("Start Time", trimStart * currentClip.length, 0f, currentClip.length) / currentClip.length;
        float newTrimEnd = EditorGUILayout.Slider("End Time", trimEnd * currentClip.length, 0f, currentClip.length) / currentClip.length;

        if (newTrimStart != trimStart || newTrimEnd != trimEnd)
        {
            trimStart = Mathf.Clamp01(newTrimStart);
            trimEnd = Mathf.Clamp01(Mathf.Max(newTrimEnd, trimStart + 0.01f));
            UpdateTrimSelection();
            Repaint();
        }

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField($"Selected Duration: {(trimEnd - trimStart) * currentClip.length:F2}s");

        EditorGUILayout.Space(10);

        // Oynatma kontrolları
        EditorGUILayout.LabelField("Playback Controls", EditorStyles.boldLabel);

        GUILayout.BeginHorizontal();

        if (GUILayout.Button(isPlaying ? "Stop" : "Play Selected", GUILayout.Height(30)))
        {
            if (isPlaying)
            {
                StopPreview();
            }
            else
            {
                PlaySelectedRegion();
            }
        }

        if (GUILayout.Button(isPaused ? "Resume" : "Pause", GUILayout.Height(30)))
        {
            if (isPaused)
            {
                ResumePreview();
            }
            else
            {
                PausePreview();
            }
        }

        GUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // Waveform görsel ayarları
        EditorGUILayout.LabelField("Visual Settings", EditorStyles.boldLabel);
        waveformColor = EditorGUILayout.ColorField("Waveform Color", waveformColor);
        selectionColor = EditorGUILayout.ColorField("Selection Color", selectionColor);
        waveformHeight = EditorGUILayout.Slider("Waveform Height", waveformHeight, 100f, 400f);

        EditorGUILayout.Space(10);

        // Waveform çizimi
        DrawWaveform();

        EditorGUILayout.Space(10);

        // Export butonu
        if (GUILayout.Button("Export Selected Region as WAV", GUILayout.Height(35)))
        {
            ExportSelectedRegion();
        }

        // Sürekli güncelleme (oynatma sırasında)
        if (isPlaying && !isPaused)
        {
            Repaint();
        }
    }

    void LoadAudioClip(AudioClip clip)
    {
        StopPreview();
        currentClip = clip;

        if (clip != null)
        {
            // Audio verilerini yükle
            audioSamples = new float[clip.samples * clip.channels];
            clip.GetData(audioSamples, 0);

            // Trim ayarlarını sıfırla
            trimStart = 0f;
            trimEnd = 1f;
            UpdateTrimSelection();
        }
        else
        {
            audioSamples = null;
        }
    }

    void UpdateTrimSelection()
    {
        if (currentClip != null)
        {
            selectedStartSample = Mathf.RoundToInt(trimStart * currentClip.samples);
            selectedEndSample = Mathf.RoundToInt(trimEnd * currentClip.samples);
        }
    }

    void DrawWaveform()
    {
        if (audioSamples == null || currentClip == null) return;

        // Waveform çizim alanını ayarla
        waveformRect = GUILayoutUtility.GetRect(0, waveformHeight, GUILayout.ExpandWidth(true));

        if (Event.current.type == EventType.Repaint)
        {
            // Arka plan
            EditorGUI.DrawRect(waveformRect, new Color(0.1f, 0.1f, 0.1f));

            // Waveform çizimi
            DrawWaveformData();

            // Seçim alanını çiz
            DrawSelectionArea();

            // Oynatma pozisyonunu çiz
            if (isPlaying)
            {
                DrawPlaybackPosition();
            }
        }

        // Mouse etkileşimi
        HandleWaveformInput();
    }

    void DrawWaveformData()
    {
        if (audioSamples.Length == 0) return;

        int samplesPerPixel = Mathf.Max(1, audioSamples.Length / (int)waveformRect.width);
        float centerY = waveformRect.y + waveformRect.height * 0.5f;
        float amplitude = waveformRect.height * 0.4f;

        Color oldColor = GUI.color;
        GUI.color = waveformColor;

        for (int x = 0; x < (int)waveformRect.width; x++)
        {
            int sampleIndex = x * samplesPerPixel;
            if (sampleIndex >= audioSamples.Length) break;

            float maxSample = 0f;
            for (int i = 0; i < samplesPerPixel && sampleIndex + i < audioSamples.Length; i++)
            {
                float sample = Mathf.Abs(audioSamples[sampleIndex + i]);
                if (sample > maxSample) maxSample = sample;
            }

            float height = maxSample * amplitude;
            Rect lineRect = new Rect(waveformRect.x + x, centerY - height, 1, height * 2);
            EditorGUI.DrawRect(lineRect, waveformColor);
        }

        GUI.color = oldColor;
    }

    void DrawSelectionArea()
    {
        float startX = waveformRect.x + (trimStart * waveformRect.width);
        float endX = waveformRect.x + (trimEnd * waveformRect.width);
        float width = endX - startX;

        Rect selectionRect = new Rect(startX, waveformRect.y, width, waveformRect.height);
        EditorGUI.DrawRect(selectionRect, selectionColor);

        // Seçim sınırları
        EditorGUI.DrawRect(new Rect(startX - 1, waveformRect.y, 2, waveformRect.height), Color.yellow);
        EditorGUI.DrawRect(new Rect(endX - 1, waveformRect.y, 2, waveformRect.height), Color.yellow);
    }

    void DrawPlaybackPosition()
    {
        if (previewSource.isPlaying)
        {
            float playProgress = previewSource.time / (currentClip.length * (trimEnd - trimStart));
            float posX = waveformRect.x + (trimStart + playProgress * (trimEnd - trimStart)) * waveformRect.width;

            EditorGUI.DrawRect(new Rect(posX - 1, waveformRect.y, 2, waveformRect.height), Color.red);
        }
    }

    void HandleWaveformInput()
    {
        Event e = Event.current;

        if (waveformRect.Contains(e.mousePosition))
        {
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                float normalizedX = (e.mousePosition.x - waveformRect.x) / waveformRect.width;
                normalizedX = Mathf.Clamp01(normalizedX);

                // Shift basılıysa end point'i ayarla, değilse start point'i
                if (e.shift)
                {
                    trimEnd = normalizedX;
                    if (trimEnd < trimStart) trimEnd = trimStart + 0.01f;
                }
                else
                {
                    trimStart = normalizedX;
                    if (trimStart > trimEnd) trimStart = trimEnd - 0.01f;
                }

                trimStart = Mathf.Clamp01(trimStart);
                trimEnd = Mathf.Clamp01(trimEnd);

                UpdateTrimSelection();
                e.Use();
                Repaint();
            }

            if (e.type == EventType.MouseDrag && e.button == 0)
            {
                float normalizedX = (e.mousePosition.x - waveformRect.x) / waveformRect.width;
                normalizedX = Mathf.Clamp01(normalizedX);

                if (e.shift)
                {
                    trimEnd = normalizedX;
                    if (trimEnd < trimStart) trimEnd = trimStart + 0.01f;
                }
                else
                {
                    trimStart = normalizedX;
                    if (trimStart > trimEnd) trimStart = trimEnd - 0.01f;
                }

                trimStart = Mathf.Clamp01(trimStart);
                trimEnd = Mathf.Clamp01(trimEnd);

                UpdateTrimSelection();
                e.Use();
                Repaint();
            }
        }
    }

    void PlaySelectedRegion()
    {
        if (currentClip == null || previewSource == null) return;

        StopPreview();

        // Seçili bölgeyi oynat
        previewSource.clip = currentClip;
        previewSource.time = trimStart * currentClip.length;
        previewSource.Play();

        isPlaying = true;
        isPaused = false;

        // Belirli bir süre sonra durdur
        EditorApplication.update += UpdatePlayback;
    }

    void UpdatePlayback()
    {
        if (previewSource == null || !previewSource.isPlaying)
        {
            StopPreview();
            return;
        }

        // Seçili bölgenin sonuna geldiğinde durdur
        if (previewSource.time >= trimEnd * currentClip.length)
        {
            StopPreview();
        }
    }

    void PausePreview()
    {
        if (previewSource != null && previewSource.isPlaying)
        {
            previewSource.Pause();
            isPaused = true;
        }
    }

    void ResumePreview()
    {
        if (previewSource != null && isPaused)
        {
            previewSource.UnPause();
            isPaused = false;
        }
    }

    void StopPreview()
    {
        if (previewSource != null)
        {
            previewSource.Stop();
        }

        isPlaying = false;
        isPaused = false;
        EditorApplication.update -= UpdatePlayback;
    }

    void ExportSelectedRegion()
    {
        if (currentClip == null) return;

        string path = EditorUtility.SaveFilePanel("Export Audio", "", currentClip.name + "_trimmed", "wav");
        if (string.IsNullOrEmpty(path)) return;

        // Seçili bölgeyi yeni bir AudioClip olarak oluştur
        AudioClip trimmedClip = CreateTrimmedClip();
        if (trimmedClip != null)
        {
            SavWav.Save(path, trimmedClip);
            DestroyImmediate(trimmedClip);

            EditorUtility.DisplayDialog("Export Complete", $"Audio exported to:\n{path}", "OK");
        }
    }

    AudioClip CreateTrimmedClip()
    {
        if (currentClip == null) return null;

        int startSample = Mathf.RoundToInt(trimStart * currentClip.samples);
        int endSample = Mathf.RoundToInt(trimEnd * currentClip.samples);
        int sampleCount = endSample - startSample;

        if (sampleCount <= 0) return null;

        // Yeni AudioClip oluştur
        AudioClip trimmedClip = AudioClip.Create(
            currentClip.name + "_trimmed",
            sampleCount,
            currentClip.channels,
            currentClip.frequency,
            false
        );

        // Veriyi kopyala
        float[] trimmedData = new float[sampleCount * currentClip.channels];
        for (int i = 0; i < sampleCount; i++)
        {
            for (int channel = 0; channel < currentClip.channels; channel++)
            {
                int sourceIndex = (startSample + i) * currentClip.channels + channel;
                int destIndex = i * currentClip.channels + channel;

                if (sourceIndex < audioSamples.Length)
                {
                    trimmedData[destIndex] = audioSamples[sourceIndex];
                }
            }
        }

        trimmedClip.SetData(trimmedData, 0);
        return trimmedClip;
    }
}