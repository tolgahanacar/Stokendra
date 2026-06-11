using static Stokendra.LocalizationManager;

namespace Stokendra.Infrastructure;

/// <summary>
/// Avalonia ve UI async void event handler'larında güvenli exception yönetimi sağlar.
/// async void metodlarda fırlatılan exception'lar sessizce yutulur;
/// bu helper merkezi hata yakalama ve loglama sağlar.
/// </summary>
public static class AsyncHelper
{
    /// <summary>
    /// Async bir işlemi güvenli şekilde çalıştırır.
    /// Exception'lar loglanır ve kullanıcıya gösterilir.
    /// </summary>
    /// <param name="action">Çalıştırılacak async işlem.</param>
    /// <param name="onError">Opsiyonel özel hata işleyici. Null ise varsayılan MessageBox gösterilir.</param>
    public static async void RunSafe(Func<Task> action, Action<Exception>? onError = null)
    {
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            // İptal beklenen bir durum — sessizce geç
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Async UI error", ex);

            if (onError != null)
            {
                onError(ex);
            }
            else
            {
                // In Avalonia, we should use a custom dialog service. 
                // For now, we just log it (already logged above).
            }
        }
    }

    /// <summary>
    /// Async bir işlemi güvenli şekilde çalıştırır ve tamamlanmasını bekler.
    /// UI thread'ini bloklamaz.
    /// </summary>
    public static async Task RunSafeAsync(Func<Task> action, Action<Exception>? onError = null)
    {
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            // İptal beklenen bir durum
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Async UI error", ex);

            if (onError != null)
            {
                onError(ex);
            }
            else
            {
                // In Avalonia, we should use a custom dialog service. 
                // For now, we just log it (already logged above).
            }
        }
    }
}
