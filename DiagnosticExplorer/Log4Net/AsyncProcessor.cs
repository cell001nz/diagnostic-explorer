using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using log4net.Core;
using log4net.Util;

namespace DiagnosticExplorer.Log4Net
{
	public class AsyncProcessor
	{
		private BlockingCollection<LoggingEventContext> _loggingEvents;
		private CancellationTokenSource _loggingCancelationTokenSource;
		private CancellationToken _loggingCancelationToken;
		private Task _loggingTask;
		private Action<LoggingEvent> _forwardLoggingEvent;
		private volatile bool _shutDownRequested;


		public AsyncProcessor(BufferOverflowMode overflow, int bufferSize, Action<LoggingEvent> forwardLoggingEvent)
		{
			Overflow = overflow;
			BufferSize = bufferSize;
			_forwardLoggingEvent = forwardLoggingEvent;

			if (Overflow == BufferOverflowMode.Block)
				_loggingEvents = new BlockingCollection<LoggingEventContext>(BufferSize);
			else
				_loggingEvents = new BlockingCollection<LoggingEventContext>();

			_loggingCancelationTokenSource = new CancellationTokenSource();
			_loggingCancelationToken = _loggingCancelationTokenSource.Token;
			_loggingTask = new Task(SubscriberLoop, _loggingCancelationToken);
		}

		private BufferOverflowMode Overflow { get; }
		private int BufferSize { get; }

		public void Start()
		{
			_loggingTask.Start();
		}

		private void SubscriberLoop()
		{
			//The task will continue in a blocking loop until
			//the queue is marked as adding completed, or the task is canceled.
			try
			{
				//This call blocks until an item is available or until adding is completed
				foreach (LoggingEventContext entry in _loggingEvents.GetConsumingEnumerable(_loggingCancelationToken))
				{
					try
					{
						_forwardLoggingEvent(entry.LoggingEvent);
					}
					catch (Exception ex)
					{
						ForwardInternalError(ex.Message, ex);
					}
				}
			}
			catch (OperationCanceledException ex)
			{
				if (!ex.CancellationToken.IsCancellationRequested)
				//The thread was canceled before all entries could be forwarded and the collection completed.
					ForwardInternalError("Subscriber task was canceled before completion.", ex);
				//Cancellation is called in the CompleteSubscriberTask so don't call that again.
			}
			catch (ThreadAbortException ex)
			{
				//Thread abort may occur on domain unload.
				ForwardInternalError("Subscriber task was aborted.", ex);
				//Cannot recover from a thread abort so complete the task.
				CompleteSubscriberTaskAfterError();
				//The exception is swallowed because we don't want the client application
				//to halt due to a logging issue.
			}
			catch (Exception ex)
			{
				//On exception, try to log the exception
				ForwardInternalError("Subscriber task error in forwarding loop.", ex);
				//Any error in the loop is going to be some sort of extenuating circumstance from which we
				//probably cannot recover anyway.   Complete subscribing.
				CompleteSubscriberTaskAfterError();
			}
		}

		protected void ForwardInternalError(string message, Exception exception)
		{
			try
			{
				Debug.WriteLine(exception);
				LogLog.Error(GetType(), message, exception);
//				ForwardingAppenderBase.LogLogError(GetType(), message, exception);
//				_forwardLoggingEvent(new LoggingEvent(GetType(), null, GetType().Name, Level.Error, message, exception) {Fix = Fix});
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex);
			}
		}

		private void CompleteSubscriberTaskAfterError()
		{
			_shutDownRequested = true;
			if (_loggingEvents == null || _loggingEvents.IsAddingCompleted)
			{
				return;
			}
			//Don't allow more entries to be added.
			_loggingEvents.CompleteAdding();
		}

	
		public FixFlags Fix { get; set; }

		public int QueueSize
		{
			get { return _loggingEvents.Count; }
		}

		public void Append(LoggingEvent loggingEvent)
		{
			if (!_shutDownRequested && loggingEvent != null)
			{
				bool discard = Overflow == BufferOverflowMode.Discard && _loggingEvents.Count > BufferSize;
				if (discard)
					throw new LogException($"Maximum BufferSize of {BufferSize} has been reached");

				loggingEvent.Fix = Fix;
				_loggingEvents.Add(new LoggingEventContext(loggingEvent), _loggingCancelationToken);
			}
		}

		public void Append(LoggingEvent[] loggingEvents)
		{
			if (!_shutDownRequested && loggingEvents != null)
			{
				foreach (var loggingEvent in loggingEvents)
				{
					Append(loggingEvent);
				}
			}
		}

		public void Close()
		{
			_shutDownRequested = true;
			if (_loggingEvents == null || _loggingEvents.IsAddingCompleted)
				return;

			//Don't allow more entries to be added.
			_loggingEvents.CompleteAdding();

			//Wait 5 seconds for the events to flush
			bool taskEnded = _loggingTask.Wait(TimeSpan.FromSeconds(5));

			//If the task hasn't ended, cancel the task and record the error
			if (!taskEnded)
			{
				_loggingCancelationTokenSource.Cancel();
				ForwardInternalError("The buffer was not able to be flushed before timeout occurred.", null);
			}
		}

		public void Dispose()
		{
			if (_loggingTask != null)
			{
				if (!(_loggingTask.IsCanceled || _loggingTask.IsCompleted || _loggingTask.IsFaulted))
				{
					try
					{
						Close();
					}
					catch (Exception ex)
					{
						ForwardingAppenderBase.LogLogError(GetType(), "Exception Completing Subscriber Task in Dispose Method", ex);
					}
				}
				try
				{
					_loggingTask.Dispose();
				}
				catch (Exception ex)
				{
					ForwardingAppenderBase.LogLogError(GetType(), "Exception Disposing Logging Task", ex);
				}
				finally
				{
					_loggingTask = null;
				}
			}
			if (_loggingEvents != null)
			{
				try
				{
					_loggingEvents.Dispose();
				}
				catch (Exception ex)
				{
					ForwardingAppenderBase.LogLogError(GetType(), "Exception Disposing BlockingCollection", ex);
				}
				finally
				{
					_loggingEvents = null;
				}
			}
			if (_loggingCancelationTokenSource != null)
			{
				try
				{
					_loggingCancelationTokenSource.Dispose();
				}
				catch (Exception ex)
				{
					ForwardingAppenderBase.LogLogError(GetType(), "Exception Disposing CancellationTokenSource", ex);
				}
				finally
				{
					_loggingCancelationTokenSource = null;
				}
			}
		}
	}
}