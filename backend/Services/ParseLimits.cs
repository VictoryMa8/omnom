namespace Omnom.Api.Services;

public static class ParseLimits
{
    public static readonly TimeSpan AiWait = TimeSpan.FromSeconds(40);
    public static readonly TimeSpan RequestWait = TimeSpan.FromSeconds(50);
    public static readonly TimeSpan NutritionRemoteWait = TimeSpan.FromSeconds(8);
    public static readonly TimeSpan UsdaLookup = TimeSpan.FromSeconds(2);
}
