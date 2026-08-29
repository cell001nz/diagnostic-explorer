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

using DiagnosticExplorer;

namespace WidgetSample.Harness;

public class Thing : ThingBase
{
    internal static void ConfigureDiagnostics(IDiagConfigurator config)
    {
        config.Configure<Thing>(options =>
        {
            options.ExcludeAll();
            options.Include(widget => widget.ThingValue2);
        });
    }

    public string ThingValue1 { get; set; } = "Value 1";
    public string ThingValue2 { get; set; } = "Value 2";
}

public class ThingBase : ThingSubBase
{
    public string BaseValue1 { get; set; } = "Base Value 1";
    public string BaseValue2 { get; set; } = "Base Value 2";
}

public class ThingSubBase
{
    internal static void ConfigureDiagnostics(IDiagConfigurator config)
    {
        config.Configure<ThingSubBase>(options =>
        {
            options.ExcludeAll();
            options.Include(widget => widget.SubBaseValue1);
        });
    }

    public string SubBaseValue1 { get; set; } = "Sub Base Value 1";
    public string SubBaseValue2 { get; set; } = "Sub Base Value 2";
}
