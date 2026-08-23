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
using System.Linq;

namespace DiagnosticExplorer;

public enum CollectionMode
{
    /// <summary>The count of a collection property is exposed</summary>
    Count,

    /// <summary>The items in a collection property are concatenated together</summary>
    Concatenate,

    /// <summary>The items in a collection property are listed individually</summary>
    List,

    /// <summary>Each item in a collection property is exposed in its own category</summary>
    Categories,
}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public abstract class CollectionPropertyAttribute : DiagnosticPropertyAttribute
{
    protected CollectionPropertyAttribute() { }

    protected CollectionPropertyAttribute(string name, string category = null, string description = null)
        : base(name, category, description) { }

    internal abstract CollectionOptions CreateOptions();
}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class CollectionCountAttribute : CollectionPropertyAttribute
{
    public CollectionCountAttribute() { }

    public CollectionCountAttribute(string name, string category = null, string description = null)
        : base(name, category, description) { }

    internal override CollectionOptions CreateOptions() => new(CollectionMode.Count);
}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class CollectionConcatenateAttribute : CollectionPropertyAttribute
{
    public string ValueProperty { get; set; }
    public string Separator { get; set; }
    public int MaxItems { get; set; } = PropertyGetter.MaxConcatItems;

    public CollectionConcatenateAttribute() { }

    public CollectionConcatenateAttribute(string name, string category = null, string description = null)
        : base(name, category, description) { }

    internal override CollectionOptions CreateOptions() =>
        new(CollectionMode.Concatenate)
        {
            ValueProperty = ValueProperty,
            Separator = Separator,
            MaxItems = MaxItems,
        };
}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class CollectionListAttribute : CollectionPropertyAttribute
{
    public string NameProperty { get; set; }
    public string ValueProperty { get; set; }
    public string DescriptionProperty { get; set; }
    public Func<object, string> DescriptionFormatter { get; set; }
    public string CategoryProperty { get; set; }
    public Func<object, string> CategoryFormatter { get; set; }
    public int MaxItems { get; set; } = PropertyGetter.MaxConcatItems;

    public CollectionListAttribute() { }

    public CollectionListAttribute(string name, string category = null, string description = null)
        : base(name, category, description) { }

    internal override CollectionOptions CreateOptions() =>
        new(CollectionMode.List)
        {
            NameProperty = NameProperty,
            ValueProperty = ValueProperty,
            DescriptionProperty = DescriptionProperty,
            CategoryProperty = CategoryProperty,
            MaxItems = MaxItems,
        };
}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class CollectionCategoriesAttribute : CollectionPropertyAttribute
{
    public string CategoryProperty { get; set; }
    public int MaxItems { get; set; } = PropertyGetter.MaxConcatItems;

    public CollectionCategoriesAttribute() { }

    public CollectionCategoriesAttribute(string name, string category = null, string description = null)
        : base(name, category, description) { }

    internal override CollectionOptions CreateOptions() =>
        new(CollectionMode.Categories) { CategoryProperty = CategoryProperty, MaxItems = MaxItems };
}

internal sealed class CollectionOptions
{
    public CollectionOptions(CollectionMode mode)
    {
        Mode = mode;
        MaxItems = PropertyGetter.MaxConcatItems;
    }

    public CollectionMode Mode { get; set; }
    public string NameProperty { get; set; }
    public Func<object, string> NameFormatter { get; set; }
    public string ValueProperty { get; set; }
    public Func<object, string> ValueFormatter { get; set; }
    public string DescriptionProperty { get; set; }
    public Func<object, string> DescriptionFormatter { get; set; }
    public string CategoryProperty { get; set; }
    public Func<object, string> CategoryFormatter { get; set; }
    public string Separator { get; set; }
    public int MaxItems { get; set; }
}
