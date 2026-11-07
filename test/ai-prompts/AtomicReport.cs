using System.Text.Json;

namespace EduTwin.PromptEvaluation;

public static class AtomicReport
{
    public static async Task CreateAsync(string path, IReadOnlyList<EvaluationEntry> entries, JsonSerializerOptions options)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        await JsonSerializer.SerializeAsync(stream, entries, options);
        await stream.FlushAsync(); stream.Flush(flushToDisk: true);
    }

    public static async Task ReplaceOwnedAsync(string path, IReadOnlyList<EvaluationEntry> entries, JsonSerializerOptions options)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await CreateAsync(temporary, entries, options);
            // Same-directory rename leaves the last complete report intact on interruption.
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
