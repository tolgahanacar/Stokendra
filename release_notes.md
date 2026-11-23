## 🌟 Stokendra v7.4.6 — Mimari & Asenkron Yarış Durumu Zırhlandırmaları

Bu sürüm, kullanıcı arayüzü asenkron işlemleri, komut tetiklemeleri ve güncelleme servisi üzerinde derinlemesine tespit edilen uç durumları ve yarış durumlarını (race conditions) çözümler.

### 🛡️ Neler Değişti?

- 🏎️ **`AddMovementViewModel` Asenkron Yarış Durumu (Race Condition) Giderildi:**
  - Kart Detay ekranından "Hareketi Ekle" tıklandığında, asenkron `InitAsync()` arka planda tamamlandığında seçili kartın ezilerek ilk karta dönmesi engellendi. `preSelectedCard` mekanizması ile kullanıcının seçtiği kart referansı eksiksiz korunur.
- 🛡️ **`DepartmentsViewModel` Parametresiz Tetikleme Desteği:**
  - Departman silme ve düzenleme komutları menü/kısayol üzerinden parametresiz tetiklendiğinde listedeki seçili departman üzerinden güvenli fallback ile çalışması sağlandı.
- 👥 **`UsersViewModel` Seçili Kullanıcı Fallback Koruması:**
  - Kullanıcı silme, rol değiştirme ve şifre sıfırlama komutları parametresiz tetiklemelerde `SelectedUser` üzerinden hatasız çalıştırılır hale getirildi.
- 🌐 **`UpdateService` Ağ Hata Yalıtımı ve Yerelleştirme:**
  - Güncelleme kontrolü sırasında internet veya DNS kopukluğu durumlarında oluşan istisnalar izole edildi, ayarlar ekranında yerelleştirilmiş hata mesajı gösterimi sağlandı. Kurulum dosyası indirme sonrası taşıma işlemi `File.Move(..., overwrite: true)` ile atomik hale getirildi.
- 🧪 **99/99 Test Başarısı:**
  - Önceden seçili kartın korunması ve güncelleme hata durumlarını doğrulayan 3 yeni birim testi eklendi.
