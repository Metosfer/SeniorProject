# Inventory System - Cross-Scene Setup Guide

Bu doküman, inventory sisteminizin sahneler arası çalışması için gerekli kurulumu açıklar.

## 📋 Gereksinimler

### 1. Ana Scene Setup (İlk Scene / Main Menu)
Ana sahnenize aşağıdaki component'leri ekleyin:

#### InventoryManager GameObject:
```
GameObject → Create Empty → "InventoryManager"
```
- `InventoryManager.cs` script'ini ekleyin
- Inspector'da `Player Inventory` field'ına mevcut ScriptableObject inventory'nizi atayın (opsiyonel)
- Bu GameObject otomatik olarak `DontDestroyOnLoad` olacak

#### SceneInventorySetup GameObject (ÖNEMLİ - İlk sahnede de gerekli):
```
GameObject → Create Empty → "SceneInventorySetup"
```
- `SceneInventorySetup.cs` script'ini ekleyin
- `Auto Setup Inventory UI` = true
- `Reset Inventory On Load` = false

**Not:** İlk sahnede item'ların görünmesi için SceneInventorySetup gereklidir!

### 2. Her Scene için Setup
Her yeni sahnede aşağıdaki adımları takip edin:

#### Canvas Setup:
```
1. Inventory Canvas'ını kopyalayın/yapıştırın
2. InventoryUIManager component'inin Inspector'da inventory field'ını BOŞ bırakın
3. SceneInventorySetup script'i için boş GameObject oluşturun
```

#### SceneInventorySetup GameObject:
```
GameObject → Create Empty → "SceneInventorySetup"
```
- `SceneInventorySetup.cs` script'ini ekleyin
- `Auto Setup Inventory UI` = true
- `Reset Inventory On Load` = false (sadece test için true yapın)

### 3. Inventory Component Setup
Eğer sahnede `Inventory` component'i kullanıyorsanız:
- Inspector'da `Player Inventory` field'ını BOŞ bırakın
- Script otomatik olarak persistent inventory'yi kullanacak

## 🚀 Nasıl Çalışır

### Persistent Inventory System:
1. `SCInventory.GetPersistentInventory()` sahneler arası kalıcı inventory oluşturur
2. Bu inventory Unity'nin ScriptableObject system'i sayesinde memory'de kalır
3. Her yeni sahnede UI'lar otomatik olarak bu persistent inventory'ye bağlanır

### Otomatik Setup:
1. `SceneInventorySetup` sahne başlangıcında çalışır
2. Tüm `InventoryUIManager`'ları bulur ve persistent inventory atar
3. Tüm `Inventory` component'lerini sync eder

## 🔧 Test Etme

### Console Log'larını kontrol edin:
```
"Persistent inventory reset for new play session" - Play mode başlangıcında sıfırlandı
"Merging inspector inventory to persistent inventory" - Inspector inventory kopyalanıyor
"Force merging inspector inventory to persistent inventory" - İlk sahne için zorunlu merge
"Final Inventory Slot X: ItemName" - Slot içerikleri
"UI Setup complete - SlotUIs: X, Inventory slots: Y" - UI kurulumu
"Calling initial UpdateUI" - İlk UI güncellemesi
"InventoryUIManager: Event connected on OnEnable" - Event bağlandı
"Re-connected to persistent inventory" - Inventory referansı yenilendi
"Forced UI update on panel open" - Panel açıldığında zorla güncelleme
"Delayed UI update completed" - Gecikmeli senkronizasyon tamamlandı
```

### Debug Adımları:
1. **Inventory İçeriği Kontrol:**
   - Console'da "Final Inventory Slot" mesajlarını arayın
   - Inspector'da ScriptableObject'inizde item'ların görünür olduğunu doğrulayın

2. **UI Bileşen Kontrol:**
   - Console'da "Icon component found" mesajlarını kontrol edin
   - Inventory slot'larında "Icon", "Count", "Background" child object'leri olmalı

3. **Panel Açma:**
   - I tuşuna bastığınızda inventory panel'i açılmalı
   - Console'da "UpdateUI called" mesajı görünmeli

### Manual Test:
1. İlk sahnede inventory'ye item ekleyin
2. Console'da inventory içeriğini kontrol edin  
3. I tuşuna basarak panel'i açın
4. Item'ların UI'da görünüp görünmediğini kontrol edin

## 🛠️ Troubleshooting

### UI'da Item'lar Görünmüyor veya Senkronizasyon Sorunları:
**Kontrol Listesi:**
1. **İlk Sahne**: SceneInventorySetup component'inin ana sahnede de olduğundan emin olun
2. Console'da "Persistent inventory reset for new play session" mesajını kontrol edin
3. Panel açma/kapama testleri: I tuşuna birkaç kez basarak test edin
4. Console'da "Re-connected to persistent inventory" mesajlarını arayın
5. SceneInventorySetup'da "Force UI Synchronization" = true olduğundan emin olun

**Senkronizasyon Sorunları (2+ sahne değişikliği sonrası):**
- Console'da "Event connected on OnEnable" mesajlarını kontrol edin
- "Delayed UI update completed" mesajlarını arayın
- Panel kapatıp açarak zorla senkronizasyon deneyin
- SceneInventorySetup'da "Force UI Synchronization" aktif olmalı

**Play Mode Test Modu:**
- Her play mode başlangıcında inventory otomatik sıfırlanır
- Console'da "Persistent inventory reset for new play session" görünmeli
- Sahneler arası geçişlerde item'lar korunur, sadece oyun başlangıcında sıfırlanır

**İlk Sahnede Item'lar Görünmüyor Sorunu:**
- Ana sahneye mutlaka `SceneInventorySetup` component'i ekleyin
- Console'da "Triggered inventory merge" mesajını kontrol edin
- Inspector'da inventory field'ını BOŞ bırakın (SceneInventorySetup otomatik merge edecek)

**Yaygın Sorunlar:**
- Inspector'da inventory field'ı boş bırakılmamış → BOŞ bırakın
- UI slot'larında child object'ler eksik → "Icon", "Count", "Background" ekleyin
- InventorySlotUI script'i slot'lara eklenmemiş → Her slot'a ekleyin
- Ana sahnede SceneInventorySetup yok → Mutlaka ekleyin
- SceneInventorySetup'da "Force UI Synchronization" = false → true yapın

### Item'lar korunmuyor:
- Console'da "Scene inventory setup completed" mesajını kontrol edin
- SceneInventorySetup component'inin tüm sahnelerde olduğundan emin olun

### UI çalışmıyor:
- InventoryUIManager'ın inventory field'ının boş olduğundan emin olun
- Console'da "Using persistent inventory" mesajını kontrol edin

### Drag&Drop çalışmıyor:
- Canvas ve UI setup'ının doğru kopyalandığından emin olun
- DragAndDropHandler component'lerinin mevcut olduğunu kontrol edin

## 📁 Dosya Yapısı

### Yeni Dosyalar:
- `InventoryManager.cs` - Sahneler arası inventory yönetimi
- `SceneInventorySetup.cs` - Her sahne için otomatik setup
- `InventoryPersistence.cs` - JSON tabanlı backup (opsiyonel)

### Güncellenen Dosyalar:
- `SCInventory.cs` - Persistent inventory support
- `InventoryUIManager.cs` - Otomatik persistent inventory binding
- `Inventory.cs` - Basitleştirilmiş setup
- `Plant.cs` & `WorldItem.cs` - Persistent inventory support

## ⚡ Hızlı Start

1. **Ana sahnenize `InventoryManager` ekleyin**
2. **Ana sahneye `SceneInventorySetup` ekleyin** (ÖNEMLİ!)
3. **Diğer sahnelere Inventory Canvas'ını kopyalayın**  
4. **Her sahneye `SceneInventorySetup` ekleyin**
5. **Tüm Inspector'lardaki inventory field'larını BOŞ bırakın**
6. **Test edin!**

**Kritik:** Ana sahnede SceneInventorySetup olmadan ilk başlangıçta item'lar görünmez!

Sistem artık sahneler arası otomatik olarak çalışacak. 🎉
