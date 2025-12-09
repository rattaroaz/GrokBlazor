using System.Collections.Generic;
using System.Threading.Tasks;

namespace GrokBlazorApp.Services;

public interface IGrokApiService
{
    Task<string> GetChatCompletionAsync(List<Message> messages);
    Task<string> GetChatCompletionAsync(string userPrompt);
}
