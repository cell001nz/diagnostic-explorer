using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using log4net.Appender;
using log4net.Core;
using log4net.Util;

namespace DiagnosticExplorer.Log4Net
{

	public class MultiErrorHandler : IErrorHandler
	{
		private List<IErrorHandler> _handlers = new List<IErrorHandler>();
	
		public MultiErrorHandler()
		{
			_handlers.Add(new OnlyOnceErrorHandler());
		}

		public void AddHandler(IErrorHandler handler)
		{
			if (handler == null) throw new ArgumentNullException(nameof(handler));

			_handlers.Add(handler);
		}

		public void Error(string message)
		{
			foreach (IErrorHandler handler in _handlers)
				handler.Error(message);
		}

		public void Error(string message, Exception exception)
		{
			foreach (IErrorHandler handler in _handlers)
				handler.Error(message, exception);
		}

		public void Error(string message, Exception exception, ErrorCode errorCode)
		{
			foreach (IErrorHandler handler in _handlers)
				handler.Error(message, exception, errorCode);
		}

		public static void SetErrorHandler(AppenderSkeleton appender, AppenderProxyErrorHandler handler)
		{
			IErrorHandler existingHandler = appender.ErrorHandler;
			MultiErrorHandler multiHandler = existingHandler as MultiErrorHandler;

			if (multiHandler == null)
			{
				multiHandler = new MultiErrorHandler();
				appender.ErrorHandler = multiHandler;
				if (existingHandler != null)
					multiHandler.AddHandler(existingHandler);
			}
			multiHandler.AddHandler(handler);
		}
	}


	/// <summary>
	/// This object records whether an error has been recorded
	/// </summary>
	public class AppenderProxyErrorHandler : IErrorHandler
	{

		public bool HasError { get; private set; }

		public string Message { get; private set; }

		private bool IsEnabled
		{
			get { return Thread.CurrentThread == _enabledThread; }
		}

		private Thread _enabledThread;

		public void EnableForCurrentThread()
		{
			_enabledThread = Thread.CurrentThread;
		}

		public void Disable()
		{
			_enabledThread = null;
		}

		public void Error(string message)
		{
			if (IsEnabled)
			{
				Message = message;
				HasError = true;
			}
		}

		public void Error(string message, Exception exception)
		{
			if (IsEnabled)
			{
				Message = exception?.Message ?? message;
				HasError = true;
			}
		}

		public void Error(string message, Exception exception, ErrorCode errorCode)
		{
			if (IsEnabled)
			{
				Message = exception?.Message ?? message;
				HasError = true;
			}
		}

		internal void ResetError()
		{
			HasError = false;
			Message = null;
		}
	}
}