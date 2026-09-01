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
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using ProtoBuf;

namespace DiagnosticExplorer;

public enum PropertyAlertSeverity
{
    None = 0,
    Warning = 1,
    Error = 2,
}

public enum StatusCode
{
    Active = 1,
    Inactive = 2,
    Pending = 3,
    Success = 4,
    Warning = 5,
    Error = 6,
    Alert = 7,
    Danger = 8,
    Running = 9,
    Stopped = 10,
    Disabled = 11,
    Paused = 12,
}

public enum StatusIconSize
{
    Small = 0,
    Medium = 1,
    Large = 2,
}

public enum PropertyValueKind
{
    Unspecified = 0,
    Null = 1,
    Text = 2,
    Boolean = 3,
    Number = 4,
    PositiveNumber = 5,
    ZeroNumber = 6,
    NegativeNumber = 7,
    DateTime = 8,
    Duration = 9,
    Enumeration = 10,
    Object = 11,
}

[ProtoContract(UseProtoMembersOnly = true)]
public class PropertyAlert
{
    public PropertyAlert() { }

    public PropertyAlert(PropertyAlertSeverity severity, string message)
        : this(severity, message, message) { }

    public PropertyAlert(PropertyAlertSeverity severity, string message, string category)
    {
        Severity = severity;
        Message = message;
        Category = category ?? message;
    }

    [ProtoMember(1)]
    public PropertyAlertSeverity Severity { get; set; }

    [ProtoMember(2)]
    public string Message { get; set; }

    [ProtoMember(3)]
    public string Category { get; set; }
}

[ProtoContract(UseProtoMembersOnly = true)]
public class PropertyStatus
{
    public PropertyStatus() { }

    public PropertyStatus(StatusCode status, string text)
    {
        Status = status;
        Text = text ?? status.ToString();
    }

    [ProtoMember(1)]
    public StatusCode Status { get; set; }

    [ProtoMember(2)]
    public string Text { get; set; }
}

[ProtoContract(UseProtoMembersOnly = true)]
public class Property
{
    public Property() { }

    public Property(string name)
        : this(name, null, null) { }

    public Property(string name, string value)
        : this(name, value, null) { }

    public Property(string name, string value, string description)
    {
        Name = name;
        Value = value;
        Description = description;
    }

    [ProtoMember(1)]
    public string Name { get; set; }

    [ProtoMember(2)]
    public string Value { get; set; }

    [ProtoMember(3)]
    public string Description { get; set; }

    [ProtoMember(4)]
    public string OperationSet { get; set; }

    [ProtoMember(5)]
    public bool CanSet { get; set; }

    [ProtoMember(6)]
    public List<PropertyAlert> Alerts { get; set; } = new();

    [ProtoMember(7)]
    public bool CanDrillDown { get; set; }

    [ProtoMember(8)]
    public bool DrillDownIconOnly { get; set; }

    [ProtoMember(9)]
    public PropertyValueKind ValueKind { get; set; }

    [ProtoMember(10)]
    public bool CanJsonHover { get; set; }

    [ProtoMember(11)]
    public bool CanExpandedHover { get; set; }

    [ProtoMember(12)]
    public string DrillDownText { get; set; }

    [ProtoMember(13)]
    public bool NoTruncate { get; set; }

    [ProtoMember(14)]
    public List<PropertyStatus> Statuses { get; set; } = new();

    [ProtoMember(15)]
    public StatusIconSize StatusIconSize { get; set; }

    internal object SourceObject { get; set; }

    internal object ValueObject { get; set; }

    internal object DrillDownObject { get; set; }

    internal int DrillDownMaxItems { get; set; }

    internal PropertyInfo SourceProperty { get; set; }

    internal void InferValueKind()
    {
        if (ValueKind != PropertyValueKind.Unspecified)
            return;

        if (ValueObject == null)
        {
            ValueKind = Value == null ? PropertyValueKind.Null : PropertyValueKind.Text;
            return;
        }

        if (ValueObject is string || ValueObject is char || ValueObject is Guid || ValueObject is Uri)
        {
            ValueKind = PropertyValueKind.Text;
            return;
        }

        if (ValueObject is bool)
        {
            ValueKind = PropertyValueKind.Boolean;
            return;
        }

        if (ValueObject is DateTime || ValueObject is DateTimeOffset)
        {
            ValueKind = PropertyValueKind.DateTime;
            return;
        }

        if (ValueObject is TimeSpan)
        {
            ValueKind = PropertyValueKind.Duration;
            return;
        }

        Type valueType = ValueObject.GetType();
        if (valueType.IsEnum)
        {
            ValueKind = PropertyValueKind.Enumeration;
            return;
        }

        if (IsNumeric(valueType))
        {
            ValueKind = GetNumericValueKind(ValueObject);
            return;
        }

        ValueKind = PropertyValueKind.Object;
    }

    private static bool IsNumeric(Type valueType)
    {
        switch (Type.GetTypeCode(valueType))
        {
            case TypeCode.Byte:
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Int32:
            case TypeCode.UInt32:
            case TypeCode.Int64:
            case TypeCode.UInt64:
            case TypeCode.Single:
            case TypeCode.Double:
            case TypeCode.Decimal:
                return true;
            default:
                return false;
        }
    }

    private static PropertyValueKind GetNumericValueKind(object value)
    {
        try
        {
            decimal number = Convert.ToDecimal(value);
            if (number > 0)
                return PropertyValueKind.PositiveNumber;
            if (number < 0)
                return PropertyValueKind.NegativeNumber;
            return PropertyValueKind.ZeroNumber;
        }
        catch (OverflowException)
        {
            return PropertyValueKind.Number;
        }
    }

    public override string ToString()
    {
        string descr = string.IsNullOrEmpty(Description) ? "" : string.Format(" ({0})", Description);

        string opset = OperationSet == null ? "" : string.Format(" (OperationSet={0})", OperationSet);

        string settable = CanSet ? " (SET)" : "";

        return $"{Name} = [{Value}]{settable}{descr}{opset}";
    }
}

public static class PropertyExtensions
{
    private static readonly StringComparer _ignoreCase = StringComparer.CurrentCultureIgnoreCase;

    public static Property FindByName(this IEnumerable<Property> list, string name)
    {
        if (list == null)
            throw new ArgumentNullException(nameof(list));

        return list.FirstOrDefault(x => _ignoreCase.Equals(x.Name, name));
    }
}
