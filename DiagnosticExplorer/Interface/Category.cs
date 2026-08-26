using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using ProtoBuf;

namespace DiagnosticExplorer;

[ProtoContract(UseProtoMembersOnly = true)]
public class Category
{
    public Category()
    {
        Properties = new List<Property>();
    }

    public Category(string name)
        : this()
    {
        Name = name;
    }

    [ProtoMember(1)]
    public string Name { get; set; }

    [ProtoMember(2)]
    public string OperationSet { get; set; }

    [ProtoMember(3)]
    public List<Property> Properties { get; set; }

    [ProtoMember(4)]
    public bool CanDrillDown { get; set; }

    internal object ValueObject { get; set; }

    internal object DrillDownObject { get; set; }

    internal int DrillDownMaxItems { get; set; }
}

public static class CategoryExtensions
{
    private static readonly StringComparer _ignoreCase = StringComparer.CurrentCultureIgnoreCase;

    public static Category FindByName(this IEnumerable<Category> list, string name)
    {
        if (list == null)
            throw new ArgumentNullException(nameof(list));

        return list.FirstOrDefault(x => _ignoreCase.Equals(x.Name, name));
    }
}
