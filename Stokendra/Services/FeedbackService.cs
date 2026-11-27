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
            var settings = AppServices.TryGetCurrent(out var app) ? app!.Settings : AppSettings.Load();
            return !string.IsNullOrWhiteSpace(settings.FeedbackEmail)
                ? settings.FeedbackEmail.Trim()
                : "2005.tolgahan@gmail.com";
        }
    }

    public async Task<(bool Success, string Message)> SendFeedbackAsync(FeedbackReport report)
    {
        try
        {
            var settings = AppServices.TryGetCurrent(out var app) ? app!.Settings : AppSettings.Load();
            string fullReport = BuildFullReportText(report);

            // 1. Webhook veya Form API tanımlıysa HTTP POST ile doğrudan gönder
            string endpoint = !string.IsNullOrWhiteSpace(settings.FeedbackWebhookUrl)
                ? settings.FeedbackWebhookUrl.Trim()
                : "https://api.web3forms.com/submit";

            string accessKey = !string.IsNullOrWhiteSpace(settings.Web3FormsAccessKey)
                ? settings.Web3FormsAccessKey.Trim()
                : "";

            if (!string.IsNullOrWhiteSpace(accessKey) || !string.IsNullOrWhiteSpace(settings.FeedbackWebhookUrl))
            {
                var payload = new
                {
                    access_key = accessKey,
                    subject = $"[Stokendra {AppInfo.VersionString}] [{report.Type}] {report.Subject}",
                    from_name = string.IsNullOrWhiteSpace(report.UserEmail) ? "Stokendra User" : report.UserEmail,
                    email = string.IsNullOrWhiteSpace(report.UserEmail) ? TargetEmail : report.UserEmail,
                    message = fullReport
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(endpoint, content);

                if (response.IsSuccessStatusCode)
                {
                    return (true, LocalizationManager.L("feedback_send_success"));
                }

                AppLogger.LogError($"Feedback HTTP submission returned {(int)response.StatusCode} ({response.ReasonPhrase})");
            }

            // Doğrudan API yapılandırılmamışsa veya HTTP başarısız olduysa e-posta istemcisine yönlendir
            if (TryOpenDefaultMailClient(report, out string? mailError))
            {
                return (true, LocalizationManager.L("feedback_opened_in_mail_client"));
            }

            return (false, $"{LocalizationManager.L("feedback_send_failed")}: {mailError}");
        }
        catch (Exception ex)
        {
            AppLogger.LogError("FeedbackService send error", ex);
            if (TryOpenDefaultMailClient(report, out _))
            {
                return (true, LocalizationManager.L("feedback_opened_in_mail_client"));
            }
            return (false, $"{LocalizationManager.L("feedback_send_failed")}: {ex.Message}");
        }
    }

    public void OpenDefaultMailClient(FeedbackReport report)
    {
        TryOpenDefaultMailClient(report, out _);
    }

    private bool TryOpenDefaultMailClient(FeedbackReport report, out string? error)
    {
        error = null;
        try
        {
            string email = TargetEmail;
            string subject = $"[Stokendra {AppInfo.VersionString}] [{report.Type}] {report.Subject}";
            string encodedSubject = Uri.EscapeDataString(subject);

            // Windows ShellExecute mailto: URI length limit (~2000 chars); progressively trim logs then message so launch never fails.
            const int MaxMailtoLength = 1900;
            string prefixHeader = $"[Stokendra {AppInfo.VersionString}] {report.Type}\nSubject: {report.Subject}\n";
            if (!string.IsNullOrWhiteSpace(report.UserEmail))
                prefixHeader += $"From: {report.UserEmail}\n";
            prefixHeader += $"Time: {report.CreatedAt:yyyy-MM-dd HH:mm:ss}\n\n{report.Message}\n";

            string sysSection = report.IncludeLogs && !string.IsNullOrWhiteSpace(report.SystemInfo)
                ? $"\n--- SYSTEM ---\n{report.SystemInfo}"
                : "";

            string logText = report.IncludeLogs ? (report.RecentLogs ?? "") : "";
            // Prefer the newest lines from error.log (tail), not oldest lines
            if (logText.Length > 600)
                logText = "...\n" + logText.Substring(logText.Length - 600);

            string BuildBody(string msgHeader, string sys, string logs)
            {
                if (string.IsNullOrWhiteSpace(logs)) return msgHeader + sys;
                return msgHeader + sys + "\n--- RECENT LOGS ---\n" + logs;
            }

            string body = BuildBody(prefixHeader, sysSection, logText);
            string mailtoUrl = $"mailto:{email}?subject={encodedSubject}&body={Uri.EscapeDataString(body)}";

            if (mailtoUrl.Length > MaxMailtoLength && !string.IsNullOrEmpty(logText))
            {
                // Binary trim logs to fit budget
                while (mailtoUrl.Length > MaxMailtoLength && logText.Length > 0)
                {
                    int cut = Math.Max(1, logText.Length / 2);
                    logText = logText.Length > cut ? logText.Substring(cut) : "";
                    body = BuildBody(prefixHeader, sysSection, string.IsNullOrEmpty(logText) ? "" : "..." + logText);
                    mailtoUrl = $"mailto:{email}?subject={encodedSubject}&body={Uri.EscapeDataString(body)}";
                }
            }

            if (mailtoUrl.Length > MaxMailtoLength)
            {
                // Still too long (very long user message): truncate body directly preserving valid encoding
                body = body.Substring(0, Math.Min(body.Length, 900));
                mailtoUrl = $"mailto:{email}?subject={encodedSubject}&body={Uri.EscapeDataString(body)}";
                while (mailtoUrl.Length > MaxMailtoLength && body.Length > 64)
                {
                    body = body.Substring(0, (int)(body.Length * 0.8));
                    mailtoUrl = $"mailto:{email}?subject={encodedSubject}&body={Uri.EscapeDataString(body + "\n...[truncated]")}";
                }
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = mailtoUrl,
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            AppLogger.LogError("OpenDefaultMailClient failed", ex);
            return false;
        }
    }

    public string BuildFullReportText(FeedbackReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("================================================================================");
        sb.AppendLine("STOKENDRA KULLANICI GERİ BİLDİRİM RAPORU");
        sb.AppendLine($"Uygulama Sürümü: {AppInfo.VersionString}");
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
            sb.AppendLine(string.IsNullOrWhiteSpace(report.RecentLogs) ? "No error logs recorded." : report.RecentLogs);
            sb.AppendLine("================================================================================");
        }

        return sb.ToString();
    }
}
