using System;

namespace DiagnosticExplorer;

public static class ExtendedPropertyConfiguratorExtensions
{
    public static IPropertyConfigurator<T, TProperty> Expand<T, TProperty>(this IPropertyConfigurator<T, TProperty> property)
    {
        if (property == null)
            throw new ArgumentNullException(nameof(property));

        if (property is IExtendedStrategyConfigurator configurator)
            configurator.ConfigureExtended();

        return property;
    }
}
