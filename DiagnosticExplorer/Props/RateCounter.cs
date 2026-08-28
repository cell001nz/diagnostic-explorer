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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Timers;

namespace DiagnosticExplorer;

public class RateCounter
{
    private static readonly Timer _timer;
    private static readonly List<WeakReference> _counters = new List<WeakReference>();
    private long _lastCheck = Stopwatch.GetTimestamp();
    private readonly int[] _counts;
    private readonly TimeSpan[] _times;
    private int _index;

    static RateCounter()
    {
        _timer = new Timer();
        _timer.Elapsed += Run;
        _timer.Interval = 1000;
    }

    public RateCounter(byte secondsAverage)
    {
        if (secondsAverage <= 0)
            secondsAverage = 5;

        _counts = new int[secondsAverage];
        _times = new TimeSpan[secondsAverage];

        lock (_counters)
        {
            _counters.Add(new WeakReference(this));
            if (_counters.Count == 1)
                _timer.Start();
        }
    }

    private void Increment()
    {
        lock (_counts)
        {
            long timestamp = Stopwatch.GetTimestamp();
            _times[_index % _counts.Length] = TimeSpan.FromSeconds((timestamp - _lastCheck) / (double)Stopwatch.Frequency);
            CalcRate();
            _index++;
            _counts[_index % _counts.Length] = 0;
            _times[_index % _counts.Length] = TimeSpan.Zero;
            _lastCheck = timestamp;
        }
    }

    private void CalcRate()
    {
        double r = _counts.Sum();
        TimeSpan totalTime = _times.Aggregate((t1, t2) => t1 + t2);

        if (totalTime == TimeSpan.Zero)
            Rate = 0;
        else
            Rate = r / totalTime.TotalSeconds;
    }

    public void Register(int count)
    {
        if (count <= 0)
            return;

        lock (_counts)
        {
            Total += (ulong)count;
            _counts[_index % _counts.Length] += count;
        }
    }

    public double Rate { get; private set; }

    public ulong Total { get; private set; }

    private static void Run(object state, ElapsedEventArgs e)
    {
        try
        {
            lock (_counters)
            {
                for (int i = _counters.Count - 1; i >= 0; i--)
                {
                    WeakReference r = _counters[i];
                    RateCounter counter = (RateCounter)r.Target;
                    if (counter == null)
                        _counters.RemoveAt(i);
                    else
                        counter.Increment();
                }
                if (_counters.Count == 0)
                    _timer.Stop();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }
}
