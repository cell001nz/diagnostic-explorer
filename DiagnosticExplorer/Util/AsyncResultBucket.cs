using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiagnosticExplorer.Util
{
    public class AsyncResultBucket
    {
        private ConcurrentDictionary<string, ResultHolder> _results = new();

        public void SetResult(string requestId, object result)
        {
            if (requestId == null) throw new ArgumentNullException(nameof(requestId));

            if (_results.TryGetValue(requestId, out var holder))
                holder.CompletionSource.SetResult(result);
        }

        public async Task<T> GetResult<T>(string requestId, TimeSpan timeout) where T : class
        {
            if (requestId == null) throw new ArgumentNullException(nameof(requestId));

            var holder = _results.GetOrAdd(requestId, r => new ResultHolder(r));
            try
            {
                await Task.WhenAny(Task.Delay(timeout), holder.CompletionSource.Task);
                return (T) holder.Value;
            }
            finally
            {
                _results.TryRemove(requestId, out _);
            }
        }


        class ResultHolder
        {
            public ResultHolder(string requestId)
            {
                RequestId = requestId;
            }

            public string RequestId { get; }
            public object Value { get; set; }
            public TaskCompletionSource<object> CompletionSource { get; set; } = new();
        }

    }
}
