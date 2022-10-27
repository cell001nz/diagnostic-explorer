
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DiagnosticExplorer.Log4Net
{
	/// <summary>
	/// Provides an abstraction for the system clock
	/// </summary>
	public static class SystemDateTime
    {
        /// <summary>
        /// By default returns the current date and time but can be set for
        /// unit testing purposes
        /// </summary>
        public static Func<DateTime> Now = () => DateTime.Now;
        public static Func<DateTime> UtcNow = () => DateTime.UtcNow;
    }
}
