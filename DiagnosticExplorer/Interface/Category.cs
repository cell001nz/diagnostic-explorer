using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json.Serialization;

namespace DiagnosticExplorer;

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

    public string Name { get; set; }
    public string OperationSet { get; set; }
    public List<Property> Properties { get; set; }
    public bool CanDrillDown { get; set; }
    public bool IsExpanded { get; set; }
    public bool IsExpandedProperty { get; set; }
    public List<PropertyStatus> Statuses { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public StatusIconSize StatusIconSize { get; set; }

    internal object ValueObject { get; set; }

    internal object DrillDownObject { get; set; }

    internal int DrillDownMaxItems { get; set; }
}

public static class CategoryExtensions
{
    private static readonly StringComparer _ignoreCase = StringComparer.CurrentCultureIgnoreCase;

    public static string NormalizeName(string name)
    {
        return string.IsNullOrWhiteSpace(name) || string.Equals(name, "General", StringComparison.OrdinalIgnoreCase) ? null : name;
    }

    public static Category FindByName(this IEnumerable<Category> list, string name)
    {
        if (list == null)
            throw new ArgumentNullException(nameof(list));

        name = NormalizeName(name);
        return list.FirstOrDefault(x => _ignoreCase.Equals(x.Name, name));
    }
}
