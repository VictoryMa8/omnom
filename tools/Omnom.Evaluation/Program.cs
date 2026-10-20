using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Omnom.Api.Data;
using Omnom.Api.Services;
using Omnom.Evaluation;

var json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true, NumberHandling = JsonNumberHandling.Strict };
var options = new Dictionary<string, string>(); var flags = new HashSet<string>();
var allowed = new HashSet<string> { "--mode", "--dataset", "--output", "--case", "--max-requests", "--export-references" };
try
{
    for (var i = 0; i < args.Length; i++)
        if (args[i] is "--allow-live" or "--require-perfect") flags.Add(args[i]);
        else if (allowed.Contains(args[i]) && i + 1 < args.Length && !args[i + 1].StartsWith("--")) options.Add(args[i], args[++i]);
        else throw new ArgumentException("Unknown, duplicated, or incomplete argument: " + args[i]);
    using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=:memory:").Options);
    db.Database.OpenConnection(); DbInitializer.Initialize(db);
    var references = db.FoodReferences.OrderBy(f => f.Id).Select(f => new { f.Id, f.Name, f.NormalizedQuery, f.DefaultServingGrams,
        f.DefaultServingUnit, f.CaloriesPer100g, f.ProteinPer100g, f.CarbsPer100g, f.FatPer100g, f.FiberPer100g, f.IsStaple }).ToList();
    if (options.TryGetValue("--export-references", out var export))
    {
        await File.WriteAllTextAsync(export, JsonSerializer.Serialize(new { version = "1.0.0", source = "backend/Data/DbInitializer.cs", foods = references }, json) + "\n");
        return 0;
    }
    var mode = options.GetValueOrDefault("--mode", "heuristic");
    if (mode is not ("heuristic" or "stubbed-ai" or "live-ai")) throw new ArgumentException("Mode must be heuristic, stubbed-ai, or live-ai.");
    var live = mode == "live-ai";
    var cap = options.TryGetValue("--max-requests", out var rawCap) && int.TryParse(rawCap, out var n) ? n : 0;
    if (live && (!flags.Contains("--allow-live") || cap is < 1 or > 100 || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENROUTER_API_KEY"))))
        throw new ArgumentException("Live mode requires --allow-live, --max-requests 1..100, and an explicitly exported OPENROUTER_API_KEY. .env is never loaded.");
    if (!live && (flags.Contains("--allow-live") || options.ContainsKey("--max-requests"))) throw new ArgumentException("Live flags require --mode live-ai.");
    var dataset = Path.GetFullPath(options.GetValueOrDefault("--dataset", "tests/fixtures/meal-evaluation/v1/cases.json"));
    var raw = await File.ReadAllTextAsync(dataset);
    var corpus = JsonSerializer.Deserialize<Corpus>(raw, json) ?? throw new InvalidDataException("Empty corpus.");
    CorpusValidation.Validate(corpus);
    var snapshotPath = Path.Combine(Path.GetDirectoryName(dataset)!, "references.json");
    using var snapshot = JsonDocument.Parse(await File.ReadAllTextAsync(snapshotPath));
    var current = JsonSerializer.SerializeToElement(references, json);
    if (JsonSerializer.Serialize(snapshot.RootElement.GetProperty("foods")) != JsonSerializer.Serialize(current))
        throw new InvalidDataException("Seed reference snapshot changed. Version/review corpus before regenerating expectations.");
    var cases = options.TryGetValue("--case", out var id) ? corpus.Cases.Where(c => c.Id == id).ToList() : corpus.Cases;
    if (cases.Count == 0) throw new ArgumentException("Unknown case ID.");
    var offlineHandler = new NetworkForbidden();
    using var offline = new HttpClient(offlineHandler);
    var foods = new LocalReferences(new UsdaFoodService(offline, db, new ConfigurationBuilder().Build(), NullLogger<UsdaFoodService>.Instance));
    var liveConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
        ["OPENROUTER_API_KEY"] = live ? Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") : null }).Build();
    var approved = new OpenRouterService(offline, liveConfig, db, NullLogger<OpenRouterService>.Instance).GetAvailableFreeModels().ToHashSet();
    using var handler = new CappedRouterHandler(cap, approved, live ? new HttpClientHandler { AllowAutoRedirect = false } : new NetworkForbidden());
    using var client = new HttpClient(handler) { Timeout = ParseLimits.AiWait + TimeSpan.FromSeconds(2) };
    var router = new OpenRouterService(client, liveConfig, db, NullLogger<OpenRouterService>.Instance);
    using var cancel = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancel.Cancel(); };
    var started = DateTimeOffset.UtcNow; var runs = new List<CaseRun>(); string? stopped = null;
    foreach (var fixture in cases)
    {
        if (cancel.IsCancellationRequested) { stopped = "cancelled"; break; }
        if (live && (handler.StopReason != null || handler.Sent >= cap)) { stopped = handler.StopReason ?? "request-cap"; break; }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancel.Token); deadline.CancelAfter(ParseLimits.RequestWait);
        var timer = Stopwatch.StartNew();
        try
        {
            var parser = new MealParserService(live ? router : new CannedRouter(fixture, mode == "heuristic"), foods, NullLogger<MealParserService>.Instance);
            var result = await parser.ParseMealAsync(fixture.Prompt, cancellationToken: deadline.Token);
            if (live && handler.StopReason != null)
            { stopped = handler.StopReason; runs.Add(new(fixture.Id, fixture.Category, "not-scored:" + stopped, timer.Elapsed.TotalMilliseconds, false, null, stopped)); break; }
            runs.Add(new(fixture.Id, fixture.Category, "scored", timer.Elapsed.TotalMilliseconds,
                result.AiSummary?.Contains("local parser", StringComparison.OrdinalIgnoreCase) == true,
                Scoring.Score(fixture, result), null));
        }
        catch (OperationCanceledException)
        { var reason = cancel.IsCancellationRequested ? "cancelled" : "deadline"; runs.Add(new(fixture.Id, fixture.Category, reason, timer.Elapsed.TotalMilliseconds, false, null, reason)); if (cancel.IsCancellationRequested) { stopped = reason; break; } }
        catch (Exception ex)
        { runs.Add(new(fixture.Id, fixture.Category, "runner-error", timer.Elapsed.TotalMilliseconds, false, null, ex.GetType().Name)); }
    }
    string Git(params string[] arguments)
    {
        try { using var p = new Process { StartInfo = new("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false } };
            foreach (var a in arguments) p.StartInfo.ArgumentList.Add(a); p.Start(); var value = p.StandardOutput.ReadToEnd().Trim(); p.WaitForExit(); return p.ExitCode == 0 ? value : "unavailable"; }
        catch { return "unavailable"; }
    }
    var output = Path.GetFullPath(options.GetValueOrDefault("--output", $".evaluation/{mode}.json"));
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    var report = new { datasetVersion = corpus.Version, datasetSha256 = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw))),
        referenceSha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(snapshotPath))), mode,
        revision = Git("rev-parse", "HEAD"), workingTree = Git("status", "--short"), startedAt = started, completedAt = DateTimeOffset.UtcNow,
        environment = new { dotnet = Environment.Version.ToString(), platform = System.Runtime.InteropServices.RuntimeInformation.OSDescription },
        massToleranceGrams = Scoring.MassTolerance, offlineNetworkAttempts = offlineHandler.Attempts,
        selectedCases = cases.Count, unattemptedCaseIds = cases.Skip(runs.Count).Select(c => c.Id), stopReason = stopped,
        live = new { enabled = live, requestCap = live ? (int?)cap : null, requestsSent = handler.Sent,
            approvedModels = approved, requestedModels = handler.RequestedModels, actualModels = handler.ActualModels,
            requestsWithReportedCost = handler.WithCost, reportedCostSum = handler.WithCost > 0 ? (double?)handler.CostSum : null,
            requestsWithoutReportedCost = handler.MissingCost },
        metrics = Scoring.Aggregate(runs), byCategory = runs.GroupBy(r => r.Category).ToDictionary(g => g.Key, g => Scoring.Aggregate(g)), cases = runs,
        limitations = new[] { "Offline canned AI does not measure model quality.", "All modes resolve against pinned local references; no remote USDA.",
            "Nutrition expectations are code-reference contracts, not independent dietary measurements.", "Live usage/cost is unknown when a provider omits metadata." } };
    await File.WriteAllTextAsync(output, JsonSerializer.Serialize(report, json) + "\n");
    await File.WriteAllTextAsync(Path.ChangeExtension(output, ".md"), Reports.Markdown(corpus, mode, runs, stopped));
    Console.WriteLine($"{mode}: {runs.Count}/{cases.Count} cases attempted; {runs.Count(r => r.Score?.FullyCorrect == true)} fully correct; report {output}");
    if (stopped != null) Console.WriteLine("Stopped: " + stopped);
    if (offlineHandler.Attempts > 0 || runs.Any(r => r.Outcome == "runner-error")) return 2;
    if (stopped != null || runs.Any(r => r.Outcome != "scored")) return 3;
    return flags.Contains("--require-perfect") && runs.Any(r => r.Score?.FullyCorrect != true) ? 1 : 0;
}
catch (Exception ex) when (ex is ArgumentException or InvalidDataException or JsonException or IOException)
{ Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message); return 2; }
