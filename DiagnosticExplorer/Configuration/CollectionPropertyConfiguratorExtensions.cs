using System;
using System.Collections.Generic;

namespace DiagnosticExplorer;

public static class CollectionPropertyConfiguratorExtensions
{
    public static ICollectionConfigurator<T, TItem> ListItems<T, TItem>(
        this IPropertyConfigurator<T, TItem[]> property,
        Action<ICollectionListConfigurator<TItem>> configure = null
    ) => property.CollectionItems().ListItems(configure);

    public static ICollectionConfigurator<T, TItem> CollectionItems<T, TItem>(this IPropertyConfigurator<T, TItem[]> property) =>
        ConfigureCollection<T, TItem>(property);

    public static ICollectionConfigurator<T, TItem> ShowCount<T, TItem>(
        this IPropertyConfigurator<T, TItem[]> property,
        string name = null
    ) => ConfigureCollection<T, TItem>(property).ShowCount(name);

    public static ICollectionConfigurator<T, TItem> ConcatItems<T, TItem>(
        this IPropertyConfigurator<T, TItem[]> property,
        string separator = null,
        string name = null
    ) => ConfigureCollection<T, TItem>(property).ConcatItems(separator, name);

    public static ICollectionConfigurator<T, TItem> SectionByItem<T, TItem>(
        this IPropertyConfigurator<T, TItem[]> property,
        Func<TItem, object> category,
        string name = null
    ) => ConfigureCollection<T, TItem>(property).SectionByItem(category, name);

    public static ICollectionConfigurator<T, TItem> WithMaxItems<T, TItem>(
        this IPropertyConfigurator<T, TItem[]> property,
        int maxItems
    ) => ConfigureCollection<T, TItem>(property).WithMaxItems(maxItems);

    public static ICollectionConfigurator<T, TItem> ListItems<T, TItem>(
        this IPropertyConfigurator<T, IList<TItem>> property,
        Action<ICollectionListConfigurator<TItem>> configure = null
    ) => property.CollectionItems().ListItems(configure);

    public static ICollectionConfigurator<T, TItem> CollectionItems<T, TItem>(this IPropertyConfigurator<T, IList<TItem>> property) =>
        ConfigureCollection<T, TItem>(property);

    public static ICollectionConfigurator<T, TItem> ShowCount<T, TItem>(
        this IPropertyConfigurator<T, IList<TItem>> property,
        string name = null
    ) => ConfigureCollection<T, TItem>(property).ShowCount(name);

    public static ICollectionConfigurator<T, TItem> ConcatItems<T, TItem>(
        this IPropertyConfigurator<T, IList<TItem>> property,
        string separator = null,
        string name = null
    ) => ConfigureCollection<T, TItem>(property).ConcatItems(separator, name);

    public static ICollectionConfigurator<T, TItem> SectionByItem<T, TItem>(
        this IPropertyConfigurator<T, IList<TItem>> property,
        Func<TItem, object> category,
        string name = null
    ) => ConfigureCollection<T, TItem>(property).SectionByItem(category, name);

    public static ICollectionConfigurator<T, TItem> WithMaxItems<T, TItem>(
        this IPropertyConfigurator<T, IList<TItem>> property,
        int maxItems
    ) => ConfigureCollection<T, TItem>(property).WithMaxItems(maxItems);

    public static ICollectionConfigurator<T, TItem> ListItems<T, TItem>(
        this IPropertyConfigurator<T, IReadOnlyList<TItem>> property,
        Action<ICollectionListConfigurator<TItem>> configure = null
    ) => property.CollectionItems().ListItems(configure);

    public static ICollectionConfigurator<T, TItem> CollectionItems<T, TItem>(this IPropertyConfigurator<T, IReadOnlyList<TItem>> property) =>
        ConfigureCollection<T, TItem>(property);

    public static ICollectionConfigurator<T, TItem> ShowCount<T, TItem>(
        this IPropertyConfigurator<T, IReadOnlyList<TItem>> property,
        string name = null
    ) => ConfigureCollection<T, TItem>(property).ShowCount(name);

    public static ICollectionConfigurator<T, TItem> ConcatItems<T, TItem>(
        this IPropertyConfigurator<T, IReadOnlyList<TItem>> property,
        string separator = null,
        string name = null
    ) => ConfigureCollection<T, TItem>(property).ConcatItems(separator, name);

    public static ICollectionConfigurator<T, TItem> SectionByItem<T, TItem>(
        this IPropertyConfigurator<T, IReadOnlyList<TItem>> property,
        Func<TItem, object> category,
        string name = null
    ) => ConfigureCollection<T, TItem>(property).SectionByItem(category, name);

    public static ICollectionConfigurator<T, TItem> WithMaxItems<T, TItem>(
        this IPropertyConfigurator<T, IReadOnlyList<TItem>> property,
        int maxItems
    ) => ConfigureCollection<T, TItem>(property).WithMaxItems(maxItems);

    public static ICollectionConfigurator<T, TItem> ListItems<T, TItem>(
        this IPropertyConfigurator<T, IReadOnlyCollection<TItem>> property,
        Action<ICollectionListConfigurator<TItem>> configure = null
    ) => property.CollectionItems().ListItems(configure);

    public static ICollectionConfigurator<T, TItem> CollectionItems<T, TItem>(this IPropertyConfigurator<T, IReadOnlyCollection<TItem>> property) =>
        ConfigureCollection<T, TItem>(property);

    public static ICollectionConfigurator<T, TItem> ShowCount<T, TItem>(
        this IPropertyConfigurator<T, IReadOnlyCollection<TItem>> property,
        string name = null
    ) => ConfigureCollection<T, TItem>(property).ShowCount(name);

    public static ICollectionConfigurator<T, TItem> ConcatItems<T, TItem>(
        this IPropertyConfigurator<T, IReadOnlyCollection<TItem>> property,
        string separator = null,
        string name = null
    ) => ConfigureCollection<T, TItem>(property).ConcatItems(separator, name);

    public static ICollectionConfigurator<T, TItem> SectionByItem<T, TItem>(
        this IPropertyConfigurator<T, IReadOnlyCollection<TItem>> property,
        Func<TItem, object> category,
        string name = null
    ) => ConfigureCollection<T, TItem>(property).SectionByItem(category, name);

    public static ICollectionConfigurator<T, TItem> WithMaxItems<T, TItem>(
        this IPropertyConfigurator<T, IReadOnlyCollection<TItem>> property,
        int maxItems
    ) => ConfigureCollection<T, TItem>(property).WithMaxItems(maxItems);

    public static ICollectionConfigurator<T, TItem> ListItems<T, TItem>(
        this IPropertyConfigurator<T, ISet<TItem>> property,
        Action<ICollectionListConfigurator<TItem>> configure = null
    ) => property.CollectionItems().ListItems(configure);

    public static ICollectionConfigurator<T, TItem> CollectionItems<T, TItem>(this IPropertyConfigurator<T, ISet<TItem>> property) =>
        ConfigureCollection<T, TItem>(property);

    public static ICollectionConfigurator<T, TItem> ShowCount<T, TItem>(
        this IPropertyConfigurator<T, ISet<TItem>> property,
        string name = null
    ) => ConfigureCollection<T, TItem>(property).ShowCount(name);

    public static ICollectionConfigurator<T, TItem> ConcatItems<T, TItem>(
        this IPropertyConfigurator<T, ISet<TItem>> property,
        string separator = null,
        string name = null
    ) => ConfigureCollection<T, TItem>(property).ConcatItems(separator, name);

    public static ICollectionConfigurator<T, TItem> SectionByItem<T, TItem>(
        this IPropertyConfigurator<T, ISet<TItem>> property,
        Func<TItem, object> category,
        string name = null
    ) => ConfigureCollection<T, TItem>(property).SectionByItem(category, name);

    public static ICollectionConfigurator<T, TItem> WithMaxItems<T, TItem>(
        this IPropertyConfigurator<T, ISet<TItem>> property,
        int maxItems
    ) => ConfigureCollection<T, TItem>(property).WithMaxItems(maxItems);

    public static ICollectionConfigurator<T, TItem> CollectionItems<T, TItem>(this IPropertyConfigurator<T, ICollection<TItem>> property) =>
        ConfigureCollection<T, TItem>(property);

    public static ICollectionConfigurator<T, TItem> ShowCount<T, TItem>(
        this IPropertyConfigurator<T, ICollection<TItem>> property,
        string name = null
    ) => ConfigureCollection<T, TItem>(property).ShowCount(name);

    public static ICollectionConfigurator<T, TItem> ConcatItems<T, TItem>(
        this IPropertyConfigurator<T, ICollection<TItem>> property,
        string separator = null,
        string name = null
    ) => ConfigureCollection<T, TItem>(property).ConcatItems(separator, name);

    public static ICollectionConfigurator<T, TItem> ListItems<T, TItem>(
        this IPropertyConfigurator<T, ICollection<TItem>> property,
        Action<ICollectionListConfigurator<TItem>> configure = null
    ) => ConfigureCollection<T, TItem>(property).ListItems(configure);

    public static ICollectionConfigurator<T, TItem> SectionByItem<T, TItem>(
        this IPropertyConfigurator<T, ICollection<TItem>> property,
        Func<TItem, object> category,
        string name = null
    ) => ConfigureCollection<T, TItem>(property).SectionByItem(category, name);

    public static ICollectionConfigurator<T, TItem> WithMaxItems<T, TItem>(
        this IPropertyConfigurator<T, ICollection<TItem>> property,
        int maxItems
    ) => ConfigureCollection<T, TItem>(property).WithMaxItems(maxItems);

    private static ICollectionConfigurator<T, TItem> ConfigureCollection<T, TItem>(IPropertyConfigurator property)
    {
        if (property == null)
            throw new ArgumentNullException(nameof(property));

        if (property is ICollectionStrategyConfigurator<T> configurator)
            return configurator.ConfigureCollection<TItem>();

        return new CollectionConfigurator<T, TItem>(new PropertyConfiguration(string.Empty, typeof(ICollection<TItem>), _ => null));
    }
}
