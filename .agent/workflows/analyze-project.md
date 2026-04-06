---
description: Stokendra projesini analiz etmek ve kod kalitesini değerlendirmek için kullanılan workflow
---

# Stokendra Proje Analiz Workflow'u

Bu workflow, Stokendra stok takip uygulamasının kapsamlı analizini yapmak için kullanılır.

## Proje Genel Bilgi
- **Framework**: .NET 8 Windows Forms
- **DB**: SQLite (Microsoft.Data.Sqlite 8.0.0)
- **Grafikler**: ScottPlot 5.1.57
- **Excel**: ClosedXML 0.105.0
- **Versiyon**: 3.9.4

## Analiz Adımları

### 1. Yapısal Analiz
// turbo
```
Proje kök dizinini listele: c:\Users\tolga.acar\Documents\GitHub\Stokendra
```
Kontrol edilecek ana dizinler:
- `StokTakip/` — Ana proje
- `StokTakip/Data/` — Veritabanı katmanı (Database.cs ~2000 satır)
- `StokTakip/Forms/` — 19 Form/Panel dosyası
- `StokTakip/Models/` — 5 POCO model (StokKarti, StokHareketi, ServisKaydi, Birim, Not)
- `StokTakip/Resources/` — Lokalizasyon JSON dosyaları (tr/en)
- `StokTakip.HardTests/` — Entegrasyon test projesi

### 2. Mimari Katman Analizi
Her katmanı sırayla incele:

1. **Entry Point**: `Program.cs` — Exception handling, DB initialization, login flow
2. **Veritabanı**: `Data/Database.cs` — Schema, migration (V1-V10), CRUD, auth, validation
3. **Modeller**: `Models/` — StokKarti, StokHareketi, ServisKaydi, Birim, Not
4. **UI**: `UIHelper.cs` — Renk paleti, font sistemi, custom kontroller (GlowCard, SectionPanel, StatCard)
5. **Formlar**: `Forms/` — MainForm (sidebar nav), DashboardPanel, RaporlarPanel, StokHareketPanel vb.
6. **Lokalizasyon**: `LocalizationManager.cs` + `Resources/lang_*.json`
7. **Yedekleme**: `BackupManager.cs` — SQLite copy, Excel export, SQL export, ZIP
8. **Güncelleme**: `UpdateChecker.cs` — GitHub API release check
9. **Ayarlar**: `AppSettings.cs` + `AppPaths.cs` — JSON config, path normalization

### 3. Güvenlik Denetimi
Kontrol edilecek alanlar:
- [ ] SQL injection koruması (parameterized queries)
- [ ] Password hashing (PBKDF2-SHA512, v3 scheme)
- [ ] Timing attack koruması (FixedTimeEquals)
- [ ] Input validation (TrimTo, NormalizeRequiredText)
- [ ] Brute force koruması (LoginForm _attempts)
- [ ] DB integrity (PRAGMA quick_check, FK constraints)
- [ ] Audit logging (AuditLog tablosu)

### 4. Performans Analizi
Kontrol edilecek alanlar:
- [ ] WAL mode aktif mi?
- [ ] Index stratejisi yeterli mi?
- [ ] Connection pooling
- [ ] DoubleBuffered kullanımı
- [ ] SuspendLayout/ResumeLayout
- [ ] Async operasyonlar

### 5. Kod Kalitesi
Kontrol edilecek alanlar:
- [ ] Magic string'ler → enum dönüşümü gerekli mi?
- [ ] Tek dosyada çok fazla sorumluluk (Database.cs)
- [ ] IDisposable implementasyonu doğru mu?
- [ ] Hardcoded string'ler (lokalizasyon dışında kalanlar)
- [ ] Error handling tutarlılığı
- [ ] XML documentation

### 6. Test Coverage
// turbo
```
dotnet test StokTakip.HardTests/StokTakip.HardTests.csproj
```
Mevcut testler:
1. Database path and config
2. Duplicate stock code rejected
3. Negative stock blocked
4. Deleting critical entry blocked
5. Parent cards reject movements
6. Legacy password upgraded
7. Password policy enforced
8. Concurrent writes stay consistent
9. Backup and SQL export
10. Bulk service insert

### 7. Rapor Oluşturma
Bulgular `walkthrough.md` artifact'ına yazılır. Şunları içermelidir:
- Mimari diyagram (mermaid)
- Dosya istatistikleri
- Kritik/orta/düşük öncelikli bulgular
- Güvenlik değerlendirmesi (0-10 puan)
- Performans tespitleri
- Eksik özellik listesi
- Aksiyon planı (sprint bazlı)

## Önemli Dosya Konumları

| Dosya | Konum | Açıklama |
|-------|-------|----------|
| Proje dosyası | `StokTakip/StokTakip.csproj` | .NET 8, WinForms |
| Veritabanı | `StokTakip/Data/Database.cs` | ~2000 satır, tek dosya |
| UI Sistemi | `StokTakip/UIHelper.cs` | Premium dark theme |
| Ana Form | `StokTakip/Forms/MainForm.cs` | Sidebar navigation |
| Dashboard | `StokTakip/Forms/DashboardPanel.cs` | İstatistik kartları, grafikler |
| Raporlar | `StokTakip/Forms/RaporlarPanel.cs` | Tüketim analizi, yazdırma |
| Hareketler | `StokTakip/Forms/StokHareketPanel.cs` | CRUD, import/export, pagination |
| Ayarlar | `StokTakip/Forms/AyarlarPanel.cs` | Dil, DB, yedekleme, şifre |
| TR Dil | `StokTakip/Resources/lang_tr.json` | 265+ çeviri anahtarı |
| EN Dil | `StokTakip/Resources/lang_en.json` | İngilizce çeviriler |
| Testler | `StokTakip.HardTests/Program.cs` | 10 entegrasyon testi |
| Installer | `build_installer.iss` | Inno Setup script |
