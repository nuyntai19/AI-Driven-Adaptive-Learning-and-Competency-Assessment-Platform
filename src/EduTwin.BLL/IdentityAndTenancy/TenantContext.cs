using System;
using System.Collections.Generic;
using EduTwin.DAL.Persistence.Tenancy;

namespace EduTwin.BLL.IdentityAndTenancy;

public class TenantContext : ITenantContext, ITenantContextInitializer, IBackgroundTenantScopeFactory, ITenantIdAccessor
{
    private class TenantState
    {
        public Guid? CenterId { get; set; }
        public Guid? UserId { get; set; }
        public string? Role { get; set; }
        public int? AuthVersion { get; set; }
    }

    private readonly Stack<TenantState> _states = new();

    private TenantState? CurrentState => _states.Count > 0 ? _states.Peek() : null;

    public Guid? CenterId => CurrentState?.CenterId;
    public Guid? UserId => CurrentState?.UserId;
    public string? Role => CurrentState?.Role;
    public int? AuthVersion => CurrentState?.AuthVersion;
    public bool IsResolved => CenterId.HasValue && CenterId.Value != Guid.Empty;

    public void Initialize(Guid centerId, Guid userId, string role, int authVersion)
    {
        if (centerId == Guid.Empty) throw new InvalidOperationException("CenterId cannot be empty.");
        if (userId == Guid.Empty) throw new InvalidOperationException("UserId cannot be empty.");
        if (string.IsNullOrWhiteSpace(role)) throw new InvalidOperationException("Role cannot be blank.");
        if (authVersion < 1) throw new InvalidOperationException("Invalid AuthVersion.");

        if (IsResolved)
        {
            if (CenterId != centerId || UserId != userId || Role != role || AuthVersion != authVersion)
            {
                throw new InvalidOperationException("Cannot change identity in the same request scope.");
            }
            return; // Idempotent
        }

        _states.Push(new TenantState
        {
            CenterId = centerId,
            UserId = userId,
            Role = role,
            AuthVersion = authVersion
        });
    }

    public IDisposable BeginScope(Guid centerId)
    {
        if (centerId == Guid.Empty) throw new InvalidOperationException("CenterId cannot be empty.");
        if (UserId != null) throw new InvalidOperationException("Cannot override authenticated request with background scope.");

        var state = new TenantState { CenterId = centerId };
        _states.Push(state);
        return new ScopeDisposable(this, state);
    }

    private void PopScope(TenantState expectedState)
    {
        if (_states.Count == 0 || _states.Peek() != expectedState)
        {
            throw new InvalidOperationException("Scopes must be disposed in LIFO order without skipping.");
        }
        _states.Pop();
    }

    private class ScopeDisposable : IDisposable
    {
        private readonly TenantContext _context;
        private readonly TenantState _state;
        private bool _disposed;

        public ScopeDisposable(TenantContext context, TenantState state)
        {
            _context = context;
            _state = state;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _context.PopScope(_state);
            _disposed = true;
        }
    }

}
