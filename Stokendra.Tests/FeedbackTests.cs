using System;
using System.IO;
using System.Threading.Tasks;
using NSubstitute;
using Stokendra.Infrastructure;
using Stokendra.Models;
using Stokendra.Services;
using Stokendra.ViewModels;
using Xunit;

namespace Stokendra.Tests;

public class FeedbackTests
{
    public FeedbackTests()
    {
        LocalizationManager.Initialize("tr");
    }

    [Fact]
    public void AppLogger_GetSystemDiagnosticSummary_ContainsKeyPlatformDetails()
    {
        string summary = AppLogger.GetSystemDiagnosticSummary();

        Assert.NotNull(summary);
        Assert.Contains("Stokendra", summary);
        Assert.Contains(AppInfo.VersionString, summary);
        Assert.DoesNotContain("v" + AppInfo.VersionString, summary);
        Assert.Contains(Environment.Version.ToString(), summary);
        Assert.DoesNotContain("Makine", summary);
        Assert.DoesNotContain(Environment.MachineName, summary);
    }

    [Fact]
    public void AppLogger_GetRecentLogs_DoesNotThrowAndReturnsContent()
    {
        AppLogger.LogError("Test diagnostic error message for feedback test");
        string logs = AppLogger.GetRecentLogs(10);

        Assert.NotNull(logs);
        Assert.Contains("Test diagnostic error message", logs);
    }

    [Fact]
    public void FeedbackService_TargetEmail_DefaultsToCorrectGmailAddress()
    {
        var service = new FeedbackService();
        Assert.Equal("2005.tolgahan@gmail.com", service.TargetEmail);
    }

    [Fact]
    public void FeedbackService_BuildFullReportText_IncludesAllSections()
    {
        var service = new FeedbackService();
        var report = new FeedbackReport
        {
            Type = "Hata Bildirimi",
            Subject = "Raporlama Ekranı Dondu",
            UserEmail = "test@company.com",
            Message = "Birim filtresi seçilirken hata oluştu.",
            IncludeLogs = true,
            SystemInfo = "Test System Info Windows 11",
            RecentLogs = "Sample error stack trace line 1\r\nSample line 2"
        };

        string fullText = service.BuildFullReportText(report);

        Assert.Contains("STOKENDRA KULLANICI GERİ BİLDİRİM RAPORU", fullText);
        Assert.Contains("Raporlama Ekranı Dondu", fullText);
        Assert.Contains("test@company.com", fullText);
        Assert.Contains("Birim filtresi seçilirken hata oluştu.", fullText);
        Assert.Contains("Test System Info Windows 11", fullText);
        Assert.Contains("Sample error stack trace line 1", fullText);
    }

    [Fact]
    public async Task FeedbackViewModel_SendFeedback_RequiresSubjectAndMessage()
    {
        var mockService = Substitute.For<IFeedbackService>();
        var mockDialog = Substitute.For<IDialogService>();

        var vm = new FeedbackViewModel(mockService, mockDialog)
        {
            Subject = "",
            Message = ""
        };

        await vm.SendFeedbackAsync();

        // Servise çağrı yapılmamalı, kullanıcı uyarılmalı
        await mockService.DidNotReceive().SendFeedbackAsync(Arg.Any<FeedbackReport>());
        await mockDialog.Received(1).ShowMessageAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task FeedbackViewModel_SendFeedback_ClearsFieldsOnSuccess()
    {
        var mockService = Substitute.For<IFeedbackService>();
        mockService.SendFeedbackAsync(Arg.Any<FeedbackReport>())
            .Returns(Task.FromResult((true, "Gönderildi")));

        var mockDialog = Substitute.For<IDialogService>();

        var vm = new FeedbackViewModel(mockService, mockDialog)
        {
            Subject = "Test Başlığı",
            Message = "Test Açıklaması",
            UserEmail = "kullanici@deneme.com"
        };

        await vm.SendFeedbackAsync();

        await mockService.Received(1).SendFeedbackAsync(Arg.Is<FeedbackReport>(r =>
            r.Subject == "Test Başlığı" &&
            r.Message == "Test Açıklaması" &&
            r.UserEmail == "kullanici@deneme.com"));

        // Başarılı olduğunda form sıfırlanmalı
        Assert.Equal("", vm.Subject);
        Assert.Equal("", vm.Message);
    }
}
