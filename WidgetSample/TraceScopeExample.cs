using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DiagnosticExplorer;

namespace WidgetSample
{
	public static class TraceScopeExample
	{
		private static Random _rand = new Random();

		public static void TestTraceScope1()
		{
			using (new TraceScope())
			{
				int times = _rand.Next(1, 5);
				TraceScope.Trace("About to call TestTraceScope2() {0} times", times);
				for (int i = 0; i < times; i++)
					TestTraceScope2();

				TraceScope.Trace("Just called TestTraceScope2()");
			}
		}

		public static void TestTraceScope2()
		{
			using (new TraceScope())
			{
				if (_rand.Next(100) < 50)
					TestTraceScope2();

				int times = _rand.Next(1, 3);
				TraceScope.Trace("About to call TestTraceScope3() {0} times", times);
				for (int i = 0; i < times; i++)
					TestTraceScope3();
				TraceScope.Trace("Just called TestTraceScope3()");
			}
		}

		public static void TestTraceScope3()
		{
			using (new TraceScope())
			{
				if (_rand.Next(100) < 5)
					TestTraceScope2();

				TraceScope.Trace("About to call TestTraceScope4()");
				TestTraceScope4();
				TraceScope.Trace("Just called TestTraceScope4()");
			}
		}

		public static void TestTraceScope4()
		{
			using (new TraceScope())
			{
				TraceScope.Trace("Your lucky random number is {0}", _rand.Next());
				TraceScope.Trace(@"Heres a multiline trace message
which, as you can see,
has more than one line");
			}
		}
	}
}