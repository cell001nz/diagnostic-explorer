using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Diagnostics.Service.Common.Transport;

namespace DiagnosticExplorer;

public sealed class NullRetroLogger : IRetroLogger
{
    public Task<long> Delete(string[] idList) => Task.FromResult(0L);

    public async IAsyncEnumerable<RetroMsg[]> GetMessages(
        RetroQuery query,
        [EnumeratorCancellation] CancellationToken cancel)
    {
        await Task.CompletedTask;
        yield break;
    }

    public Task WriteMessages(ICollection<DiagnosticMsg> msg, CancellationToken cancel) => Task.CompletedTask;
}