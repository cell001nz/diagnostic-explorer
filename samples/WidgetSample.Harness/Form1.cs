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
using Timer = System.Threading.Timer;

namespace WidgetSample.Harness;

//Only public properties with a DiagnosticPropertyAttribute will be exposed
[DiagnosticClass(AttributedPropertiesOnly = true, DeclaringTypeOnly = true)]
public partial class Form1 : Form, INotifyPropertyChanged
{
    private static int _evtCount1;
    private static readonly Random _rand = new Random();
    private readonly BindingList<Gadget> _gadgets;
    private readonly BindingList<Widget> _widgets;
    private Timer _counterTimer;
    private Timer _evtTimer;

    private string _infoText;
    private Timer _configTimer;
    private Timer _listTestTimer;
    private Task _autoLogTask;

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
        //Exposure the remoting interface
        _gadgets = new BindingList<Gadget>();
        _widgets = new BindingList<Widget>();

        gadgetGrid.DataSource = _gadgets;
        widgetGrid.DataSource = _widgets;

        gadgetGrid.RowsRemoved += HandleGadgetRemoved;
        widgetGrid.RowsRemoved += HandleWidgetRemoved;

        UpdateList = new List<int> { 1, 2, 4, 5 };

        //			SendInitial();
        _evtTimer = new Timer(SendEvents, null, 1000, 1000);
        _counterTimer = new Timer(IncrementCount, null, 400, 400);
        _listTestTimer = new Timer(MungeNumbersList, null, 100, 100);
        _configTimer = new Timer(RefreshWidgetsAndGadgets, null, 750, 1000);
        FormClosing += (_, _) => _configTimer.Dispose();

        txtContent.DataBindings.Add("Text", this, "InfoText", false, DataSourceUpdateMode.OnPropertyChanged);

        Shown += HandleFormShown;
    }

    private void HandleFormShown(object sender, EventArgs e)
    {
        AddGadget();
        AddGadget();
        AddWidget();
        AddWidget();
    }

    [ExtendedProperty]
    public Widget NullWidget => null;

    //		[CollectionList(Category="Numbers")]
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
    public long SetMePlease { get; set; } = -1234L;

    [DiagnosticProperty(AllowSet = false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Counter2 { get; set; }

    [RateProperty(Category = "Widgets", ExposeRate = false, ExposeTotal = true)]
    public RateCounter WidgetEvents { get; } = new RateCounter(5);

    [RateProperty(Category = "Gadgets", ExposeTotal = true, Description = "The rate of gadget events received")]
    public RateCounter GadgetEvents { get; } = new RateCounter(5);

    [CollectionList(Category = "All Gadgets")]
    public ICollection<Gadget> Gadgets
    {
        get { return _gadgets; }
    }

    [CollectionCategories(CategoryProperty = nameof(Widget.FullName))]
    public ICollection<Widget> Widgets
    {
        get { return _widgets; }
    }

    private ICollection<Widget> GetWidgets() => _widgets.ToArray();

    internal static void ConfigureDiagnostics(IDiagConfigurator config)
    {
        config.Configure<Form1>(options =>
        {
            options.ExcludeAll();
            options.Property(form => form.NullWidget).Category("NullWidget category").Expand();
            options.Property(form => form.InfoText).Named("Blah INFOTEXT").AllowSet();
            options.Property(form => form.SetMePlease).AllowSet();
            options.Property(form => form.Counter2);
            options.Property(form => form._infoText);
            options.Property("Widget", form => form._widgets.Count);

            options
                .Property("WidgetCount", form => form.Widgets.Count)
                .Warn(form => form.Widgets.Count > 2, "Not too many widgets", "Widget count")
                .Error(form => form.Widgets.Count > 4, "Too many widgets", "Widget count");

            options
                .Property("Computed", form => $"This form has {form.Controls.Count} controls")
                .Description(form => $"Control Info for {form.GetHashCode()}");

            options.Property("Widget inventory", form => form.Widgets).WithDrillDown(maxItems: 25);
            options.Property(form => form.Widgets).SectionByItem(obj => obj.PrimaryConfig.EnvironmentName);

            using (options.CreateCategoryScope("Widgets"))
            {
                options.Property(form => form.WidgetIdCount);
                options.Property(form => form.WidgetEvents).ShowRate(false).ShowTotal();
                options.Property("Widgety Things", form => form.Widgets).AsDrillDown();
                options
                    .Property("Widgets from method", form => form.GetWidgets())
                    .ListItems(config => config.Name(obj => obj.FullName))
                    .AsDrillDownIcon("Click for more info");
            }

            using (options.CreateCategoryScope("Gadgets"))
            {
                options.Property(form => form.GadgetIdCount).Description("Max Gadget Id");
                options.Property(form => form.GadgetEvents).Description("The rate of gadget events received").ShowRate().ShowTotal();
            }

            using (options.CreateCategoryScope("All Gadgets"))
            {
                options
                    .Property("Gadgety Things", form => form.Gadgets)
                    .ListItems(options =>
                        options
                            .Name(gadget => $"{gadget.Id} - {gadget.Name}")
                            .Category(gadget => gadget.Purpose)
                            .Description(gadget => $"Description for {gadget.Name}")
                    )
                    .WithMaxItems(int.MaxValue)
                    .AsDrillDown();
            }
        });
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
            _formLog.Error(ex);
        }
    }

    private void IncrementCount(object o)
    {
        SetMePlease++;
        Counter2++;
    }

    private void RefreshWidgetsAndGadgets(object state)
    {
        foreach (Widget widget in _widgets.ToArray())
            widget.RefreshValues();

        foreach (Gadget gadget in _gadgets.ToArray())
            gadget.RefreshValues();
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
            _formLog.Info(msg);
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
            _ = RunScopedTraceExampleAsync(
                message => _formLog.Info(message),
                exception => _formLog.Error(exception),
                $"Form Trace Scope {_evtCount1++}"
            );

        if (chkWidgets.Checked)
            _ = RunScopedTraceExampleAsync(
                message => _widgetLog.Info(message),
                exception => _widgetLog.Error(exception),
                $"Widget Trace Scope {_evtCount1++}"
            );

        if (chkGadgets.Checked)
            _ = RunScopedTraceExampleAsync(
                message => _gadgetLog.Info(message),
                exception => _gadgetLog.Error(exception),
                $"Gadget Trace Scope {_evtCount1++}"
            );
    }

    private void SendInitial()
    {
        for (int i = 0; i < 10; i++)
        {
            _ = RunScopedTraceExampleAsync(
                message => _formLog.Info(message),
                exception => _formLog.Error(exception),
                $"Form Trace Scope {_evtCount1++}"
            );

            _ = RunScopedTraceExampleAsync(
                message => _widgetLog.Info(message),
                exception => _widgetLog.Error(exception),
                $"Widget Trace Scope {_evtCount1++}"
            );

            _ = RunScopedTraceExampleAsync(
                message => _gadgetLog.Info(message),
                exception => _gadgetLog.Error(exception),
                $"Gadget Trace Scope {_evtCount1++}"
            );
        }
    }

    private static Task RunScopedTraceExampleAsync(Action<string> info, Action<Exception> error, string message)
    {
        return Task.Run(async () =>
        {
            try
            {
                using var scope = new TraceScope(info);
                TraceScope.Trace(message);
                await TraceScopeExample.TestTraceScope1();
            }
            catch (Exception ex)
            {
                error(ex);
            }
        });
    }

    private void HandleGadgetRemoved(object sender, DataGridViewRowsRemovedEventArgs e)
    {
        _gadgetLog.Info("A gadget was removed");
        _formLog.Info("Form1 removed a gadget");
        GadgetEvents.Register(1);

        //Force a garbage collection to get the removed gadget out of diagnostics
        //If we had a handle to the removed item we could do this much better
        //by disposing it
        GC.Collect();
    }

    private void HandleWidgetRemoved(object sender, DataGridViewRowsRemovedEventArgs e)
    {
        _widgetLog.Info("A widget was removed");
        _formLog.Info("Form1 removed a widget");
        WidgetEvents.Register(1);

        //Read comment in HandleGadgetRemoved above
        GC.Collect();
    }

    private void bAddGadget_Click(object sender, EventArgs e) => AddGadget();

    private void AddGadget()
    {
        Gadget gadget = CreateGadget(GadgetIdCount++);
        _gadgets.Add(gadget);
        gadget.LogAdded();
        _formLog.Info("Form1 added a gadget");
        GadgetEvents.Register(1);
    }

    private partial Gadget CreateGadget(int id);

    private void bAddWidget_Click(object sender, EventArgs e) => AddWidget();

    private void AddWidget()
    {
        Widget widget = CreateWidget(WidgetIdCount++);
        _widgets.Add(widget);
        widget.LogAdded();
        _formLog.Info("Form1 added a widget");
        WidgetEvents.Register(1);
    }

    private partial Widget CreateWidget(int id);

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
            _formLog.Info(msg, ex);
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
            _formLog.Notice(msg, ex);
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
            _formLog.Warn(msg, ex);
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
            _formLog.Error(msg, ex);
            // MessageBox.Show(this, msg + ".  Check diagnostics for a full stack trace.");
        }
    }

    private void bRemoveGadget_Click(object sender, EventArgs e)
    {
        RemoveItem(_gadgets, _gadgetLog.Error);
    }

    private void bRemoveWidget_Click(object sender, EventArgs e)
    {
        RemoveItem(_widgets, _widgetLog.Error);
    }

    private void RemoveItem<T>(BindingList<T> items, Action<Exception> error)
    {
        try
        {
            int index = _rand.Next(0, items.Count);
            items.RemoveAt(index);
        }
        catch (Exception ex)
        {
            error(ex);
            // MessageBox.Show(this, "Error removing item, check diagnostics for more details.");
        }
    }

    private async void btnTraceScope_Click(object sender, EventArgs e)
    {
        using (new TraceScope(message => _formLog.Info(message)))
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
        using var scope = new TraceScope("UI_ACTION_RoutingModel_SendAll", message => _formLog.Info(message), forceTrace: true);

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
                    _formLog.Log(IntToSampleLogLevel(_rand.Next(1, 12) * 10000), $"Event #{i}");
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

    private static SampleLogLevel IntToSampleLogLevel(int value)
    {
        if (value >= 90000)
            return SampleLogLevel.Critical;
        if (value >= 70000)
            return SampleLogLevel.Error;
        if (value >= 60000)
            return SampleLogLevel.Warning;
        if (value >= 50000)
            return SampleLogLevel.Notice;
        if (value >= 40000)
            return SampleLogLevel.Information;
        if (value >= 30000)
            return SampleLogLevel.Debug;
        return SampleLogLevel.Trace;
    }
}
