using System;

namespace Stokendra.Models;

/// <summary>
/// Kullanıcı geri bildirimi, hata ve öneri rapor modeli.
/// </summary>
public class FeedbackReport
{
    /// <summary>Bildirim türü: "Hata Bildirimi", "Öneri", "Soru / Destek"</summary>
    public string Type { get; set; } = "Hata Bildirimi";

    /// <summary>Bildirim başlığı / konusu.</summary>
    public string Subject { get; set; } = "";

    /// <summary>Kullanıcının geri dönüş için girdiği e-posta adresi (opsiyonel).</summary>
    public string UserEmail { get; set; } = "";

    /// <summary>Kullanıcının açıklaması / mesajı.</summary>
    public string Message { get; set; } = "";

    /// <summary>Sistem bilgileri ve hata loglarının rapora eklenip eklenmeyeceği.</summary>
    public bool IncludeLogs { get; set; } = true;

    /// <summary>İşletim sistemi, donanım mimarisi ve uygulama sürüm özeti.</summary>
    public string SystemInfo { get; set; } = "";

    /// <summary>Son oluşan hata günlükleri (error.log son 50 satır).</summary>
    public string RecentLogs { get; set; } = "";

    /// <summary>Raporun oluşturulma zamanı.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
