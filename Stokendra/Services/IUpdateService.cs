using System.Threading.Tasks;

namespace Stokendra.Services;

public interface IUpdateService
{
    Task<(string TagName, string HtmlUrl)> GetLatestReleaseAsync();
}
