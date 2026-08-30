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
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Printing;
using System.Text.Json;
using DiagnosticExplorer;
using DiagnosticExplorer.Logging;

namespace WidgetSample.Harness;

//Widget uses the DiagnosticManager.RegisterAsync method of registering itself with diagnostics
public partial class Widget : IDisposable, INotifyPropertyChanged
{
    private static readonly Random _rand = new Random();
    private static readonly string[] _names = ["Widget X", "Widget Y", "Widget Z", "Widget W"];
    private readonly int _id;
    private readonly SampleLogger _log;
    private DateTime _dateCreated;
    private string _name;
    private Point _size;

    public int Id
    {
        get { return _id; }
    }

    private int _someProp = 123;
    private string _privateString = "234";

    private decimal _myDecimal = 123.234m;

    public string FullName => $"{Name}({_id})";

    public WidgetConfig PrimaryConfig { get; } = new();
    public WidgetConfig SecondaryConfig { get; } = new();

    #region ConfigureDiagnostics

    // Invoked by Diagnostic Explorer assembly configuration discovery.
    internal static void ConfigureDiagnostics(IDiagConfigurator config)
    {
        config.Configure<Widget>(options =>
        {
            options.IncludeAll();
            options.Exclude(widget => widget.IgnoredProperty);
            options.Property(widget => widget.Name).AllowSet();
            options.Property(widget => widget._dateCreated).ShowElapsed();
            options.Property(widget => widget.PrimaryConfig).Expand(true).WithLabel("Widget Config").WithExpandedHover();
            options.Property(widget => widget.SecondaryConfig).Expand().WithDrillDownOnly().WithExpandedHover();

            options
                .Custom(
                    "Config",
                    projection =>
                    {
                        projection.Property(widget => widget.FullName);
                        projection.Property(widget => "Hello " + widget.FullName);
                        projection.Property("Primary config", widget => widget.PrimaryConfig).Expand();
                        projection.Property("Secondary config", widget => widget.SecondaryConfig).Expand();
                        projection.Property("Names", widget => _names).ListItems();
                    }
                )
                .WithDrillDownOnly()
                .WithExpandedHover();
        });

        config.ConfigureDrillDown<Widget>(options =>
        {
            options.Property("Some Prop", widget => widget._someProp);
            options.Property("Private String", widget => widget._privateString);
            options.Property(widget => widget._myDecimal);
            options.Property(widget => widget.Name).AllowSet();
            options.Property(widget => widget.DateCreated).WithCategory("Info").AllowSet();
            options.Property(widget => widget.Size).WithCategory("Info").AllowSet();
            // options.Route(
            // widget => $"{typeof(Widget).FullName}.{widget.FullName}",
            // LoggerNameMatchMode.Exact,
            // route => route.To("Widgets", "Widget Events")
            // );
        });
    }

    #endregion

    [DiagnosticMethod]
    public void Randomise()
    {
        Name = _names[_rand.Next(0, _names.Length)];
        DateCreated = DateTime.Now.AddMinutes(_rand.Next(0, 10000));
        Size = new Point(_rand.Next(), _rand.Next());
    }

    public void RefreshValues()
    {
        PrimaryConfig.RefreshValues(0.2m);
        _log.Info("{FullName} Refreshed values {@PrimaryConfig}", FullName, PrimaryConfig);
    }

    [DiagnosticMethod]
    public void Clear()
    {
        Name = null;
        DateCreated = DateTime.Now;
    }

    [DiagnosticProperty(Ignore = true)]
    public string IgnoredProperty
    {
        get { return "This value will not be exposed in diagnostics"; }
    }

    [DiagnosticProperty(AllowSet = true)]
    public string Name
    {
        get { return _name; }
        set
        {
            _name = value;
            OnPropertyChanged("Name");
        }
    }

    [DiagnosticProperty(AllowSet = true, FormatString = "{0:d MMM yyyy HH:mm:ss}", Category = "Info")]
    public DateTime DateCreated
    {
        get { return _dateCreated; }
        set
        {
            _dateCreated = value;
            OnPropertyChanged("DateCreated");
        }
    }

    [DiagnosticProperty(AllowSet = true, FormatString = "Located at {0}", Category = "Info")]
    public Point Size
    {
        get { return _size; }
        set
        {
            _size = value;
            OnPropertyChanged("Size");
        }
    }

    #region IDisposable Members

    public void Dispose()
    {
        Dispose(true);
    }

    #endregion

    #region INotifyPropertyChanged Members

    public event PropertyChangedEventHandler PropertyChanged;

    #endregion

    ~Widget()
    {
        Dispose(false);
    }

    private void OnPropertyChanged(string propertyName)
    {
        if (PropertyChanged != null)
            PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
    }

    protected void Dispose(bool disposing)
    {
        if (disposing)
            DiagnosticManager.Unregister(this);
    }

    public override string ToString()
    {
        return string.Format("Widget {0} ({1})", Id, Size);
    }

    [DiagnosticMethod]
    public void Reset()
    {
        Name = null;
        Size = new Point(0, 0);
        DateCreated = new DateTime(2000, 1, 1);
    }

    [DiagnosticMethod]
    public void Reset(string name, DateTime dateCreated)
    {
        Name = name;
        DateCreated = dateCreated;
    }
}
