using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using log4net.Core;

namespace WidgetSample
{
    public static class LoggingExtensions
    {
        public static void Notice(this ILog log, string message, params object[] args)
        {
            LoggingEventData data = new() { Message = message, Level = Level.Notice };

            if (args?.Length > 0)
            {
                try
                {
                    data.Message = string.Format(message, args);
                }
                catch (Exception ex)
                {
                    data.Message += $" (loggging format exception): {ex.Message}";
                }
            }

            log.Logger.Log(new LoggingEvent(data));
        }
    }
}
