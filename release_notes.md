## 🔬 Stokendra v7.4.8 — Derin Performans & Veritabanı İndeks Güncellemesi

Bu sürüm, arama filtreleme algoritmalarında bellek tahsislerini (GC baskısını) sıfıra indiren, DataGrid çoklu seçimlerini atomikleştiren ve SQLite B-Tree indeksleme stratejisini optimize eden derin mimari iyileştirmeler içerir.

### ⚡ Neler Değişti?

- 🔍 **`StockCard.MatchesSearch` ile Sıfır-Tahsisli Arama:**
  - Arama kutusuna her harf yazıldığında kart başına 3 adet yeni küçük-harf string nesnesi üretilmesi engellendi. Kartın kod, isim ve kategori bilgileri tek seferde önbelleğe alınarak sonraki tuş vuruşlarında $O(1)$ string tahsisi ile anında arama yapılması sağlandı.
- ⚡ **DataGrid Çoklu Seçimlerinde Atomik Güncelleme:**
  - Kartlar, Hareketler, Servisler ve Notlar ekranlarında çoklu seçim yapılırken N defa tetiklenen toolbar ve UI bildirimleri `ReplaceAll` ile tek bir atomik `Reset` olayına indirildi.
- 📑 **SQLite Yeni B-Tree İndeksleri (`IX_Movements_Date` & `IX_Movements_Type_Qty`):**
  - Tüm hareketlerin tarih aralığı filtrelemeleri $O(N)$ tam tablo taramasından $O(\log N)$ indeks aralık taramasına dönüştürüldü.
  - Gösterge Paneli toplam stok hesabı doğrudan indeks üzerinden karşılanacak şekilde zırhlandırıldı.
- 🎯 **`TodayMovements` İndeks Dostu Sorgu:**
  - `WHERE DATE(Date) = DATE('now', 'localtime')` ifadesi `WHERE Date >= date('now', 'localtime') AND Date < date('now', 'localtime', '+1 day')` olarak güncellenerek indeks seek kullanımına açıldı.
- 🚀 **SQLite Bağlantı Havuzu PRAGMA İyileştirmesi:**
  - `journal_mode = WAL` kontrolü her bağlantı açılışından kaldırılarak yalnızca DB ilklendirmesine alındı; havuzdan bağlantı alma süresi hafifletildi.
- 🧪 **103/103 Test Başarısı:**
  - Önbellekli arama eşleştirmesini ve Türkçe karakter duyarlılığını doğrulayan yeni birim testleri eklendi.
