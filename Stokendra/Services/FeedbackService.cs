using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Stokendra;
using Stokendra.Infrastructure;
using Stokendra.Models;

namespace Stokendra.Services;

/// <summary>
/// Geri bildirimlerin ve hata günlüklerinin iletimini yöneten servis.
/// </summary>
public class FeedbackService : IFeedbackService
{
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(15) };

    public string TargetEmail
    {
        get
        {
            var settings = AppSettings.Load();
            return !string.IsNullOrWhiteSpace(settings.FeedbackEmail)
                ? settings.FeedbackEmail.Trim()
                : "tolgahanacar.dev@gmail.com";
        }
    }

    public async Task<(bool Success, string Message)> SendFeedbackAsync(FeedbackReport report)
    {
        try
        {
            var settings = AppSettings.Load();
            string fullReport = BuildFullReportText(report);

            // 1. Webhook veya Form API tanımlıysa HTTP POST ile doğrudan gönder
            string endpoint = !string.IsNullOrWhiteSpace(settings.FeedbackWebhookUrl)
                ? settings.FeedbackWebhookUrl.Trim()
                : "https://api.web3forms.com/submit";

            string accessKey = !string.IsNullOrWhiteSpace(settings.Web3FormsAccessKey)
                ? settings.Web3FormsAccessKey.Trim()
                : "";

            // Eğer özel webhook varsa veya web3forms anahtarı tanımlıysa
            if (!string.IsNullOrWhiteSpace(accessKey) || !string.IsNullOrWhiteSpace(settings.FeedbackWebhookUrl))
            {
                var payload = new
                {
                    access_key = accessKey,
                    subject = $"[Stokendra v{AppInfo.VersionString}] [{report.Type}] {report.Subject}",
                    from_name = string.IsNullOrWhiteSpace(report.UserEmail) ? "Stokendra Kullanıcısı" : report.UserEmail,
                    email = string.IsNullOrWhiteSpace(report.UserEmail) ? TargetEmail : report.UserEmail,
                    message = fullReport
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(endpoint, content);

                if (response.IsSuccessStatusCode)
                {
                    return (true, LocalizationManager.L("feedback_send_success"));
                }
            }

            // Doğrudan API yapılandırılmamışsa veya başarısız olursa istemciye mailto yönlendirmesi öner
            OpenDefaultMailClient(report);
            return (true, LocalizationManager.L("feedback_opened_in_mail_client"));
        }
        catch (Exception ex)
        {
            AppLogger.LogError("FeedbackService send error", ex);
            // Hata durumunda da kullanıcının emeği kaybolmasın diye mailto ile aç
            OpenDefaultMailClient(report);
            return (false, $"{LocalizationManager.L("feedback_send_failed")}: {ex.Message}");
        }
    }

    public void OpenDefaultMailClient(FeedbackReport report)
    {
        try
        {
            string email = TargetEmail;
            string subject = $"[Stokendra v{AppInfo.VersionString}] [{report.Type}] {report.Subject}";

            // Mailto gövdesi (Windows mailto max URL uzunluğu sınırı ~2000 karakterdir)
            var sb = new StringBuilder();
            sb.AppendLine($"--- STOKENDRA GERİ BİLDİRİM / HATA RAPORU ---");
            sb.AppendLine($"Tür: {report.Type}");
            sb.AppendLine($"Konu: {report.Subject}");
            if (!string.IsNullOrWhiteSpace(report.UserEmail))
                sb.AppendLine($"Gönderen: {report.UserEmail}");
            sb.AppendLine($"Tarih: {report.CreatedAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine($"[AÇIKLAMA]");
            sb.AppendLine(report.Message);
            sb.AppendLine();

            if (report.IncludeLogs)
            {
                sb.AppendLine($"[SİSTEM BİLGİSİ]");
                sb.AppendLine(report.SystemInfo);

                if (!string.IsNullOrWhiteSpace(report.RecentLogs))
                {
                    sb.AppendLine($"[SON HATA GÜNLÜĞÜ ÖZETİ]");
                    // Mailto boyutunu aşmamak için ilk 500 karakterini ekle
                    string truncatedLogs = report.RecentLogs.Length > 500
                        ? report.RecentLogs.Substring(0, 500) + "\r\n... [Kalan loglar kırpıldı]"
                        : report.RecentLogs;
                    sb.AppendLine(truncatedLogs);
                }
            }

            string body = sb.ToString();
            string mailtoUrl = $"mailto:{email}?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}";

            Process.Start(new ProcessStartInfo
            {
                FileName = mailtoUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppLogger.LogError("OpenDefaultMailClient failed", ex);
        }
    }

    public string BuildFullReportText(FeedbackReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("================================================================================");
        sb.AppendLine($"STOKENDRA KULLANICI GERİ BİLDİRİM RAPORU");
        sb.AppendLine($"Uygulama Sürümü: v{AppInfo.VersionString}");
        sb.AppendLine($"Tarih / Saat    : {report.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Bildirim Türü   : {report.Type}");
        sb.AppendLine($"Konu            : {report.Subject}");
        if (!string.IsNullOrWhiteSpace(report.UserEmail))
            sb.AppendLine($"İletişim        : {report.UserEmail}");
        sb.AppendLine("================================================================================");
        sb.AppendLine();
        sb.AppendLine("[KULLANICI MESAJI / AÇIKLAMA]");
        sb.AppendLine(report.Message);
        sb.AppendLine();

        if (report.IncludeLogs)
        {
            sb.AppendLine("================================================================================");
            sb.AppendLine("[SİSTEM & ÇALIŞMA ZAMANI BİLGİLERİ]");
            sb.AppendLine(report.SystemInfo);
            sb.AppendLine();

            sb.AppendLine("================================================================================");
            sb.AppendLine("[SON HATA GÜNLÜKLERİ (error.log)]");
            sb.AppendLine(string.IsNullOrWhiteSpace(report.RecentLogs) ? "Kayıtlı hata logu yok." : report.RecentLogs);
            sb.AppendLine("================================================================================");
        }

        return sb.ToString();
    }
}
