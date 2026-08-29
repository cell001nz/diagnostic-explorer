using System.Collections.Generic;

namespace DiagnosticExplorer;

internal enum NestedPropertyRenderMode
{
    All,
    PrimaryOnly,
}

internal static class NestedPropertyRenderer
{
    public static void Render(object value, PropertyBag bag, string category, NestedPropertyRenderMode mode)
    {
        foreach (PropertyGetter getter in DiagnosticManager.GetPropertyGetters(value))
        {
            if (mode == NestedPropertyRenderMode.All || (getter.IsDirectProperty && getter.IsInGeneralCategory(value)))
                getter.GetProperties(value, bag, category);
        }
    }
}
