# Inventory Sürükle-Bırak Sistemi Kurulum Rehberi

## Oluşturulan Scriptler:
1. **DragAndDropHandler.cs** - Inventory slot'larının sürüklenmesini yönetir
2. **DropZone.cs** - Eşyaların bırakılabileceği alanları tanımlar
3. **WorldDropZone.cs** - Dünya drop zone'u (DropZone'dan türetilmiş)
4. **WorldItemSpawner.cs** - Dünyada eşya oluşturmayı yönetir
5. **WorldItem.cs** - Dünyada bulunan eşyaları temsil eder

## Unity'de Kurulum:

### 1. WorldItemSpawner Setup (ÖNEMLİ):
1. **Hierarchy'de boş bir GameObject oluşturun** ve ismini "WorldItemSpawner" yapın
2. Bu GameObject'e **WorldItemSpawner.cs** script'ini ekleyin
3. Inspector'da **Default World Item Prefab** field'ına istediğiniz prefab'ı sürükleyip bırakın
   - Bu prefab dünyada spawn olacak eşyanın görünümü olacak
   - Prefab'ın Collider, Rigidbody vb. bileşenleri olmasına gerek yok, script otomatik ekleyecek

### 2. UI Setup (Inventory Panel):
- Her inventory slot'ının aşağıdaki yapıya sahip olması gerekiyor:
  ```
  SlotUI (InventorySlotUI + DragAndDropHandler components)
  ├── Icon (Image component)
  └── Count (TextMeshProUGUI component)
  ```
- **DragAndDropHandler** otomatik olarak slot'lara eklenir

### 3. Canvas Setup:
- Inventory Panel'in Canvas'ında **GraphicRaycaster** component'i olmalı
- Canvas'ın **Render Mode**'u "Screen Space - Overlay" olmalı

### 4. EventSystem:
- Scene'de **EventSystem** GameObject'i bulunmalı
- EventSystem'de **Standalone Input Module** component'i olmalı

### 5. World Drop Zone Setup (Opsiyonel):
- Dünyada özel drop zone oluşturmak için:
  1. Boş GameObject oluştur
  2. **WorldDropZone** script'ini ekle
  3. **Collider** ekle (Trigger olarak ayarla)
  4. İsteğe bağlı olarak görsel gösterge ekle

### 6. Player Setup:
- Player GameObject'ine "Player" tag'i ekle
- Player'da **Inventory** script'i olmalı ve **playerInventory** atanmış olmalı

### 7. Camera Setup:
- Ana camera'nın tag'i "MainCamera" olmalı

### 8. WorldItem için Tag Setup:
- Project Settings → Tags and Layers'a gidin
- Tags kısmına "WorldItem" tag'ini ekleyin

## Script Atama Rehberi:

### WorldItemSpawner Script'i:
- **Nereye:** Hierarchy'de boş bir GameObject (örn: "Managers", "WorldItemSpawner")
- **Nasıl:** GameObject'e WorldItemSpawner.cs'yi component olarak ekle
- **Ayarlar:** Inspector'da Default World Item Prefab field'ına prefab ata

### Plant Script'i:
- **Nereye:** Sahnedeki Plant GameObject'lerine
- **Nasıl:** Plant GameObject'e Plant.cs component'ini ekle
- **Ayarlar:** 
  - Item field'ına SCItem ata
  - Pickup UI field'ına pickup UI prefab'ı ata (opsiyonel)
  - Pickup Range ve Pickup Key ayarlarını yapılandır

### WorldItem Script'i:
- **Nereye:** Prefab'lara (dünyada spawn olacak eşya prefab'larına)
- **Nasıl:** Eğer prefab'ta yoksa otomatik olarak eklenir
- **Ayarlar:** Genellikle otomatik ayarlanır

### WorldDropZone Script'i:
- **Nereye:** Özel drop zone'lar için GameObject'lere
- **Nasıl:** GameObject'e component olarak ekle + Collider (trigger) ekle
- **Ayarlar:** Ground Layer, colors vb.

### DragAndDropHandler Script'i:
- **Nereye:** Inventory slot UI'larına
- **Nasıl:** InventoryUIManager otomatik olarak ekler
- **Ayarlar:** Otomatik konfigure edilir

### PickupUIController Script'i (Opsiyonel):
**Bu script tamamen opsiyoneldir!** Pickup UI'larınızı daha güzel yapmak istiyorsanız kullanın.

#### **📋 DETAYLI KURULUM ADIMLARİ:**

##### **1. PickupUI Prefab'ı Oluşturun:**

**Adım 1:** Hierarchy'de sağ click → **UI → Canvas**
- Canvas ismi: "PickupUI_Canvas"
- **Render Mode'u "World Space"** olarak değiştirin (önemli!)
- **Canvas Scaler** component'ini silin (gerekli değil)

**Adım 2:** Canvas altında Text oluşturun
- Canvas'a sağ click → **UI → Text - TextMeshPro**
- Text ismi: "PickupText"
- Text: "Press E to pickup"
- Font size: 24 (veya istediğiniz boyut)
- Alignment: Center
- Color: Beyaz veya görünür bir renk

**Adım 3:** Canvas'a PickupUIController script'ini ekleyin
- Canvas'ı seçin
- Inspector'da **Add Component** → **PickupUIController**

**Adım 4:** Script ayarlarını yapın
- **Pickup Text:** PickupText'i sürükleyip bırakın
- **Pickup Key:** E (varsayılan)
- **Pickup Message:** "Press {0} to pickup" (varsayılan)
- **Fade Speed:** 2 (animasyon hızı)

**Adım 5:** Canvas boyutunu ayarlayın
- Canvas'ın **Width: 200, Height: 50** (veya istediğiniz boyut)
- **Scale: 0.01, 0.01, 0.01** (dünyada küçük görünmesi için)

**Adım 6:** Prefab olarak kaydedin
- Canvas'ı Project window'a sürükleyin
- İsim: "PickupUI_Prefab"

##### **2. Plant ve WorldItem'lara Atayın:**

**Plant'lara atama:**
1. Plant GameObject'inizi seçin
2. Inspector'da **Plant script'ini** bulun
3. **Pickup UI** field'ına PickupUI_Prefab'ını sürükleyin

**WorldItem'lara atama:**
- WorldItem'lar otomatik oluşturuluyor, prefab ataması gerek yok
- Ama eğer özel WorldItem prefab'ınız varsa ona da atayabilirsiniz

#### **🎮 NASIL ÇALIŞIR:**

1. **Plant'a yaklaştığınızda:**
   - PickupUI_Prefab instantiate olur
   - "Press E to pickup" yazısı belirir
   - Fade in animasyonu oynar

2. **Plant'tan uzaklaştığınızda:**
   - Fade out animasyonu oynar
   - UI kaybolur

3. **E tuşuna bastığınızda:**
   - Item toplanır
   - UI kaybolur

#### **💡 ALTERNATIF (Basit Yöntem):**
Eğer UI'ya ihtiyacınız yoksa:
- **Pickup UI field'larını boş bırakın**
- Sistem yine çalışır, sadece görsel UI olmaz
- Console'da "Press E to pickup" mesajları görürsünüz

#### **🔧 SORUN GİDERME:**
- **UI görünmüyor:** Canvas'ın "World Space" olduğunu kontrol edin
- **UI çok büyük:** Canvas Scale'ini küçültün (0.01, 0.01, 0.01)
- **Text görünmüyor:** TextMeshPro'nun Pickup Text field'ına atandığını kontrol edin
- **Animasyon yok:** Fade Speed'i artırın

## Özel Prefab Sistemi:

### 🌿 **İtem Toplama ve Atma Mantığı:**
- **Aloe bitkisini topladığınızda:** Tüm bitkiyi toplamış gibi görünür ama inventory'e yaprağı eklenir
- **Yaprağı attığınızda:** Sadece yaprak drop olur (bitkinin kendisi değil)

### 📦 **SCItem Ayarları:**
Her SCItem'da iki prefab field'ı var:
- **Item Prefab:** Dünyada bulunurken kullanılan (örn: Aloe bitkisi)
- **Drop Prefab:** Inventory'den atıldığında kullanılan (örn: Aloe yaprağı)

#### **Kurulum:**
1. SCItem asset'inizi açın (örn: Aloe item)
2. **Item Prefab:** Aloe bitkisi prefab'ını atayın
3. **Drop Prefab:** Aloe yaprağı prefab'ını atayın

### 🔧 **Stack Limit Sorunu Çözüldü:**
- Artık 4 stack limitine ulaştığında diğer boş slotları kullanır
- "Inventory full" mesajı sadece gerçekten tüm slotlar doluyken görünür

### 📦 **Tek Item Atma Sistemi:**
- Inventory'den item attığınızda sadece **1 tane** atılır
- Stack'ten **1 azalır**, tümü atılmaz
- Örnek: 3 yaprak varken 1 tane atarsanız, 2 yaprak kalır

### World Item Pickup:
- Dünyada bulunan eşyalara yaklaştığınızda "E" tuşu ile toplayabilirsiniz
- Eşyalar otomatik olarak inventory'e eklenir

### Visual Feedback:
- Drop zone'lar highlight olur
- Sürükleme sırasında slot şeffaflaşır
- Invalid drop'larda renk değişimi

## Özellikler:

### Sürükle-Bırak:
- Inventory slot'larını mouse ile sürükleyebilirsiniz ✅ **Mouse hizalaması düzeltildi**
- Canvas dışına bıraktığınızda eşya dünyada spawn olur ✅ **Özel drop prefab sistemi**
- Belirli drop zone'lara bırakabilirsiniz

### World Item Pickup:
- Dünyada bulunan eşyalara yaklaştığınızda "E" tuşu ile toplayabilirsiniz
- Eşyalar otomatik olarak inventory'e eklenir
- Stack limit problemi çözüldü ✅

### Visual Feedback:
- Drop zone'lar highlight olur
- Sürükleme sırasında slot şeffaflaşır
- Invalid drop'larda renk değişimi

## Nasıl Kullanılır:

1. **Inventory'yi açın** (I tuşu)
2. **Eşyayı sürükleyin** (Sol mouse tuşu ile tıklayıp tutun) - Mouse ile tam hizada hareket eder
3. **İstediğiniz yere bırakın**:
   - Canvas dışına bırakın → Dünyada spawn olur
   - Drop zone'a bırakın → Belirli alana spawn olur
4. **Dünyada bulunan eşyaları toplamak için**:
   - Plant'lara yaklaşın → **E tuşuna basın** (artık otomatik toplama yok)
   - World item'lara yaklaşın → **E tuşuna basın**
   - Her iki durumda da "Press E to pickup" mesajı görünür

## Güncellemeler:

### ✅ **E Tuşu ile Toplama Sistemi:**
- **Plant'lar artık otomatik toplanmıyor**
- Hem Plant'lar hem WorldItem'lar **E tuşu** ile toplanıyor
- Yaklaştığınızda pickup UI göstergesi çıkıyor
- Tutarlı kullanıcı deneyimi sağlıyor

## Özelleştirme:

### Drop Zone Ayarları:
- `acceptAllItems`: Tüm eşyaları kabul et
- `acceptedItemTypes`: Sadece belirli türleri kabul et
- `normalColor`, `highlightColor`, `invalidColor`: Renk ayarları

### World Item Ayarları:
- `pickupRange`: Toplama mesafesi
- `pickupKey`: Toplama tuşu
- Item türüne göre otomatik renklendirme

## Debug ve Sorun Giderme:

Eğer sistem çalışmıyorsa şu konuları kontrol edin:

1. **Console'da debug mesajları** var mı?
2. **EventSystem** scene'de mevcut mu?
3. **Canvas'ta GraphicRaycaster** var mı?
4. **InventoryUIManager'da inventory** atanmış mı?
5. **Player'da Inventory script** ve playerInventory atanmış mı?
6. **Slot'larda Icon ve Count child'ları** doğru isimde mi?

## İpuçları:

- Yeni eşya türleri için WorldItemSpawner'da renk ayarları ekleyebilirsiniz
- Drop zone'ları kendi ihtiyaçlarınıza göre özelleştirebilirsiniz
- World item prefab'ları oluşturup WorldItemSpawner'da kullanabilirsiniz
- Ses efektleri ve parçacık sistemleri eklenebilir
