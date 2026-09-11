using System.Threading.Tasks;
using Omnom.Api.Models;

namespace Omnom.Api.Services;

public interface IMealParserService
{
    Task<AiParsedMealResult> ParseMealAsync(string prompt, string? mealTypeHint = null, CancellationToken cancellationToken = default);
}
