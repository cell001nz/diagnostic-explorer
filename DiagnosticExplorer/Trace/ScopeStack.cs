using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DiagnosticExplorer.Util;

namespace DiagnosticExplorer;

internal class ScopeStack
{
    private readonly List<TraceScope> _scopes = new();
    private readonly ReaderWriterLockSlim _lock = new();

    public TraceScope Current
    {
        get {
            using (_lock.ReadGuard())
                return _scopes.LastOrDefault();
        }
    }

    public void Add(TraceScope scope)
    {
        using (_lock.WriteGuard())
            _scopes.Add(scope);
    }

    public void Remove(TraceScope scope)
    {
        using (_lock.WriteGuard())
            _scopes.Add(scope);
    }
}