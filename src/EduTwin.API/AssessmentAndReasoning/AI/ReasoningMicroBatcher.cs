using System.Text.Json;
using EduTwin.BLL.AssessmentAndReasoning.AI;
using Microsoft.Extensions.Options;

namespace EduTwin.API.AssessmentAndReasoning.AI;

// A bounded, short coalescing window only. Durable ownership/retries/checkpoints stay in SQL.
public sealed class ReasoningMicroBatcher(IReasoningBatchExecutor executor, IOptions<AIGradingOptions> options, TimeProvider? clock = null) : IDisposable
{
    private readonly AIGradingOptions _options = options.Value;
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly object _gate = new();
    private readonly Dictionary<Partition, Group> _groups = [];
    private readonly CancellationTokenSource _shutdown = new();
    private int _pending;
    private bool _disposed;
    public string ProfileVersion => executor.ProfileVersion;
    public string ProviderName => executor.ProviderName;
    public string ModelName => executor.ModelName;

    public async Task<AnalyzeReasoningResponse> AnalyzeAsync(AnalyzeReasoningRequest request, CancellationToken token,
        AIAnalysisBatchPartition? partition = null)
    {
        token.ThrowIfCancellationRequested();
        if (_options.BatchSize is < 1 or > 5 || _options.MaxImages is < 1 or > 3
            || _options.MaxPendingItems is < 5 or > 1024 || _options.MaxInputCharacters is < 1000 or > 100000
            || _options.BatchWindow <= TimeSpan.Zero || _options.BatchWindow > TimeSpan.FromSeconds(1))
            throw GeminiAdapterException.ConfigurationInvalid();
        var id = Guid.NewGuid().ToString("N");
        var item = new Pending(new(id, request), token);
        var characters = JsonSerializer.Serialize(request).Length;
        var images = request.Question.ImageParts.Count + request.StudentSubmission.ImageParts.Count;
        if (request.ResponseRepairRule.HasValue || !_options.MicroBatchEnabled || _options.BatchSize == 1 || partition is null
            || partition.CenterId == Guid.Empty || partition.StudentId == Guid.Empty || partition.AssignmentId == Guid.Empty
            || characters > _options.MaxInputCharacters || images > _options.MaxImages)
        {
            var result = (await executor.ExecuteAsync([item.Item], token))[id];
            if (result.Error is not null) throw result.Error;
            return result.Response ?? throw GeminiAdapterException.ResponseInvalid();
        }
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_pending >= _options.MaxPendingItems) throw new AIAnalysisDeferredException(TimeSpan.FromSeconds(2));
            var key = new Partition(partition, request.Language, request.SchemaVersion);
            if (_groups.TryGetValue(key, out var current)
                && (current.Images + images > _options.MaxImages || current.Characters + characters > _options.MaxInputCharacters))
            {
                _groups.Remove(key);
                _ = DispatchAsync(current);
                current = null;
            }
            if (current is null)
            {
                current = new Group();
                _groups[key] = current;
                _ = FlushAfterWindowAsync(key, current);
            }
            current.Items.Add(item);
            current.Images += images;
            current.Characters += characters;
            _pending++;
            if (current.Items.Count >= _options.BatchSize)
            {
                _groups.Remove(key);
                _ = DispatchAsync(current);
            }
        }
        return await item.Completion.Task.WaitAsync(token);
    }

    private async Task FlushAfterWindowAsync(Partition key, Group group)
    {
        try { await Task.Delay(_options.BatchWindow, _clock, _shutdown.Token); }
        catch (OperationCanceledException) { return; }
        lock (_gate)
        {
            if (!_groups.TryGetValue(key, out var current) || !ReferenceEquals(current, group)) return;
            _groups.Remove(key);
        }
        await DispatchAsync(group);
    }

    private async Task DispatchAsync(Group group)
    {
        var active = group.Items.Where(x => !x.Token.IsCancellationRequested).ToArray();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
        var registrations = active.Select(x => x.Token.Register(() =>
        {
            if (active.All(p => p.Token.IsCancellationRequested)) linked.Cancel();
        })).ToArray();
        try
        {
            if (active.Length == 0) return;
            var results = await executor.ExecuteAsync(active.Select(x => x.Item).ToArray(), linked.Token);
            foreach (var item in active)
            {
                if (item.Token.IsCancellationRequested) item.Completion.TrySetCanceled(item.Token);
                else if (!results.TryGetValue(item.Item.ItemId, out var result)) item.Completion.TrySetException(GeminiAdapterException.ResponseInvalid());
                else if (result.Error is not null) item.Completion.TrySetException(result.Error);
                else if (result.Response is not null) item.Completion.TrySetResult(result.Response);
                else item.Completion.TrySetException(GeminiAdapterException.ResponseInvalid());
            }
        }
        catch (OperationCanceledException)
        {
            foreach (var item in active) item.Completion.TrySetCanceled(item.Token.IsCancellationRequested ? item.Token : _shutdown.Token);
        }
        catch (Exception ex)
        {
            foreach (var item in active) item.Completion.TrySetException(ex);
        }
        finally
        {
            foreach (var registration in registrations) registration.Dispose();
            foreach (var item in group.Items.Where(x => x.Token.IsCancellationRequested)) item.Completion.TrySetCanceled(item.Token);
            lock (_gate) _pending -= group.Items.Count;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _shutdown.Cancel();
            foreach (var group in _groups.Values)
                foreach (var item in group.Items) item.Completion.TrySetCanceled(_shutdown.Token);
            _groups.Clear();
        }
        // Do not dispose the token source while an in-flight provider holds registrations.
    }

    private sealed record Partition(AIAnalysisBatchPartition Scope, string Language, string Schema);
    private sealed class Group
    {
        public List<Pending> Items { get; } = [];
        public int Images { get; set; }
        public int Characters { get; set; }
    }
    private sealed record Pending(ReasoningBatchItem Item, CancellationToken Token)
    {
        public TaskCompletionSource<AnalyzeReasoningResponse> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
