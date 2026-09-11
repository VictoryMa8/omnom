using System.Collections.Generic;
using System.Threading.Tasks;

namespace Omnom.Api.Services;

public interface IOpenRouterService
{
    Task<string> GenerateCompletionAsync(string systemPrompt, string userPrompt, string? model = null, CancellationToken cancellationToken = default);
    List<string> GetAvailableFreeModels();
}
