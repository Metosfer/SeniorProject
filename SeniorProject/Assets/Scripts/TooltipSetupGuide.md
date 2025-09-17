# Tooltip Sistemi Kurulum Rehberi

## Unity Editor'da Tooltip UI Oluşturma

### 1. Canvas Oluşturma (Zaten mevcutsa atla)
1. Scene'de sağ tık → UI → Canvas
2. Canvas ismi: "UI Canvas" 
3. Canvas Scaler: Scale With Screen Size
4. Reference Resolution: 1920x1080

### 2. Tooltip Panel Oluşturma
1. Canvas altında sağ tık → UI → Panel
2. Panel ismi: "TooltipPanel"
3. Anchor: sol alt köşe (0,0)
4. Position: (0,0,0)
5. Size: (200, 100) - başlangıç boyutu, dinamik olarak değişecek

### 3. Background Image Ayarları
1. TooltipPanel'in Image componenti:
   - Source Image: UI/Skin/Background (veya istediğin sprite)
   - Image Type: Sliced (kenarlık için)
   - Color: (0.05, 0.05, 0.05, 0.95) - koyu gri, şeffaf

### 4. Text Komponenti
1. TooltipPanel altında sağ tık → UI → Text - TextMeshPro
2. Text ismi: "TooltipText"
3. Anchor: stretch (sol, sağ, üst, alt kenarları doldur)
4. Left/Right/Top/Bottom: 12, 12, 8, 8 (padding)
5. TextMeshPro Settings:
   - Font Size: 14
   - Color: White
   - Alignment: Top Left
   - Overflow: Overflow
   - Rich Text: ✓ (HTML tag desteği için)

### 5. Canvas Group Ekleme
1. TooltipPanel'i seç
2. Add Component → Canvas Group
3. Alpha: 0 (başlangıçta görünmez)
4. Interactable: ✗ (tooltip tıklanamaz)
5. Blocks Raycasts: ✗ (mouse event'leri geçirir)

### 6. TooltipManager Script Ekleme
1. Canvas'a TooltipManager script'ini ekle
2. Script alanlarını doldur:
   - Tooltip Panel: TooltipPanel'i sürükle
   - Tooltip Text: TooltipText'i sürükle  
   - Tooltip Background: TooltipPanel'in Image'ini sürükle

### 7. Layer Order Ayarı
1. Canvas'ın Sorting Layer: "UI" 
2. Order in Layer: 100 (en üstte görünmesi için)

### 8. InventorySlotUI Ayarları
1. Her inventory slot'unda InventorySlotUI script'inde:
2. Enable Tooltip: ✓ 

## Test Etme
1. Play Mode'a gir
2. Envanterde bir item'in üzerine mouse'u getir
3. 0.3 saniye sonra tooltip görünmeli
4. Mouse'u hareket ettir → tooltip takip etmeli
5. Mouse'u item'dan çıkar → tooltip kaybolmalı

## Görsel İyileştirmeler (Opsiyonel)
- TooltipPanel'e Outline effect ekle
- Gradient background
- Shadow effect
- Animation easing

## Sorun Giderme
- Tooltip görünmüyor → TooltipManager Instance null kontrolü
- Text görünmüyor → TextMeshPro font asset atanmış mı?
- Pozisyon yanlış → Canvas coordinates kontrolü
- Performance → Tooltip update frequency azalt