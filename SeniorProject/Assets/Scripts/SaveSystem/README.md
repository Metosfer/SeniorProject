# Gelişmiş Save Sistemi - Kullanım Kılavuzu

Bu save sistemi, oyunun tüm durumunu (player pozisyonu, inventory, sahnedeki objeler, panellerin durumu vb.) kaydeder ve geri yükler.

## Kurulum

### 1. Gerekli Dosyalar
Aşağıdaki dosyalar projenize eklenmiştir:
- `SaveSystem/GameSaveManager.cs` - Ana save sistem yöneticisi
- `SaveSystem/ISaveable.cs` - Save edilebilir objeler için interface
- `SaveSystem/SaveSystemManager.cs` - Save sistemini başlatan yönetici
- `SaveSystem/SaveSystemTester.cs` - Test için örnek script

### 2. Save Sistemini Aktifleştirme

#### Yöntem 1: SaveSystemManager ile (Önerilen)
1. Herhangi bir sahneye boş bir GameObject oluşturun
2. Bu objeye `SaveSystemManager` component'ini ekleyin
3. Settings'leri ayarlayın:
   - `Auto Save On Scene Change`: Sahne değişiminde otomatik save
   - `Load On Start`: Oyun başlangıçında otomatik load
   - `Auto Load Save Time`: Yüklenecek save'in zamanı

#### Yöntem 2: Manual Setup
1. Sahneye boş bir GameObject oluşturup `GameSaveManager` ekleyin
2. Sahneye başka bir GameObject oluşturup `WorldItemSpawner` ekleyin
3. Her iki objeyi de "Don't Destroy On Load" yapın

### 3. Mevcut Scriptlerin Güncellenmesi

Aşağıdaki scriptler save sistemi ile uyumlu hale getirilmiştir:
- `BookManager` - Panel durumları kaydedilir
- `FlaskManager` - Panel durumları kaydedilir  
- `DoorManager` - Kapı rotasyonları kaydedilir
- `PauseMenuController` - Yeni save sistemi kullanır
- `MainMenuController` - Yeni load sistemi kullanır
- `SceneManager` - Sahne değişiminde otomatik save

### 4. Kendi Scriptlerinizi Uyumlu Hale Getirme

Kendi scriptlerinizin de save edilmesini istiyorsanız:

```csharp
public class MyScript : MonoBehaviour, ISaveable
{
    public int myValue;
    public bool myBool;
    
    // Save edilecek verileri döndür
    public Dictionary<string, object> GetSaveData()
    {
        Dictionary<string, object> data = new Dictionary<string, object>();
        data["myValue"] = myValue;
        data["myBool"] = myBool;
        return data;
    }
    
    // Save edilen verileri yükle
    public void LoadSaveData(Dictionary<string, object> data)
    {
        if (data.ContainsKey("myValue"))
            myValue = (int)data["myValue"];
            
        if (data.ContainsKey("myBool"))
            myBool = (bool)data["myBool"];
    }
}
```

## Kullanım

### Save/Load İşlemleri

```csharp
// Save
GameSaveManager.Instance.SaveGame();

// Load (belirli bir save zamanı ile)
GameSaveManager.Instance.LoadGame("2025-07-30 15:30:45");

// Tüm save zamanlarını al
List<string> saveTimes = GameSaveManager.Instance.GetSaveTimes();

// Save sil
GameSaveManager.Instance.DeleteSave("2025-07-30 15:30:45");
```

### Test Sistemi

`SaveSystemTester` component'ini sahneye ekleyerek test edebilirsiniz:
- F5: Hızlı Save
- F9: Hızlı Load (en son save'i yükler)
- UI butonları otomatik oluşturulur

## Neleri Kaydeder?

### Otomatik Kaydedilenler
- **Player Pozisyonu ve Rotasyonu**: Tam olarak nerede kalmıştınız
- **Inventory Durumu**: Tüm item'lar ve miktarları
- **World Items**: Yere atılan/düşen tüm objeler
- **Plant'lar**: Sahnedeki bitki objeleri
- **Scene Objects**: ISaveable implement eden tüm objeler

### Panel Durumları
- BookManager panel durumu
- FlaskManager panel durumu  
- Diğer panellerin açık/kapalı durumu

### Kapı Durumları
- DoorManager kapı rotasyonları
- Interaktif objelerin durumları

## Önemli Notlar

### Item Management
- Sistem otomatik olarak Resources klasöründeki SCItem'ları bulur
- World item'lar WorldItemSpawner üzerinden restore edilir
- Item prefab'ları SCItem scriptlerinde tanımlanmalı

### Performance
- Save işlemi JSON formatında PlayerPrefs'e kaydedilir
- Maksimum 3 save slot desteklenir (ayarlanabilir)
- Büyük sahnelerde save/load işlemi birkaç saniye sürebilir

### Sahne Değişimleri
- `SaveSystemManager` kullanıyorsanız sahne değişimlerinde otomatik save
- Manuel save de `SceneManager` script'i güncellenmiştir

### Debug
- Console'da save/load işlemlerinin detayları görünür
- Hata durumlarında detaylı log mesajları

## Sorun Giderme

### "GameSaveManager bulunamadı" Hatası
- `SaveSystemManager` component'ini sahneye ekleyin
- Veya manuel olarak `GameSaveManager` objesini oluşturun

### Item'lar Load Edilmiyor
- SCItem'ların Resources klasöründe olduğundan emin olun
- Item name'lerin tutarlı olduğunu kontrol edin

### Panel Durumları Load Edilmiyor
- Manager scriptlerinin ISaveable implement ettiğinden emin olun
- Panel referanslarının doğru atandığını kontrol edin

### Performance Sorunları
- Save slot sayısını azaltın
- Çok fazla world item varsa temizlik yapın
- Büyük sahnelerde incremental save sistemi kullanın

## Gelecek Geliştirmeler

- Binary format save sistemi (daha hızlı)
- Cloud save desteği
- Incremental save (sadece değişenleri kaydet)
- Save file encryption
- Automatic backup sistem
