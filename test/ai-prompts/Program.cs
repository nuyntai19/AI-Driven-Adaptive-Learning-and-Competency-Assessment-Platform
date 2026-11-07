using System.Text.Encodings.Web;
using System.Text.Json;
using EduTwin.PromptEvaluation;

var json = new JsonSerializerOptions(FixtureReader.Json)
{
    WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
};
try
{
    var flags = new HashSet<string>(StringComparer.Ordinal);
    var values = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i] is "--live" or "--prepare" or "--self-test" or "--help")
        { if (!flags.Add(args[i])) throw new InvalidDataException(); }
        else if (args[i] is "--cases" or "--output" or "--model" or "--limit" or "--timeout-seconds")
        {
            var key = args[i];
            if (++i >= args.Length || args[i].StartsWith("--", StringComparison.Ordinal) || !values.TryAdd(key, args[i]))
                throw new InvalidDataException();
        }
        else throw new InvalidDataException("Unsupported argument.");
    }
    if (flags.Contains("--help") || args.Length == 0)
    {
        Console.WriteLine("Modes: --prepare | --live | --self-test. Options: --cases <directory> --output <new-file.json> --model <deployed-model> --limit <1..100> --timeout-seconds <1..120>.");
        Console.WriteLine("Live: supply AI_PROMPT_PROVIDER (Gemini/Groq), AI_PROMPT_API_KEY and AI_PROMPT_MODEL as process environment variables. Deployment environment names are also supported. Never pass keys on the command line. No .env files are loaded.");
        return;
    }
    if (flags.Count != 1) throw new InvalidDataException("Select one mode.");
    var directory = Path.GetFullPath(values.GetValueOrDefault("--cases") ?? Path.Combine("test", "ai-prompts", "cases"));
    var paths = Directory.GetFiles(directory, "*.json").Order(StringComparer.Ordinal).ToArray();
    if (paths.Length == 0 || paths.Length > 100) throw new InvalidDataException("Use one through 100 authored fixtures.");
    // Validate ALL files before any model call. Expectations are a separate developer-only file.
    var cases = paths.Select(path =>
    {
        if (new FileInfo(path).Length > 8 * 1024 * 1024) throw new InvalidDataException("Oversized fixture.");
        var id = Path.GetFileNameWithoutExtension(path);
        if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^[A-Za-z0-9_-]+$")) throw new InvalidDataException("Invalid fixture filename.");
        return (Id: id, Input: FixtureReader.Read(File.ReadAllText(path)));
    }).ToArray();
    foreach (var item in cases) _ = RequestBridge.Build(item.Input);
    if (flags.Contains("--self-test"))
    {
        await OfflineChecks.RunAsync(cases.Select(c => c.Input).ToArray());
        return;
    }
    var limit = int.Parse(values.GetValueOrDefault("--limit") ?? "20", System.Globalization.CultureInfo.InvariantCulture);
    var timeout = int.Parse(values.GetValueOrDefault("--timeout-seconds") ?? "60", System.Globalization.CultureInfo.InvariantCulture);
    if (limit is < 1 or > 100 || timeout is < 1 or > 120 || cases.Length > limit)
        throw new InvalidDataException("Case cap or timeout is invalid; explicitly increase --limit for larger suites.");
    var outputPath = Path.GetFullPath(values.GetValueOrDefault("--output") ??
        Path.Combine("test", "ai-prompts", "results", $"{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}-{(flags.Contains("--live") ? "model-output" : "prepared-inputs")}.json"));
    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    if (flags.Contains("--prepare"))
    {
        foreach (var item in cases) _ = RequestBridge.Build(item.Input);
        await using var prepared = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write);
        await JsonSerializer.SerializeAsync(prepared, cases.Select(c => UserOnlyExport.Input(c.Input)), json);
        Console.WriteLine($"Prepared {cases.Length} authored inputs; zero model calls. Output: {outputPath}");
        return;
    }
    var provider = Environment.GetEnvironmentVariable("AI_PROMPT_PROVIDER") ?? Environment.GetEnvironmentVariable("AIGrading__Provider") ?? "Gemini";
    if (provider is not ("Gemini" or "Groq")) throw new InvalidDataException("Unsupported provider.");
    if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("Gemini__VisualEvidenceModel")) ||
        Environment.GetEnvironmentVariable("Gemini__VisualEvidenceProfileApproved") == "true")
        throw new InvalidDataException("Independent visual-inspection deployments need their separate profile; this harness does not silently omit it.");
    var keyValue = Environment.GetEnvironmentVariable("AI_PROMPT_API_KEY") ??
        Environment.GetEnvironmentVariable(provider == "Groq" ? "AIGrading__GroqApiKey" : "Gemini__ApiKey");
    var modelValue = values.GetValueOrDefault("--model") ?? Environment.GetEnvironmentVariable("AI_PROMPT_MODEL") ??
        (provider == "Groq" ? Environment.GetEnvironmentVariable("AIGrading__GroqModel") :
            Environment.GetEnvironmentVariable("AIGrading__GeminiModel") ?? Environment.GetEnvironmentVariable("Gemini__Model"));
    if (string.IsNullOrWhiteSpace(keyValue) || string.IsNullOrWhiteSpace(modelValue))
        throw new InvalidDataException("Live mode requires the developer-supplied API key and deployed model.");
    var expectedPath = Path.Combine(Directory.GetParent(directory)!.FullName, "expectations.json");
    Dictionary<string, ExpectedObservation> expected = [];
    if (File.Exists(expectedPath))
    {
        var text = File.ReadAllText(expectedPath);
        using var doc = JsonDocument.Parse(text); FixtureReader.NoDuplicates(doc.RootElement);
        expected = JsonSerializer.Deserialize<Dictionary<string, ExpectedObservation>>(text, FixtureReader.Json) ?? [];
    }
    using IDisposable transportOwner = provider == "Groq" ? new GroqTransport(keyValue, modelValue, timeout) : new GeminiTransport(keyValue, modelValue, timeout);
    var transport = (IGradingTransport)transportOwner;
    var entries = new List<EvaluationEntry>(); var failures = 0;
    await AtomicReport.CreateAsync(outputPath, entries, json);
    foreach (var item in cases)
    {
        EvaluationResult result;
        try
        {
            result = await GradingExperiment.RunAsync(item.Input, expected.GetValueOrDefault(item.Id), transport, CancellationToken.None);
        }
        catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException))
        {
            // SDK exception bodies can contain API credentials or request content.
            // A transport failure is NOT a model response and never becomes fake report output.
            failures++;
            Console.Error.WriteLine($"{item.Id}: provider-call=FAIL; no model output was exported for this case.");
            continue;
        }
        // Report I/O errors must stop the run, not be mislabeled as provider failures.
        entries.Add(result.Entry); await AtomicReport.ReplaceOwnedAsync(outputPath, entries, json);
        var success = result.ContractValid && result.ObservationMatches;
        if (!success) failures++;
        var observationStatus = !expected.ContainsKey(item.Id) ? "NOT_CONFIGURED" : result.ObservationMatches ? "PASS" : "FAIL";
        Console.WriteLine($"{item.Id}: contract={(result.ContractValid ? "PASS" : "FAIL")}; expected-observations={observationStatus}");
    }
    Console.WriteLine($"Actual model responses: {entries.Count}/{cases.Length}; failed cases: {failures}. Output: {outputPath}");
    if (failures > 0) Environment.ExitCode = 1;
}
catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException))
{
    // Do not echo arbitrary argument, fixture text or provider diagnostics.
    Console.Error.WriteLine("Prompt evaluation failed. Check mode, fixture allowlist/limits, new output path, SDK availability and provider environment settings. No secrets or provider error bodies are logged.");
    Environment.ExitCode = 1;
}
