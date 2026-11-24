using System.Threading.Tasks;
using Stokendra.Models;

namespace Stokendra.Services;

/// <summary>
/// Geri bildirim, hata ve önerilerin geliştiriciye iletilmesini sağlayan servis arayüzü.
/// </summary>
public interface IFeedbackService
{
    /// <summary>
    /// Raporu e-posta / webhook servisi üzerinden asenkron olarak iletir.
    /// </summary>
    Task<(bool Success, string Message)> SendFeedbackAsync(FeedbackReport report);

    /// <summary>
    /// Varsayılan sistem e-posta istemcisini (Outlook, Mail vb.) önceden doldurulmuş şablonla açar.
    /// </summary>
    void OpenDefaultMailClient(FeedbackReport report);

    /// <summary>
    /// Raporun ve sistem loglarının tam metin kopyasını oluşturur.
    /// </summary>
    string BuildFullReportText(FeedbackReport report);

    /// <summary>
    /// Hedef bildirim e-posta adresini döner.
    /// </summary>
    string TargetEmail { get; }
}
