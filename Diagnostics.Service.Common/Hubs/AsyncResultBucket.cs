using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DiagnosticExplorer;

namespace Diagnostics.Service.Common.Hubs;

class AsyncCallException : ApplicationException
{
    public AsyncCallException()
    {
    }

    protected AsyncCallException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }

    public AsyncCallException(string? message, string? detail) : base(message)
    {
        Detail = detail;
    }

    public string Detail { get; set; }

    public override string ToString()
    {
        return Message + Environment.NewLine + Detail;
    }
}

public class AsyncResultBucket
{
    private ConcurrentDictionary<string, TaskCompletionSource<object>> _results = new();

    public void SetResult(RpcResult result, object returnValue)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));

        if (_results.TryGetValue(result.RequestId, out var completionSource))
        {
            if (result.IsSuccess)
                completionSource.SetResult(returnValue);
            else
                completionSource.SetException(new AsyncCallException(result.Message, result.Detail));
        }
    }

    public async Task<T> GetResult<T>(string requestId, TimeSpan timeout, CancellationToken cancel)
    {
        if (requestId == null) throw new ArgumentNullException(nameof(requestId));

        var completionSource = _results.GetOrAdd(requestId, _ => new TaskCompletionSource<object>());
        try
        {
            Task awaitResult = await Task.WhenAny(Task.Delay(timeout, cancel), completionSource.Task);

            if (awaitResult == completionSource.Task)
                return (T) completionSource.Task.Result;

            throw new TimeoutException($"{requestId} GetResult Timed out waiting");
        }
        finally
        {
            _results.TryRemove(requestId, out _);
        }
    }
}