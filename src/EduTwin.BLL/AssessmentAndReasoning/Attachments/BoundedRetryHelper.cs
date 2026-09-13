using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EduTwin.BLL.AssessmentAndReasoning.Attachments;

/// <summary>
/// Helper for bounded retry operations with exponential backoff on transient I/O exceptions.
/// </summary>
public static class BoundedRetryHelper
{
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        int maxAttempts = 3,
        int initialDelayMs = 50,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var delay = initialDelayMs;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (attempt < maxAttempts && (ex is IOException || ex is UnauthorizedAccessException))
            {
                await Task.Delay(delay, cancellationToken);
                delay *= 2;
            }
        }
    }
}
