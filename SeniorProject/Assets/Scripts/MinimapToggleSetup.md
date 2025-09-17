# Minimap Toggle ve Music Mute Setup Rehberi

## Unity Editor'da Toggle Sistemleri Kurulumu

### 1. Minimap Canvas Hazırlama

#### MinimapCanvas GameObject'ini etiketle:
1. **Minimap Canvas'ını seç**
2. **Tag**: "MinimapCanvas" ekle veya ismi "MinimapCanvas" yap
3. **VEYA**: PauseMenuController'da **Minimap Canvas** field'ına direkt assign et (tavsiye edilen)

### 2. PauseMenu Settings Panel'ine Toggle'lar Ekleme

#### UI Hierarchy güncellemesi:
```
PauseMenuPanel
└── SettingsPanel
    ├── AudioSection
    │   ├── VolumeSlider
    │   ├── MusicVolumeSlider
    │   ├── SoundEffectsVolumeSlider
    │   └── MusicMuteToggle ← YENİ
    ├── GraphicsSection
    │   └── GraphicsDropdown
    └── MinimapSection
        └── MinimapToggle
```

#### Minimap Toggle Ayarları:
1. **SettingsPanel altında**: Sağ tık → UI → Toggle
2. **İsim**: "MinimapToggle"
3. **Text**: "Enable Minimap" / "Minimap'i Etkinleştir"
4. **Is On**: ✓ (default açık)

#### Music Mute Toggle Ayarları:
1. **SettingsPanel altında**: Sağ tık → UI → Toggle
2. **İsim**: "MusicMuteToggle"
3. **Text**: "Mute Music" / "Müziği Sustur"
4. **Is On**: ✓ (default susturulmuş - CHECKED)

### 3. PauseMenuController Script Ayarları

#### Inspector'da Atamalar:
1. **PauseMenuController** script'ini seç
2. **Minimap Toggle**: MinimapToggle'ı sürükle
3. **Music Mute Toggle**: MusicMuteToggle'ı sürükle
4. **Minimap Canvas**: MinimapCanvas GameObject'ini direkt sürükle (tavsiye edilen)

### 4. Toggle Görsel Ayarları

#### Minimap Toggle:
```
Toggle (Script)
├── Is On: ✓ (açık)
├── Toggle Transition: Color Tint
├── Normal Color: White
├── Highlighted Color: Light Gray
└── Checkmark Color: Green/Blue

Label Text: "Enable Minimap"
```

#### Music Mute Toggle:
```
Toggle (Script)
├── Is On: ✓ (muted - checked)
├── Toggle Transition: Color Tint
├── Normal Color: White
├── Highlighted Color: Light Gray
└── Checkmark Color: Red/Orange

Label Text: "Mute Music"
```

### 5. Test Senaryoları

#### Minimap Testi:
1. **Play Mode**'a gir
2. **Pause menüsünü aç** (ESC)
3. **Settings** sekmesine git
4. **Minimap Toggle**'ını tıkla
5. **Minimap Canvas**'ın görünür/gizli olduğunu kontrol et

#### Music Mute Testi:
1. **Music Mute Toggle**'ı tıkla
2. **Tema müziğinin** durduğunu/başladığını kontrol et
3. **SoundManager**'da `muteMusicForTesting` değişkeninin güncellendiğini kontrol et

#### Save System Testi:
1. **Her iki toggle**'ı değiştir
2. **Oyunu kaydet**
3. **Oyunu kapat/aç**
4. **Ayarların korunduğunu** kontrol et

### 6. Çalışma Mekanizması

#### Minimap Canvas Bulma Yöntemleri:
1. **Öncelik 1**: PauseMenuController'da assigned canvas
2. **Öncelik 2**: "MinimapCanvas" tag'i ile arama
3. **Öncelik 3**: "MinimapCanvas" ismi ile arama

#### Music Mute Sistemi:
1. **SettingsManager**: `musicMuted` ayarını yönetir
2. **SoundManager**: `muteMusicForTesting` field'ını günceller
3. **Real-time**: Anında müzik durur/başlar
4. **Persistent**: Ayar oyun boyunca korunur

### 7. Varsayılan Ayarlar

#### SettingsManager Default Values:
```csharp
public bool minimapEnabled = true;  // Minimap açık
public bool musicMuted = true;      // Müzik susturulmuş (MUTED)
```

#### PlayerPrefs Fallback:
```csharp
MinimapEnabled: 1 (açık)
MusicMuted: 1 (susturulmuş)
```

### 8. Debug ve Troubleshooting

#### Yaygın Sorunlar:
- **Minimap bulunamıyor**: Canvas assign edilmiş mi, tag doğru mu?
- **Müzik durmuyor**: SoundManager Instance var mı?
- **Toggle çalışmıyor**: Event listener'lar atanmış mı?
- **Ayar kaybolmuyor**: SettingsManager çalışıyor mu?

#### Debug Commands:
```csharp
// Minimap
Debug.Log($"Minimap Canvas: {minimapCanvas != null}");
Debug.Log($"Minimap Toggle: {minimapToggle.isOn}");
Debug.Log($"Settings: {SettingsManager.Instance.Current.minimapEnabled}");

// Music Mute
Debug.Log($"Music Mute Toggle: {musicMuteToggle.isOn}");
Debug.Log($"SoundManager Mute: {SoundManager.Instance.muteMusicForTesting}");
Debug.Log($"Settings: {SettingsManager.Instance.Current.musicMuted}");
```

### 9. SoundManager Entegrasyonu

#### SetMusicMuted Metodu:
```csharp
public void SetMusicMuted(bool muted)
{
    muteMusicForTesting = muted;
    // AudioSource volume'unu güncelle
    float actualVolume = muted ? 0f : musicVolume;
    musicAudioSource.volume = actualVolume;
}
```

#### SettingsManager ApplyAudio:
```csharp
SoundManager.Instance.SetMusicVolume(Current.musicVolume);
SoundManager.Instance.SetMusicMuted(Current.musicMuted);
```

### 10. UI Layout Önerisi

#### Settings Panel Yerleşimi:
```
┌─────────── Settings Panel ───────────┐
│ Audio Settings                       │
│ ├─ Master Volume    [████████▓]     │
│ ├─ Music Volume     [██▓▓▓▓▓▓▓]     │
│ ├─ SFX Volume       [███████▓▓]     │
│ └─ Mute Music       [✓] ← CHECKED   │
│                                      │
│ Graphics Settings                    │
│ └─ Quality          [High ▼]        │
│                                      │
│ UI Settings                          │
│ └─ Enable Minimap   [✓]             │
└──────────────────────────────────────┘
```

## Özet

✅ **SettingsManager** - `minimapEnabled` ve `musicMuted` field'ları  
✅ **PauseMenuController** - İki toggle desteği ve direkt canvas assign  
✅ **SoundManager** - `SetMusicMuted()` metodu  
✅ **Save/Load** - Kalıcı ayar koruması  
✅ **Real-time Sync** - Anında uygulama  
✅ **Fallback System** - PlayerPrefs desteği  

**Artık hem minimap'i hem de müzik susturmayı settings'ten kontrol edebilir, ayarlar oyun boyunca korunur!** 🗺️🔇⚙️