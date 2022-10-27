using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Channels;
using System.Threading.Tasks;
using DiagnosticExplorer.Util;

namespace DiagnosticExplorer;

sealed public class EventSinkStream : IDisposable
{
    public event EventHandler Disposed;
    private Subject<SystemEvent> _eventSubject;
    private readonly IDisposable _eventSubscription;
    private readonly int bufferSize = 100;

    public EventSinkStream(SystemEvent[] initialEvents, TimeSpan buffer, int bufferSize)
    {
        InitialEvents = initialEvents;
        this.bufferSize = bufferSize;

        _eventSubject = new();
        _eventSubscription = _eventSubject.BufferWhenAvailable(buffer)
            .Subscribe(WriteEvents, () => EventChannel?.Writer.Complete());

        EventChannel = Channel.CreateBounded<IList<SystemEvent>>(
            new BoundedChannelOptions(10000) {
                SingleReader = true,
                FullMode = BoundedChannelFullMode.DropWrite,
            });
    }

    private void WriteEvents(IList<SystemEvent> evts)
    {
        if (evts.Count <= bufferSize)
            EventChannel?.Writer.TryWrite(evts);
        else
            evts.ToObservable().Buffer(bufferSize).ForEachAsync(chunk => EventChannel?.Writer.TryWrite(chunk));
    }

    public SystemEvent[] InitialEvents { get; }

    public void StreamEvent(SystemEvent evt)
        => _eventSubject.OnNext(evt);


    public Channel<IList<SystemEvent>> EventChannel { get; }

    public void Dispose()
    {
        Disposed?.Invoke(this, EventArgs.Empty);
        Disposed = null;

        _eventSubscription?.Dispose();
        _eventSubject = null;
    }
}