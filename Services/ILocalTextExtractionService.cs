using System.Threading.Tasks;

namespace GrokBlazorApp.Services;

public interface ILocalTextExtractionService
{
    Task<string> ExtractTextAsync(string fileName, byte[] fileContent);
}
