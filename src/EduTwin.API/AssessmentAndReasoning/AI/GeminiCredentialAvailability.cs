using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace EduTwin.API.AssessmentAndReasoning.AI;

// Model access can differ by project. Remember permanent credential/model failures
// briefly; never retry that same rejected credential in an invocation or log its value.
public sealed class GeminiCredentialAvailability(TimeProvider clock)
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _unavailable = [];
    public bool IsAvailable(string key, string model)
    {
        var id = Identity(key, model);
        if (!_unavailable.TryGetValue(id, out var until)) return true;
        if (until > clock.GetUtcNow()) return false;
        _unavailable.TryRemove(id, out _); return true;
    }
    public bool MarkFailure(string key, string model, int status)
    {
        if (status is not (401 or 403 or 404)) return false;
        _unavailable[Identity(key, model)] = clock.GetUtcNow().AddMinutes(15);
        return true;
    }
    private static string Identity(string key, string model) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key + "\n" + model)));
}
