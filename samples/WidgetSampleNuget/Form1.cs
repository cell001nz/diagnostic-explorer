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
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DiagnosticExplorer;
using DiagnosticExplorer.SelfHost;
using Microsoft.Extensions.Logging;
using Timer = System.Threading.Timer;

namespace WidgetSampleNuget;

//Only public properties with a PropertyAttribute will be exposed
[DiagnosticClass(AttributedPropertiesOnly = true, DeclaringTypeOnly = true)]
public partial class Form1 : Form, INotifyPropertyChanged
{
    private static readonly ILogger _gadgetLog = SampleLogging.GetLogger("Gadgets");
    private static readonly ILogger _widgetLog = SampleLogging.GetLogger("Widgets");
    private static readonly ILogger _formLog = SampleLogging.GetLogger(typeof(Form1).FullName);
    private static int _evtCount1;
    private static readonly Random _rand = new Random();
    private readonly BindingList<Gadget> _gadgets;
    private readonly BindingList<Widget> _widgets;
    private Timer _counterTimer;
    private Timer _evtTimer;

    private string _infoText;
    private Timer _listTestTimer;
    private Task _autoLogTask;
    private Timer _scopeTimer;
    private Task _scopeTask;
    private static DiagnosticSelfHost _selfHost;
    private static Task _selfHostStartTask;
    private bool _shutdownInProgress;

    private const bool _serverDiags = true;
    private const bool _selfHostDiags = true;

    private static bool RemoteDiagnosticsAreHostManaged() => true;

    public Form1()
    {
        InitializeComponent();

        Trace.Listeners.Add(new TextWriterTraceListener(Console.Error));
        Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

        /*            _autoLogTask = Task.Run(async () => {
                        while (true)
                        {
                            _formLog.Info($"Auto logging event {DateTime.Now:d MMM yyyy HH:ss:ss.ff}");
                            await Task.Delay(100);
                        }
                    });
        */
        StartDiagnostics();
        FormClosing += StopDiagnosticsOnClosing;

        //Exposure the remoting interface
        _gadgets = new BindingList<Gadget>();
        _widgets = new BindingList<Widget>();

        gadgetGrid.DataSource = _gadgets;
        widgetGrid.DataSource = _widgets;

        gadgetGrid.RowsRemoved += HandleGadgetRemoved;
        widgetGrid.RowsRemoved += HandleWidgetRemoved;

        UpdateList = new List<int> { 1, 2, 4, 5 };

        //RegisterAsync this class with diagnostics
        DiagnosticManager.Register(this, "Main Form", "Form 1");
        //			SendInitial();
        _evtTimer = new Timer(SendEvents, null, 1000, 1000);
        _counterTimer = new Timer(IncrementCount, null, 400, 400);
        _listTestTimer = new Timer(MungeNumbersList, null, 100, 100);

        txtContent.DataBindings.Add("Text", this, "InfoText", false, DataSourceUpdateMode.OnPropertyChanged);

        _scopeTimer = new Timer(x => DoScopeTimerCode(), null, 500, 500);
        _scopeTask = RunScopeTask();
    }

    private static void StartDiagnostics()
    {
        // To use the in-process viewer, uncomment this line and comment out StartRemoteDiagnostics().
        if (_selfHostDiags)
            StartSelfHostedDiagnostics();

        if (_serverDiags && !RemoteDiagnosticsAreHostManaged())
            StartRemoteDiagnostics();
    }

    private static void StartSelfHostedDiagnostics()
    {
        if (_selfHost != null || _selfHostStartTask != null)
            return;

        _selfHostStartTask = StartSelfHostedDiagnosticsAsync();
    }

    private static async Task StartSelfHostedDiagnosticsAsync()
    {
        try
        {
            _selfHost = await DiagnosticSelfHostingService.StartAsync(DiagnosticManager.CurrentConfiguration);
            if (_selfHost.IsEnabled)
                Debug.WriteLine($"Self-hosted diagnostics started at {_selfHost.Url}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
        finally
        {
            _selfHostStartTask = null;
        }
    }

    private static void StartRemoteDiagnostics()
    {
        DiagnosticHostingService.Start(DiagnosticManager.CurrentConfiguration);
    }

    private async void StopDiagnosticsOnClosing(object sender, FormClosingEventArgs e)
    {
        if (_shutdownInProgress)
        {
            e.Cancel = true;
            return;
        }

        _shutdownInProgress = true;
        e.Cancel = true;

        try
        {
            if (!RemoteDiagnosticsAreHostManaged())
                await DiagnosticHostingService.Stop();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
        try
        {
            await StopSelfHostedDiagnosticsAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
        finally
        {
            FormClosing -= StopDiagnosticsOnClosing;
            BeginInvoke(Close);
        }
    }

    private async void StopDiagnostics(object sender, EventArgs e)
    {
        if (_selfHostDiags)
            await StopSelfHostedDiagnosticsAsync();
        else if (!RemoteDiagnosticsAreHostManaged())
            await DiagnosticHostingService.Stop();
    }

    private static async Task StopSelfHostedDiagnosticsAsync()
    {
        Task selfHostStartTask = _selfHostStartTask;
        if (selfHostStartTask != null)
            await selfHostStartTask;

        if (_selfHost == null)
            return;

        DiagnosticSelfHost selfHost = _selfHost;
        _selfHost = null;
        await selfHost.StopAsync();
    }

    [ExtendedProperty]
    public Widget NullWidget => null;

    //		[CollectionProperty(CollectionMode.List, Category="Numbers")]
    public List<int> UpdateList { get; }

    [DiagnosticProperty(Category = "Gadgets", Description = "Max Gadeget Id")]
    public int GadgetIdCount { get; private set; }

    [DiagnosticProperty(Category = "Widgets")]
    public int WidgetIdCount { get; private set; }

    [DiagnosticProperty(AllowSet = true)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string InfoText
    {
        get { return _infoText; }
        set
        {
            _infoText = value;
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs("InfoText"));
        }
    }

    [DiagnosticProperty(AllowSet = true)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SetMePlease { get; set; }

    [DiagnosticProperty(AllowSet = false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Counter2 { get; set; }

    [RateProperty(Category = "Widgets", ExposeRate = false, ExposeTotal = true)]
    public RateCounter WidgetEvents { get; } = new RateCounter(5);

    [RateProperty(Category = "Gadgets", ExposeTotal = true, Description = "The rate of gadget events received")]
    public RateCounter GadgetEvents { get; } = new RateCounter(5);

    [CollectionList(Category = "All Gadgets")]
    public IList<Gadget> Gadgets
    {
        get { return _gadgets; }
    }

    [CollectionCategories(CategoryProperty = nameof(Widget.FullName))]
    public IList<Widget> Widgets
    {
        get { return _widgets; }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    private void MungeNumbersList(object o)
    {
        try
        {
            for (int i = 0; i < 10; i++)
            {
                for (int j = 0; j < 10; j++)
                    UpdateList.Add(_rand.Next(100));

                for (int j = 0; j < 10; j++)
                    UpdateList.RemoveAt(0);
            }
        }
        catch (Exception ex)
        {
            _formLog.LogError(ex, "An error occurred while updating the numbers list.");
        }
    }

    private void IncrementCount(object o)
    {
        SetMePlease++;
        Counter2++;
    }

    [DiagnosticMethod]
    public void SayHelloAsync(string caption, string message)
    {
        if (message == "throw")
            throw new ArgumentException("Ok, I'll throw");

        Action sayHello = () => MessageBox.Show(this, message, caption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
        BeginInvoke(sayHello);
    }

    [DiagnosticMethod]
    public string SayHelloSync(string caption, string message)
    {
        if (message == "throw")
            throw new ArgumentException("Ok, I'll throw");

        Stopwatch watch = Stopwatch.StartNew();
        Action sayHello = () => MessageBox.Show(this, message, caption, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
        Invoke(sayHello);
        return string.Format("User clicked Ok in {0:N1} seconds", watch.Elapsed.TotalSeconds);
    }

    [DiagnosticMethod]
    public string LogLotsOfStuff(string msg1, string msg2, string msg3, string msg4, string msg5, string msg6, string msg7)
    {
        string[] vals = { msg1, msg2, msg3, msg4, msg5, msg6, msg7 };
        string[] toLog = vals.Where(x => !string.IsNullOrEmpty(x)).ToArray();
        foreach (string msg in toLog)
            _formLog.LogInformation("{Message}", msg);
        return string.Format("Logged {0}/{1} messages", toLog.Length, vals.Length);
    }

    [DiagnosticMethod]
    public int GetRandomInt1()
    {
        return _rand.Next();
    }

    [DiagnosticMethod]
    public int GetRandomInt2()
    {
        return _rand.Next();
    }

    [DiagnosticMethod]
    public int GetRandomInt3()
    {
        return _rand.Next();
    }

    [DiagnosticMethod]
    public int GetRandomInt4()
    {
        return _rand.Next();
    }

    [DiagnosticMethod]
    public int GetRandomInt5()
    {
        return _rand.Next();
    }

    [DiagnosticMethod]
    public int GetRandomInt6()
    {
        return _rand.Next();
    }

    [DiagnosticMethod]
    public int GetRandomInt7()
    {
        return _rand.Next();
    }

    [DiagnosticMethod]
    public int GetRandomInt8()
    {
        return _rand.Next();
    }

    [DiagnosticMethod]
    public string RandomText()
    {
        return string.Join(Environment.NewLine, Enumerable.Range(1, _rand.Next(5, 100)).Select(_ => RandomLine()).ToArray());
    }

    [DiagnosticMethod]
    public async Task<string> RandomWord()
    {
        await Task.Delay(2_000);
        return GetRandomWord();
    }

    public string GetRandomWord()
    {
        return new string(Enumerable.Range(1, _rand.Next(1, 10)).Select(_ => _rand.Next(0, 26)).Select(x => (char)('A' + ((char)x))).ToArray());
    }

    [DiagnosticMethod]
    public string RandomLine()
    {
        return string.Join(" ", Enumerable.Range(1, _rand.Next(1, 50)).Select(_ => GetRandomWord()).ToArray());
    }

    private void SendEvents(object o)
    {
        if (chkSystem.Checked)
            _ = RunScopedTraceExampleAsync(_formLog, $"Form Trace Scope {_evtCount1++}");

        if (chkWidgets.Checked)
            _ = RunScopedTraceExampleAsync(_widgetLog, $"Widget Trace Scope {_evtCount1++}");

        if (chkGadgets.Checked)
            _ = RunScopedTraceExampleAsync(_gadgetLog, $"Gadget Trace Scope {_evtCount1++}");
    }

    private void SendInitial()
    {
        for (int i = 0; i < 10; i++)
        {
            _ = RunScopedTraceExampleAsync(_formLog, $"Form Trace Scope {_evtCount1++}");

            _ = RunScopedTraceExampleAsync(_widgetLog, $"Widget Trace Scope {_evtCount1++}");

            _ = RunScopedTraceExampleAsync(_gadgetLog, $"Gadget Trace Scope {_evtCount1++}");
        }
    }

    private static Task RunScopedTraceExampleAsync(ILogger log, string message)
    {
        return Task.Run(async () =>
        {
            try
            {
                using var scope = new TraceScope(message => log.LogInformation("{Message}", message));
                TraceScope.Trace(message);
                await TraceScopeExample.TestTraceScope1();
            }
            catch (Exception ex)
            {
                log.LogError(ex, "The trace scope example failed.");
            }
        });
    }

    private void HandleGadgetRemoved(object sender, DataGridViewRowsRemovedEventArgs e)
    {
        _gadgetLog.LogInformation("A gadget was removed");
        _formLog.LogInformation("Form1 removed a gadget");
        GadgetEvents.Register(1);

        //Force a garbage collection to get the removed gadget out of diagnostics
        //If we had a handle to the removed item we could do this much better
        //by disposing it
        GC.Collect();
    }

    private void HandleWidgetRemoved(object sender, DataGridViewRowsRemovedEventArgs e)
    {
        _widgetLog.LogInformation("A widget was removed");
        _formLog.LogInformation("Form1 removed a widget");
        WidgetEvents.Register(1);

        //Read comment in HandleGadgetRemoved above
        GC.Collect();
    }

    private void bAddGadget_Click(object sender, EventArgs e)
    {
        Gadget gadget = new Gadget(GadgetIdCount++);
        _gadgets.Add(gadget);
        _gadgetLog.LogInformation("Added gadget {GadgetId}", gadget.Id);
        _formLog.LogInformation("Form1 added a gadget");
        GadgetEvents.Register(1);
    }

    private void bAddWidget_Click(object sender, EventArgs e)
    {
        Widget widget = new Widget(WidgetIdCount++);
        _widgets.Add(widget);
        _widgetLog.LogInformation("Added widget {WidgetId}", widget.Id);
        _formLog.LogInformation("Form1 added a widget");
        WidgetEvents.Register(1);
    }

    private void bMinorProblem_Click(object sender, EventArgs e)
    {
        try
        {
            string hello = "Hello";
            string name = hello.Substring(6, 10);
            Debug.WriteLine("The name is " + name);
        }
        catch (Exception ex)
        {
            string msg = "Info something went a little wrong.";
            _formLog.LogInformation(ex, "{Message}", msg);
            // MessageBox.Show(this, msg + "  Check diagnostics for a full stack trace.");
        }
    }

    private void bNotice_Click(object sender, EventArgs e)
    {
        try
        {
            string hello = "Hello";
            string name = hello.Substring(6, 10);
            Debug.WriteLine("The name is " + name);
        }
        catch (Exception ex)
        {
            string msg = "Notice something went a little wrong.";
            _formLog.LogInformation(ex, "{Message}", msg);
            // MessageBox.Show(this, msg + "  Check diagnostics for a full stack trace.");
        }
    }

    private void bWarn_Click(object sender, EventArgs e)
    {
        try
        {
            string hello = "Hello";
            string name = hello.Substring(6, 10);
            Debug.WriteLine("The name is " + name);
        }
        catch (Exception ex)
        {
            string msg = "Warn something went a little wrong.";
            _formLog.LogWarning(ex, "{Message}", msg);
            // MessageBox.Show(this, msg + "  Check diagnostics for a full stack trace.");
        }
    }

    private void bHorrificException_Click(object sender, EventArgs e)
    {
        try
        {
            decimal div = 0;
            decimal result = 12.345M / div;
            Debug.WriteLine("The result is " + result);
        }
        catch (Exception ex)
        {
            string msg = "OMFG the whole app just went BOOM";
            _formLog.LogError(ex, "{Message}", msg);
            // MessageBox.Show(this, msg + ".  Check diagnostics for a full stack trace.");
        }
    }

    private void bRemoveGadget_Click(object sender, EventArgs e)
    {
        RemoveItem(_gadgets, _gadgetLog);
    }

    private void bRemoveWidget_Click(object sender, EventArgs e)
    {
        RemoveItem(_widgets, _widgetLog);
    }

    private void RemoveItem<T>(BindingList<T> items, ILogger log)
    {
        try
        {
            int index = _rand.Next(0, items.Count);
            items.RemoveAt(index);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "The item could not be removed.");
            // MessageBox.Show(this, "Error removing item, check diagnostics for more details.");
        }
    }

    private async void btnTraceScope_Click(object sender, EventArgs e)
    {
        using (new TraceScope(message => _formLog.LogInformation("{Message}", message)))
        {
            TraceScope.Trace($"In Trace Scope Button Click 1 InvokeRequired: {InvokeRequired}");

            Task task1 = Task.Run(async () =>
            {
                await Task.Delay(100);
                TraceScope.Trace("In the async bit A1");

                await TraceScopeExample.TestTraceScope1();

                await Task.Delay(100);
                TraceScope.Trace("In the async bit A2");
            });

            Task task2 = Task.Run(async () =>
            {
                await Task.Delay(100);
                TraceScope.Trace("In the async bit B1");
                await Task.Delay(100);
                TraceScope.Trace("In the async bit B2");
            });

            await task1;
            await task2;

            await Task.Delay(1000);
            // await TraceScopeExample.TestTraceScope1();
        }

        // MessageBox.Show("Just generated a trace scope.  Check diagnostics.");
    }

    private async void btnTestTraceScope2_Click(object sender, EventArgs e)
    {
        using var scope = new TraceScope(
            "UI_ACTION_RoutingModel_SendAll",
            message => _formLog.LogInformation("{Message}", message),
            forceTrace: true
        );

        TraceScope.Trace($"In Trace Scope Button Click 2 InvokeRequired: {InvokeRequired}");
        // await TraceScopeExample.TestTraceScope1();

        Report("Starting");

        await Task.Delay(100);
        Report("In the async bit A");

        using SemaphoreSlim throttle = new SemaphoreSlim(3);
        IEnumerable<Task> parallelTasks = Enumerable
            .Range(1, 20)
            .Select(async x =>
            {
                await throttle.WaitAsync();
                try
                {
                    List<string> ids = new();
                    ids.Add(Task.CurrentId?.ToString() ?? "X");
                    using var scope2 = new TraceScope("Doing the parallel bit");
                    Report($"Parallel...{x}...A");
                    ids.Add(Task.CurrentId?.ToString() ?? "X");
                    await Task.Delay(100);
                    ids.Add(Task.CurrentId?.ToString() ?? "X");

                    await Task.Run(async () =>
                    {
                        ids.Add(Task.CurrentId?.ToString() ?? "X");
                        Report($"Inner task {x} Q");
                        await Task.Delay(100);
                        ids.Add(Task.CurrentId?.ToString() ?? "X");
                        Report($"Inner task {x} W");
                    });

                    ids.Add(Task.CurrentId?.ToString() ?? "X");
                    Report($"Parallel...{x}...B [" + string.Join(", ", ids) + "]");
                }
                finally
                {
                    throttle.Release();
                }
            });

        await Task.WhenAll(parallelTasks);

        await Task.Delay(100);
        Report("In the async bit B");
        Report("Finished");

        await Task.Delay(1000);
    }

    static void Report(string message)
    {
        TraceScope.Trace($"REPORT {Task.CurrentId} {message}");
        Trace.WriteLine($"REPORT {Task.CurrentId} {message}");
    }

    private async Task RunScopeTask()
    {
        while (true)
        {
            // using (var scope = new TraceScope("SYNC BLAH 1"))
            {
                string message =
                    $"�$%�$%�$%�$%�$%�$%�$%�$%�$%�$% SCOPE TASK {InvokeRequired} {DateTime.Now:d MMM yyyy HH:mm:ss} �$%�$%�$%�$%�$%�$%�$%�$%�$%�$%";
                TraceScope.Trace(message);
            }
            // using (var scope = new AsyncTraceScope("ASYNC BLAH 1"))
            {
                string message =
                    $"�$%�$%�$%�$%�$%�$%�$%�$%�$%�$% SCOPE TASK {InvokeRequired} {DateTime.Now:d MMM yyyy HH:mm:ss} �$%�$%�$%�$%�$%�$%�$%�$%�$%�$%";
                TraceScope.Trace(message);
            }

            await Task.Delay(500);
        }
    }

    private void DoScopeTimerCode()
    {
        Invoke(() =>
        {
            using (var scope = new TraceScope("SYNC BLAH 2"))
            {
                string message =
                    $"�$%�$%�$%�$%�$%�$%�$%�$%�$%�$% SCOPE TIMER {InvokeRequired} {DateTime.Now:d MMM yyyy HH:mm:ss} �$%�$%�$%�$%�$%�$%�$%�$%�$%�$% ";
                TraceScope.Trace(message);
            }
        });
        Invoke(() =>
        {
            using (var scope = new TraceScope("ASYNC BLAH 2"))
            {
                string message =
                    $"�$%�$%�$%�$%�$%�$%�$%�$%�$%�$% SCOPE TIMER {InvokeRequired} {DateTime.Now:d MMM yyyy HH:mm:ss} �$%�$%�$%�$%�$%�$%�$%�$%�$%�$% ";
                TraceScope.Trace(message);
            }
        });
    }

    private void btn10_Click(object sender, EventArgs e)
    {
        GenerateEvents(10);
    }

    private void btn100_Click(object sender, EventArgs e)
    {
        GenerateEvents(100);
    }

    private void btn1000_Click(object sender, EventArgs e)
    {
        GenerateEvents(1000);
    }

    private async void GenerateEvents(int count)
    {
        Cursor = Cursors.WaitCursor;
        try
        {
            Stopwatch watch = Stopwatch.StartNew();
            await Task.Run(() =>
            {
                for (int i = 0; i < count; i++)
                {
                    _formLog.Log(IntToLogLevel(_rand.Next(1, 12) * 10000), "Event #{EventNumber}", i);
                    // await Task.Delay(TimeSpan.FromMilliseconds(5));
                }
            });

            Debug.WriteLine($"Send {count} messages took {watch.ElapsedMilliseconds}ms");
        }
        finally
        {
            Cursor = DefaultCursor;
        }
    }

    private void btnStartHosting_Click(object sender, EventArgs e)
    {
        StartDiagnostics();
    }

    private static LogLevel IntToLogLevel(int value)
    {
        if (value >= 90000)
            return LogLevel.Critical;
        if (value >= 70000)
            return LogLevel.Error;
        if (value >= 60000)
            return LogLevel.Warning;
        if (value >= 50000)
            return LogLevel.Information;
        if (value >= 40000)
            return LogLevel.Information;
        if (value >= 30000)
            return LogLevel.Debug;
        return LogLevel.Trace;
    }
}
