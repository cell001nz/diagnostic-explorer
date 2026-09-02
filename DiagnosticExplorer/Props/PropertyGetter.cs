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
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using DiagnosticExplorer.Util;

namespace DiagnosticExplorer;

internal class PropertyGetter
{
    public const int MaxConcatItems = 100;
    private Func<object, string> _nameFormatter;
    private Func<object, string> _categoryFormatter;
    private ConfiguredValue<bool> _categoryInitiallyExpanded;
    private ConfiguredValue<string> _categoryExpansionScope;
    private Func<object, string> _descriptionFormatter;
    private Func<object, string> _valueFormatter;
    private Func<object, string> _textFormatter;
    private IReadOnlyList<PropertyAlertConfiguration> _alerts;
    private IReadOnlyList<PropertyStatusConfiguration> _statuses;
    protected bool DrillDownEnabled { get; private set; }
    protected int DrillDownMaxItems { get; private set; }
    protected bool DrillDownIconOnly { get; private set; }
    protected string DrillDownText { get; private set; }
    private Func<object, string> _drillDownTextFormatter;
    protected bool JsonHoverEnabled { get; private set; }
    protected bool ExpandedHoverEnabled { get; private set; }
    protected bool NoTruncate { get; private set; }
    protected StatusIconSize StatusIconSize { get; private set; }
    protected bool IsJson { get; private set; }

    protected PropertyGetter() { }

    public PropertyGetter(PropertyInfo propInfo, bool isStatic)
        : this(propInfo, AttributeUtil.GetAttribute<DiagnosticPropertyAttribute>(propInfo), null, isStatic) { }

    public PropertyGetter(PropertyInfo propInfo, DiagnosticPropertyAttribute propAttr, bool isStatic)
        : this(propInfo, propAttr, null, isStatic) { }

    internal PropertyGetter(
        PropertyInfo propInfo,
        DiagnosticPropertyAttribute propAttr,
        PropertyConfiguration configuration,
        bool isStatic,
        bool applyAttributes = true,
        string defaultFormat = null,
        bool defaultDrillDown = false
    )
    {
        PropInfo = propInfo;

        if (propInfo != null)
        {
            GetFunc = PropertyToFunction(propInfo, isStatic);
            Name = propInfo.Name;

            if (applyAttributes)
            {
                DiagnosticClassAttribute classAttr = propInfo
                    .DeclaringType.GetCustomAttributes(typeof(DiagnosticClassAttribute), true)
                    .Cast<DiagnosticClassAttribute>()
                    .FirstOrDefault();

                if (classAttr != null && classAttr.AllPropertiesSettable)
                    CanSet = propInfo.CanWrite && classAttr.AllPropertiesSettable;
            }

            if (propAttr != null)
            {
                Name = propAttr.Name ?? Name;
                Category = propAttr.Category ?? Category;
                Description = propAttr.Description ?? Description;
                FormatString = propAttr.FormatString;
                if (propInfo.CanWrite && propAttr.AllowSetSpecified)
                    CanSet = propAttr.AllowSet;
            }
        }
        else if (configuration != null)
        {
            GetFunc = configuration.Value;
            Name = configuration.Name.Value;
        }

        if (configuration != null)
        {
            if (configuration.Name.IsSet)
                Name = configuration.Name.Value;
            _nameFormatter = configuration.NameFormatter;
            if (configuration.Category.IsSet)
                Category = configuration.Category.Value;
            _categoryFormatter = configuration.CategoryFormatter;
            _categoryInitiallyExpanded = configuration.CategoryInitiallyExpanded;
            _categoryExpansionScope = configuration.CategoryExpansionScope;
            if (configuration.Description.IsSet)
                Description = configuration.Description.Value;
            _descriptionFormatter = configuration.DescriptionFormatter;
            if (configuration.FormatString.IsSet)
                FormatString = configuration.FormatString.Value;
            _valueFormatter = configuration.ValueFormatter;
            _textFormatter = configuration.TextFormatter;
            if (configuration.IsJson.IsSet)
                IsJson = configuration.IsJson.Value;
            _alerts = configuration.Alerts;
            _statuses = configuration.Statuses;
            if (configuration.StatusIconSize.IsSet)
                StatusIconSize = configuration.StatusIconSize.Value;
            if (configuration.AllowSet.IsSet)
                CanSet = propInfo?.CanWrite == true && configuration.AllowSet.Value;
            ConfigureDrillDown(
                configuration.DrillDown,
                configuration.DrillDownMaxItems,
                configuration.DrillDownIconOnly,
                configuration.DrillDownText,
                configuration.DrillDownTextFormatter
            );
            ConfigureHover(configuration.JsonHover, configuration.ExpandedHover);
            if (configuration.NoTruncate.IsSet)
                NoTruncate = configuration.NoTruncate.Value;
        }

        if (FormatString == null && defaultFormat != null)
            FormatString = defaultFormat.Contains("{0") ? defaultFormat : "{0:" + defaultFormat + "}";
        else if (FormatString == null && propAttr != null)
            FormatString = GetDefaultFormatString(propInfo.PropertyType);

        if (defaultDrillDown)
        {
            DrillDownEnabled = true;
            DrillDownIconOnly = true;
        }
    }

    protected Func<object, object> PropertyToFunction(PropertyInfo propInfo, bool isStatic)
    {
        if (propInfo == null)
            return null;

        try
        {
            //return obj => propInfo.GetValue(obj, null);

            //This method takes 2/3 time of propInfo.GetValue
            if (isStatic)
                return obj => propInfo.GetValue(obj, null);

            ParameterExpression objParam = Expression.Parameter(typeof(object), "obj");
            UnaryExpression objToType = Expression.Convert(objParam, propInfo.DeclaringType);
            Expression propExp = Expression.Property(objToType, propInfo);
            Expression resultToObj = Expression.Convert(propExp, typeof(object));
            return (Func<object, object>)Expression.Lambda(resultToObj, objParam).Compile();
        }
        catch (Exception ex)
        {
            string msg = string.Format("Property {0}.{1}: {2}", propInfo.DeclaringType.Name, propInfo.Name, ex.Message);
            return obj => msg;
        }
    }

    protected Func<object, object> GetFunc { get; set; }

    protected static string MaxLengthString(string s, int maxLength)
    {
        if (s == null)
            return s;
        if (s.Length <= maxLength)
            return s;

        return s.Substring(0, maxLength);
    }

    public virtual void GetProperties(object obj, PropertyBag bag, string catPrepend)
    {
        string value = GetValue(obj, out object objectValue);
        Property p = new Property
        {
            Name = GetName(obj),
            Description = GetDescription(obj),
            Value = MaxLengthString(GetText(obj, value), 8092),
            ValueObject = objectValue,
            CanSet = CanSet,
            Alerts = GetAlerts(obj),
            Statuses = GetStatuses(obj),
            StatusIconSize = StatusIconSize,
            IsJson = IsJson,
            SourceObject = obj,
            SourceProperty = PropInfo,
        };
        ApplyDrillDown(p, objectValue, obj);
        if (p.DrillDownIconOnly)
            p.Value = null;

        string prependToCategory = PrependToCategory(catPrepend, obj);
        bag.AddProperty(p, prependToCategory);
        if (
            _categoryInitiallyExpanded.IsSet
            && _categoryExpansionScope.IsSet
            && string.Equals(
                CategoryExtensions.NormalizeName(prependToCategory),
                CategoryExtensions.NormalizeName(_categoryExpansionScope.Value),
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            bag.FindOrCreateCategory(prependToCategory).IsExpanded = _categoryInitiallyExpanded.Value;
        }
    }

    protected string PrependToCategory(string prepend, object obj)
    {
        return CombineCategories(prepend, GetCategory(obj));
    }

    protected string PrependToCategory(string prepend)
    {
        return CombineCategories(prepend, Category);
    }

    protected virtual string GetName(object obj) => GetFormattedMetadata(_nameFormatter, obj, Name);

    protected virtual string GetDescription(object obj) => GetFormattedMetadata(_descriptionFormatter, obj, Description);

    protected virtual string GetCategory(object obj) => GetFormattedMetadata(_categoryFormatter, obj, Category);

    private string GetText(object obj, string value)
    {
        if (_textFormatter == null)
            return value;

        try
        {
            return _textFormatter(obj);
        }
        catch (Exception ex)
        {
            return $"<{ex.Message}>";
        }
    }

    internal virtual bool IsDirectProperty => true;

    internal bool IsInGeneralCategory(object obj) => CategoryExtensions.NormalizeName(GetCategory(obj)) == null;

    protected List<PropertyAlert> GetAlerts(object obj)
    {
        if (_alerts == null || _alerts.Count == 0)
            return null;

        List<PropertyAlert> activeAlerts = new();
        Dictionary<string, int> alertIndexes = new(StringComparer.Ordinal);
        foreach (PropertyAlertConfiguration alert in _alerts)
        {
            try
            {
                if (alert.Condition(obj))
                {
                    string message = alert.Message(obj);
                    string category = alert.Category == null ? message : alert.Category(obj);
                    AddWorstAlert(activeAlerts, alertIndexes, new PropertyAlert(alert.Severity, message, category));
                }
            }
            catch (Exception ex)
            {
                string message = $"<{ex.Message}>";
                AddWorstAlert(activeAlerts, alertIndexes, new PropertyAlert(PropertyAlertSeverity.Error, message));
                break;
            }
        }

        return activeAlerts.Count == 0 ? null : activeAlerts;
    }

    protected List<PropertyStatus> GetStatuses(object obj)
    {
        if (_statuses == null || _statuses.Count == 0)
            return null;

        List<PropertyStatus> activeStatuses = new();
        foreach (PropertyStatusConfiguration status in _statuses)
        {
            try
            {
                if (status.Condition(obj))
                    activeStatuses.Add(new PropertyStatus(status.Status, status.Text(obj)));
            }
            catch (Exception ex)
            {
                activeStatuses.Add(new PropertyStatus(StatusCode.Error, $"<{ex.Message}>"));
                break;
            }
        }

        return activeStatuses.Count == 0 ? null : activeStatuses;
    }

    private static void AddWorstAlert(List<PropertyAlert> alerts, IDictionary<string, int> indexes, PropertyAlert alert)
    {
        string category = alert.Category ?? string.Empty;
        if (indexes.TryGetValue(category, out int index))
        {
            if (alert.Severity > alerts[index].Severity)
                alerts[index] = alert;
            return;
        }

        indexes.Add(category, alerts.Count);
        alerts.Add(alert);
    }

    private static string GetFormattedMetadata(Func<object, string> formatter, object obj, string fallback)
    {
        if (formatter == null)
            return fallback;

        try
        {
            return formatter(obj);
        }
        catch (Exception ex)
        {
            return $"<{ex.Message}>";
        }
    }

    protected static string CombineCategories(string start, string end)
    {
        start = CategoryExtensions.NormalizeName(start);
        end = CategoryExtensions.NormalizeName(end);
        if (string.IsNullOrEmpty(start))
            return end;

        if (string.IsNullOrEmpty(end))
            return start;

        return start + "." + end;
    }

    private string GetDefaultFormatString(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            type = type.GetGenericArguments()[0];

        if (type == typeof(float))
            return "{0:N2}";

        if (type == typeof(double))
            return "{0:N2}";

        if (type == typeof(decimal))
            return "{0:N2}";

        if (type == typeof(DateTime) || type == typeof(DateTime?))
            return "{0:d MMM yyyy H:mm:ss}";

        return null;
    }

    public PropertyInfo PropInfo { get; private set; }
    public string Name { get; protected set; }
    public string Description { get; protected set; }
    protected string FormatString { get; set; }
    public bool CanSet { get; private set; }
    public string Category { get; protected set; }

    public string GetValue(object obj, out object objectValue)
    {
        return GetValue(obj, GetFunc, out objectValue);
    }

    protected string FormatEnumerable(IEnumerable col, string separator, int maxItems, bool includeCount = true)
    {
        IEnumerable<object> asObject = col.Cast<object>();
        int count = asObject.Count();
        if (count == 0)
            return "0 items";

        List<string> values = new List<string>();
        if (maxItems <= 0)
            maxItems = MaxConcatItems;

        int remaining = count - maxItems;

        foreach (object o in asObject.Take(maxItems))
            values.Add(FormatValue(o));

        if (remaining > 0)
            values.Add(string.Format("... ({0} more item{1})", remaining, remaining == 1 ? "" : "s"));

        string formattedValues = string.Join(separator, values.ToArray());
        if (!includeCount)
            return formattedValues;

        string countText = string.Format("{0} item{1}", count, count == 1 ? "" : "s");
        return countText + ": " + formattedValues;
    }

    public string GetValue(object obj, Func<object, object> propInfo, out object propertyValue)
    {
        try
        {
            propertyValue = propInfo(obj);
            if (propertyValue == null)
                return null;

            return FormatValue(propertyValue);
        }
        catch (Exception ex)
        {
            propertyValue = null;
            return string.Format("<{0}>", ex.Message);
        }
    }

    protected void ConfigureCustomProperty(CustomPropertyConfiguration configuration)
    {
        Name = configuration.Name;
        Category = configuration.Category.IsSet ? configuration.Category.Value : null;
        Description = configuration.Description.IsSet ? configuration.Description.Value : null;
        _categoryInitiallyExpanded = configuration.CategoryInitiallyExpanded;
        _categoryExpansionScope = configuration.CategoryExpansionScope;
        _valueFormatter = configuration.ValueFormatter;
        _alerts = configuration.Alerts;
        _statuses = configuration.Statuses;
        if (configuration.IsJson.IsSet)
            IsJson = configuration.IsJson.Value;
        ConfigureDrillDown(
            configuration.DrillDown,
            configuration.DrillDownMaxItems,
            configuration.DrillDownIconOnly,
            configuration.DrillDownText,
            configuration.DrillDownTextFormatter
        );
        ConfigureHover(configuration.JsonHover, configuration.ExpandedHover);
    }

    protected void ApplyDrillDown(Property property, object value, object owner)
    {
        bool canDrillDown = DiagnosticManager.IsDrillDownValue(value);
        if (!canDrillDown && (!JsonHoverEnabled || value == null))
            return;

        property.CanDrillDown = DrillDownEnabled && canDrillDown;
        property.DrillDownIconOnly = property.CanDrillDown && DrillDownIconOnly;
        property.DrillDownText = property.CanDrillDown ? GetDrillDownText(owner) : null;
        property.CanJsonHover = JsonHoverEnabled && value != null;
        property.CanExpandedHover = ExpandedHoverEnabled && canDrillDown;
        property.DrillDownObject = value;
        property.DrillDownMaxItems = DrillDownMaxItems;
    }

    private void ConfigureDrillDown(
        ConfiguredValue<bool> enabled,
        ConfiguredValue<int> maxItems,
        ConfiguredValue<bool> iconOnly,
        ConfiguredValue<string> text,
        Func<object, string> textFormatter = null
    )
    {
        DrillDownEnabled = enabled.IsSet && enabled.Value;
        DrillDownMaxItems = maxItems.IsSet ? maxItems.Value : DiagnosticManager.DrillDownMaxItems;
        DrillDownIconOnly = iconOnly.IsSet && iconOnly.Value;
        DrillDownText = text.IsSet ? text.Value : null;
        _drillDownTextFormatter = textFormatter;
    }

    private string GetDrillDownText(object owner)
    {
        if (_drillDownTextFormatter == null)
            return DrillDownText;

        try
        {
            return _drillDownTextFormatter(owner);
        }
        catch (Exception ex)
        {
            return $"<{ex.Message}>";
        }
    }

    private void ConfigureHover(ConfiguredValue<bool> jsonHover, ConfiguredValue<bool> expandedHover)
    {
        JsonHoverEnabled = jsonHover.IsSet && jsonHover.Value;
        ExpandedHoverEnabled = expandedHover.IsSet && expandedHover.Value;
    }

    protected string FormatValue(object val)
    {
        if (val == null)
            return null;

        if (_valueFormatter != null)
            return _valueFormatter(val);

        if (val is TimeSpan)
            return FormatTimeSpan((TimeSpan)val);

        if (val is string)
            return (string)val;

        if (val is IEnumerable)
            return FormatEnumerable((IEnumerable)val, Environment.NewLine, MaxConcatItems);

        if (FormatString != null)
            return string.Format(FormatString, val);

        return val.ToString();
    }

    protected static string FormatTimeSpan(TimeSpan span)
    {
        string sign = span < TimeSpan.Zero ? "-" : "";
        string format = "{0}{2:D2}:{3:D2}:{4:D2}";
        if (span.Days != 0)
            format = "{0}{1}.{2:D2}:{3:D2}:{4:D2}";

        if (Math.Abs(span.TotalSeconds) < 1)
            format += ".{5:D2}";

        return string.Format(
            format,
            sign,
            Math.Abs(span.Days),
            Math.Abs(span.Hours),
            Math.Abs(span.Minutes),
            Math.Abs(span.Seconds),
            Math.Abs(span.Milliseconds)
        );
    }
}
