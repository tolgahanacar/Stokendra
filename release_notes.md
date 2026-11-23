## 🚀 Stokendra v7.4.7 — Mimari & Akıcı Sekme Geçişi Performans Güncellemesi

Bu sürüm, çekirdek mimariyi koruyarak sekme geçişlerindeki takılmaları, çift layout çizimlerini ve ekran kırpışmalarını ortadan kaldıran 5 ana performans optimizasyonu içerir.

### ⚡ Öne Çıkan Geliştirmeler

- 🎞️ **`TransitioningContentControl` ile Akıcı Geçiş (120ms CrossFade):**
  - Sayfa geçişleri sert (harsh) anlık sıçramalar yerine donanım hızlandırmalı 120ms CrossFade efektiyle pürüzsüz ve akıcı hale getirildi.
- ⚡ **`ViewLocator` Derlenmiş IL Fabrika Önbelleği (Compiled Expression Factory):**
  - Her sekme geçişinde tekrarlanan string manipülasyonları, `Type.GetType` ve yavaş reflection `Activator.CreateInstance` yerine derlenmiş `Func<Control>` delegeleri önbelleğe alındı. Sayfa ve dialog inşaları nanosaniyeler mertebesine indirildi.
- 🔄 **`BulkObservableCollection.ReplaceAll` ile Sıfır Kırpışma:**
  - `Cards.Clear()` ve `Cards.AddRange(data)` kombinasyonunun neden olduğu çift Reset olayı yerine atomik `ReplaceAll` getirildi. DataGrid ve ListBox satırları tek bir render döngüsünde titreşimsiz güncellenir.
- 📊 **`DashboardView` Sıfır Kırpışma Arka Plan Yenilemesi:**
  - Gösterge paneline her tıklandığında istatistik kartlarının silinip "Yükleniyor..." gösterilmesi engellendi. Mevcut kartlar korunarak SQLite güncellemeleri yerinde (in-place) pürüzsüzce uygulanır.
- 🛡️ **Aktif Menü Çift Tıklama Koruması:**
  - Zaten açık olan sekmeye tekrar tıklandığında gereksiz veritabanı sorguları ve UI yenilemeleri engellendi.
- 🧪 **102/102 Test Başarısı:**
  - Test paketi %40 hızlanarak 1 dakikada 102/102 testle başarıyla tamamlandı.
