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
using System.Text.Json;
using DiagnosticExplorer;
using DiagnosticExplorer.Logging;

namespace WidgetSample.Harness;

//Widget extends DiagnosticManager in order to register itself with diagnostics
public partial class Gadget : INotifyPropertyChanged
{
    private static Random _rand = new Random();
    public event PropertyChangedEventHandler PropertyChanged;

    [DiagnosticMethod]
    public void Randomise()
    {
        Name = GetRandom(_names);
        Purpose = GetRandom(_purposes);
    }

    [DiagnosticMethod]
    public void Clear()
    {
        Name = null;
        Purpose = null;
    }

    public void RefreshValues()
    {
        Configuration.RefreshValues(0.2m);
        _log.Info($"{FullName} Refreshed values " + JsonSerializer.Serialize(Configuration, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void OnPropertyChanged(string propertyName)
    {
        if (PropertyChanged != null)
            PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
    }

    private string GetRandom(string[] items)
    {
        int index = _rand.Next(0, items.Length);
        return items[index];
    }

    private static string[] _names = new[] { "Gadget X", "Gadget Y", "Gadget Z", "Gadget W" };

    private static string[] _purposes = new[] { "Technical", "Muckabout", "Stuff" };

    public override string ToString()
    {
        return string.Format("Gadget {0}", Id);
    }

    public int Id { get; private set; }

    public string FullName => $"{Name}({Id})";

    public GadgetConfig Configuration { get; } = new();

    private string _name;

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

    private string _purpose;

    [DiagnosticProperty(AllowSet = true)]
    public string Purpose
    {
        get { return _purpose; }
        set
        {
            _purpose = value;
            OnPropertyChanged("Purpose");
        }
    }

    public static void ConfigureGadget(IDiagConfigurator config)
    {
        config.Configure<Gadget>(options =>
        {
            options.IncludeAll();
            options.Property(gadget => gadget.Name).AllowSet();
            options.Property(gadget => gadget.Purpose).AllowSet();
            options.Property(gadget => gadget.Configuration).Category("Configuration").Expand();
        });

        config.ConfigureDrillDown<Gadget>(options =>
        {
            options.IncludeAll();
            options.Property(gadget => gadget.Name).AllowSet();
            options.Property(gadget => gadget.Configuration).Named("Gadget Config").AsDrillDownIcon();
            options.Route(
                gadget => $"{typeof(Gadget).FullName}.{gadget.FullName}",
                LoggerNameMatchMode.Exact,
                route => route.To("Gadget", "Gadget Events")
            );
        });

        ConfigureGadgetConfig(config);
    }

    private static void ConfigureGadgetConfig(IDiagConfigurator config)
    {
        config.Configure<GadgetConfig>(options =>
        {
            options.IncludeAll();
            // options.Property(obj => obj.Power).Category("Power").Expand();
            options.Property(obj => obj.CommissionedOn).AsDateOnly();
            options.Property(obj => obj.Power).AsJson(100).WithJsonHover().WithDrillDown();
            options.Property("Network2", obj => obj.Network).AsJson(100).WithExpandedHover().WithDrillDown();
            options.Property(obj => obj.Network).Category("Network").Expand();
            options.Property(obj => obj.Maintenance).Category("Maintenance").Expand();
        });

        config.Configure<GadgetPowerConfig>(options =>
        {
            options.IncludeAll();
        });
        // config.Configure<GadgetNetworkConfig>(options => options.IncludeAll());
        // config.Configure<GadgetMaintenanceConfig>(options => options.IncludeAll());
    }
}
