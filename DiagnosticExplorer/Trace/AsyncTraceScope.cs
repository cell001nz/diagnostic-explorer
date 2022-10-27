#region Copyright

// Diagnostic Explorer, a .Net diagnostic toolset
// Copyright (C) 2010 Cameron Elliot
// 
// This file is part of Diagnostic Explorer.
// 
// Diagnostic Explorer is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Diagnostic Explorer is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public License
// along with Diagnostic Explorer.  If not, see <http://www.gnu.org/licenses/>.
// 
// http://diagexplorer.sourceforge.net/

#endregion

using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using log4net;
using System.Linq;

namespace DiagnosticExplorer
{

	using ATraceItem = TraceItem<AsyncTraceScope>;

	/// <summary>Enabled trace to a single source through method calls</summary>
	public class AsyncTraceScope : IDisposable
    {
        private static AsyncLocal<List<AsyncTraceScope>> _scopeStack;

        private static List<AsyncTraceScope> ScopeStack
        {
            get => _scopeStack.Value ??= new List<AsyncTraceScope>();
            set => _scopeStack.Value = value;
        }

		private DateTime _created = DateTime.UtcNow;
		private DateTime? _disposed;
		private List<ATraceItem> _ATraceItems = new List<ATraceItem>();
		private bool _forceTrace;
		private bool _isRoot;
		private Timer _autoTraceTimer;
		private object _syncLock = new object();

		/// <summary>
		/// Sorted dictionary of time in milliseconds vs the trace method
		/// which should be used if the operation exceeds that time
		/// </summary>
		private SortedDictionary<int, Action<string>> _traceMethods;

		#region Constructors

		public AsyncTraceScope([CallerMemberName] string name = null)
		{
			Setup(name, null, null, false);
		}

		public AsyncTraceScope(TraceMode mode, [CallerMemberName] string name = null)
		{
			Setup(name, null, mode, false);
		}

		public AsyncTraceScope(Action<string> traceMethod, [CallerMemberName] string name = null)
		{
			Setup(name, traceMethod, null, false);
		}

		public AsyncTraceScope(Action<string> traceMethod, TraceMode mode, [CallerMemberName] string name = null)
		{
			Setup(name, traceMethod, mode, false);
		}

		public AsyncTraceScope(string name, TraceMode mode)
		{
			Setup(name, null, mode, false);
		}

		public AsyncTraceScope(string name, Action<string> traceMethod)
		{
			Setup(name, traceMethod, null, false);
		}

		public AsyncTraceScope(string name, Action<string> traceMethod, TraceMode mode)
		{
			Setup(name, traceMethod, mode, false);
		}
	
		public AsyncTraceScope(string name, Action<string> traceMethod, bool forceTrace)
		{
			Setup(name, traceMethod, null, forceTrace);
		}

		public AsyncTraceScope(Action<string> traceMethod, bool forceTrace, [CallerMemberName] string name = null)
		{
			Setup(name, traceMethod, null, forceTrace);
		}

		public AsyncTraceScope(string name, Action<string> traceMethod,  TraceMode mode, bool forceTrace)
		{
			Setup(name, traceMethod, mode, forceTrace);
		}

		private void Setup(string name, Action<string> traceMethod, TraceMode? mode, bool forceTrace)
		{
			_traceMethods = new SortedDictionary<int, Action<string>>();
			SetTraceAction(0, traceMethod);

			Name = name;
			ScopeTraceMode = mode;

			_forceTrace = forceTrace;

			_isRoot = ScopeStack.Count == 0;

			if (ScopeStack.Count != 0)
			{
				lock (CurrentScope._syncLock)
					CurrentScope._ATraceItems.Add(new ATraceItem(this));
			}

			ScopeStack.Add(this);
		}

        public IDisposable SetAsCurrentScope()
        {
            if (CurrentScope == this)
                return new DisposeAction();

			ScopeStack.Add(this);
            return new DisposeAction(RemoveFromScopeStack);
        }

        private void RemoveFromScopeStack()
        {
            if (ScopeStack.Count == 0)
                return;

            ScopeStack.RemoveAt(ScopeStack.Count - 1);
            if (ScopeStack.Count == 0)
                ScopeStack = null;
        }

		private class DisposeAction : IDisposable
        {
            private Action _action;

            public DisposeAction()
            {
            }

            public DisposeAction(Action action)
            {
                _action = action;
            }

            public void Dispose()
            {
                _action?.Invoke();
            }
        }

		#endregion

		public void StartAutoTraceTimer(TimeSpan time)
		{
			if (_autoTraceTimer != null)
				_autoTraceTimer.Dispose();

			_autoTraceTimer = new Timer(AutoTraceAfterTimeout, null, (int)time.TotalMilliseconds, Timeout.Infinite);
		}

		private void AutoTraceAfterTimeout(object state)
		{
			lock (_syncLock)
				_ATraceItems.Add(new ATraceItem("FORCE TRACE AFTER TIMEOUT"));

			TraceMessage();
		}

		public static AsyncTraceScope Current
		{
			get { return ScopeStack?.LastOrDefault(); }
		}

		public string Name { get; set; }
		public bool IsHidden { get; set; }

		public TraceMode? ScopeTraceMode { get; set; }

		public TimeSpan? SuppressDetailThreshold { get; set; }

		public void SetTraceAction(Action<string> traceMethod)
		{
			SetTraceAction(0, traceMethod);
		}

		public void SetTraceAction(int timeMillis, Action<string> traceMethod)
		{
			_traceMethods[timeMillis] = traceMethod;
		}

		public static void Invoke(ISynchronizeInvoke invoke, Action action)
		{
			AsyncTraceScope currentScope = CurrentScope;
			if (currentScope == null)
			{
				action();
			}
			else
			{
				Func<Action, AsyncTraceScope> traceFunc = ExecuteSynchronizeInvoke;
				AsyncTraceScope scope = (AsyncTraceScope)invoke.Invoke(traceFunc, new object[] { action });
				lock (currentScope._syncLock)
					currentScope._ATraceItems.Add(new ATraceItem(scope));
			}
		}

		private static AsyncTraceScope ExecuteSynchronizeInvoke(Action action)
		{
			using (AsyncTraceScope scope = new AsyncTraceScope())
			{
				action();
				return scope;
			}
		}

		public static TraceMode TraceMode
		{
			get
			{
				if (ScopeStack == null) return TraceMode.Normal;

				for (int i = ScopeStack.Count - 1; i >= 0; i--)
				{
					AsyncTraceScope scope = ScopeStack[i];
					if (scope.ScopeTraceMode.HasValue)
						return scope.ScopeTraceMode.Value;
				}
				return TraceMode.Normal;
			}
		}

		private static void WriteGenericArguments(StringBuilder sb, IEnumerable<Type> types)
		{
			sb.Append("<");
			foreach (Type t in types)
			{
				AppendType(sb, t);
				sb.Append(", ");
			}
			if (sb[sb.Length - 1] != '<')
				sb.Length -= 2;
			sb.Append(">");
		}

		private static void AppendType(StringBuilder sb, Type type)
		{
			if (!type.IsGenericType)
			{
				sb.Append(type.Name);
			}
			else
			{
				sb.Append(Regex.Replace(type.UnderlyingSystemType.Name, "`.*", ""));
				WriteGenericArguments(sb, type.GetGenericArguments());
			}
		}

		public TimeSpan Age => DateTime.UtcNow.Subtract(_created);


        public override string ToString()
		{
			StringBuilder sb = new StringBuilder();
			StringWriter writer = new StringWriter(sb);
			IndentedTextWriter indentedWriter = new IndentedTextWriter(writer);

			DateTime lastMessage = _created;
			ToString(indentedWriter, _created, ref lastMessage, 0);
			indentedWriter.Flush();
			writer.Flush();
			return sb.ToString();
		}

		public void ToString(IndentedTextWriter writer, DateTime scopeStart, ref DateTime lastMessage, int indent)
		{
			if (IsHidden) return;

			lock (_syncLock)
			{
				if (_ATraceItems.Count == 0)
				{
					WriteBeginEndScope(this, writer, scopeStart, ref lastMessage, false);
				}
				else
				{
					WriteBeginScope(writer, scopeStart, ref lastMessage);

					writer.Indent += indent;
					foreach (ATraceItem item in _ATraceItems)
					{
						if (item.TraceScope == null)
						{
							WriteString(writer, item.Message, item.Created, scopeStart, ref lastMessage);
						}
						else if (FullTraceRequired(item))
						{
							item.TraceScope.ToString(writer, scopeStart, ref lastMessage, 1);
						}
						else
						{
							WriteBeginEndScope(item.TraceScope, writer, scopeStart, ref lastMessage, true);
						}
					}
					writer.Indent -= indent;

					WriteEndScope(writer, scopeStart, ref lastMessage);
				}
			}
		}

		private bool FullTraceRequired(ATraceItem item)
		{
			if (item.TraceScope == null) return false;
			if (item.TraceScope._forceTrace) return false;
			if (item.TraceScope.SuppressDetailThreshold == null) return true;

			TimeSpan thresh = item.TraceScope.SuppressDetailThreshold.Value;
			TimeSpan duration = item.TraceScope._disposed.Value - item.TraceScope._created;
			return duration >= thresh;
		}

		private void WriteBeginScope(IndentedTextWriter writer, DateTime scopeStart, ref DateTime lastMessage)
		{
			if (Name == null) return;

			string message = string.Format("BEGIN {0}", Name);
			if (_disposed != null)
				message = string.Format("{0} ({1:N3} seconds)", message, _disposed.Value.Subtract(_created).TotalSeconds);

			WriteString(writer, message, _created, scopeStart, ref lastMessage);
		}

		private void WriteEndScope(IndentedTextWriter writer, DateTime scopeStart, ref DateTime lastMessage)
		{
			if (Name == null) return;
			if (_disposed == null) return;

			string message = string.Format("END {0} ({1:N3} seconds)", Name, _disposed.Value.Subtract(_created).TotalSeconds);

			WriteString(writer, message, _disposed.Value, scopeStart, ref lastMessage);
		}

		private static void WriteBeginEndScope(AsyncTraceScope scope, IndentedTextWriter writer,
			DateTime scopeStart, ref DateTime lastMessage, bool suppressed)
		{
			if (scope.Name == null) return;
			if (scope._disposed == null) return;

			TimeSpan duration = scope._disposed.Value.Subtract(scope._created);

			string message = string.Format("BEGIN/END{0} {1} ({2:N3} seconds)",
			                               suppressed ? "*" : "",
			                               scope.Name,
			                               duration.TotalSeconds);

			WriteString(writer, message, scope._disposed.Value, scopeStart, ref lastMessage);
		}

		private static void WriteString(IndentedTextWriter writer, string message, DateTime itemDate, DateTime scopeStart, ref DateTime lastMessage)
		{
			double age = itemDate.Subtract(scopeStart).TotalSeconds;
			double split = itemDate.Subtract(lastMessage).TotalSeconds;

			string[] parts = Regex.Split(message, @"\r?\n");
			writer.Write("[{0:00.000}] [{1:00.000}] ", age, split);
			writer.WriteLine(parts[0].Trim());

			for (int i = 1; i < parts.Length; i++)
			{
				writer.Write("                  ");
				writer.WriteLine(parts[i]);
			}

			lastMessage = itemDate;
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing)
		{
			if (disposing)
			{

				if (_autoTraceTimer != null)
				{
					_autoTraceTimer.Change(Timeout.Infinite, Timeout.Infinite);
					_autoTraceTimer.Dispose();
					_autoTraceTimer = null;
				}

				_disposed = DateTime.UtcNow;

				if (ScopeStack.Count == 0) return;

				ScopeStack.RemoveAt(ScopeStack.Count - 1);
				if (ScopeStack.Count == 0)
                    ScopeStack = null;
				TraceMessage();
			}
		}

		private void TraceMessage()
		{
			try
			{
				KeyValuePair<int, Action<string>> tracePair = GetTraceMethod(Age.TotalMilliseconds);
				if (tracePair.Value == null) return;
				if (!_isRoot && !_forceTrace) return;

				tracePair.Value(ToString());
			}
			catch (Exception ex)
			{
				LogManager.GetLogger(GetType()).Error(ex.Message, ex);
			}
		}

		private KeyValuePair<int, Action<string>> GetTraceMethod(double millis)
		{
			foreach (var pair in _traceMethods.Reverse())
				if (millis >= pair.Key)
					return pair;

			return _traceMethods.FirstOrDefault();
		}

		#region Write Methods

		public static ITraceItem Trace(StackTrace trace)
		{
			string s = trace.ToString();
			if (s.Length > 200)
				s = s.Substring(0, 200);
			return Trace(s);
		}

		public static ITraceItem Trace(object o)
		{
			return Trace(o?.ToString() ?? "");
		}

		public static void TraceIf(bool condition, object o)
		{
			if (condition)
				Trace(o?.ToString() ?? "");
		}

		public static ITraceItem Trace(string format, params object[] args)
		{
			if (ScopeStack == null) return null;
			if (ScopeStack.Count == 0) return null;

			format = TryFormat(format, args);
			lock (CurrentScope._syncLock)
			{
				ATraceItem item = new ATraceItem(format);
				CurrentScope._ATraceItems.Add(item);
				return item;
			}
		}

		public static void TraceIf(bool condition, string format, params object[] args)
		{
			if (condition)
				Trace(format, args);
		}
		
		private static AsyncTraceScope CurrentScope
		{
			get
			{
				if (ScopeStack == null) return null;
				if (ScopeStack.Count == 0) return null;
				return ScopeStack[ScopeStack.Count - 1];
			}
		}

		private static string TryFormat(string format, object[] args)
		{
			try
			{
				if (format == null) return "";
				if (args != null && args.Length != 0)
					format = string.Format(format, args);
				
				return format;
			}
			catch
			{
				return format;
			}
		}

		#endregion
	}
}