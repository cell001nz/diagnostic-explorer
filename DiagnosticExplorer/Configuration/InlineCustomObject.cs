using System;
using System.Collections.Generic;

namespace DiagnosticExplorer;

internal interface IInlineCustomObject
{
    void AddProperties(PropertyBag bag);
}

internal sealed class InlineCustomObjectConfigurator<T> : ICustomObjectConfigurator<T>
{
    private readonly List<InlineCustomObjectMember> _members = new();

    public IReadOnlyList<InlineCustomObjectMember> Members => _members;

    public ICustomPropertyConfigurator<T> Property(string name, Func<T, object> value)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A property name is required.", nameof(name));
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        CustomPropertyConfiguration configuration = new(name, item => value((T)item));
        _members.Add(new InlineCustomPropertyMember(configuration));
        return new CustomPropertyConfigurator<T>(configuration);
    }

    public IExtendedPropertyConfigurator<T, TProperty> Expanded<TProperty>(string name, Func<T, TProperty> value)
    {
        PropertyConfiguration configuration = AddConfiguredProperty(name, typeof(TProperty), item => value((T)item), PropertyStrategy.Extended);
        return new ExtendedPropertyConfigurator<T, TProperty>(configuration);
    }

    public ICollectionConfigurator<T, TItem> Collection<TItem>(string name, Func<T, IEnumerable<TItem>> value)
    {
        PropertyConfiguration configuration = AddConfiguredProperty(
            name,
            typeof(IEnumerable<TItem>),
            item => value((T)item),
            PropertyStrategy.Collection
        );
        return new CollectionConfigurator<T, TItem>(configuration);
    }

    public IRateConfigurator<T> Rate(string name, Func<T, RateCounter> value)
    {
        PropertyConfiguration configuration = AddConfiguredProperty(name, typeof(RateCounter), item => value((T)item), PropertyStrategy.Rate);
        return new RateConfigurator<T>(configuration);
    }

    private PropertyConfiguration AddConfiguredProperty(string name, Type valueType, Func<object, object> value, PropertyStrategy strategy)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A property name is required.", nameof(name));
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        PropertyConfiguration configuration = new(name, valueType, value) { Strategy = strategy };
        _members.Add(new InlineConfiguredPropertyMember(configuration));
        return configuration;
    }
}

internal sealed class InlineCustomObject<T> : IInlineCustomObject
{
    private readonly T _source;
    private readonly IReadOnlyList<InlineCustomObjectMember> _members;

    public InlineCustomObject(T source, IReadOnlyList<InlineCustomObjectMember> members)
    {
        _source = source;
        _members = members;
    }

    public void AddProperties(PropertyBag bag)
    {
        foreach (InlineCustomObjectMember member in _members)
            member.AddProperties(_source, this, bag);
    }
}

internal abstract class InlineCustomObjectMember
{
    public abstract void AddProperties(object source, object projection, PropertyBag bag);
}

internal sealed class InlineCustomPropertyMember : InlineCustomObjectMember
{
    private readonly CustomPropertyConfiguration _configuration;

    public InlineCustomPropertyMember(CustomPropertyConfiguration configuration) => _configuration = configuration;

    public override void AddProperties(object source, object projection, PropertyBag bag) =>
        new CustomPropertyGetter(_configuration.Bind(source)).GetProperties(projection, bag, null);
}

internal sealed class InlineConfiguredPropertyMember : InlineCustomObjectMember
{
    private readonly PropertyConfiguration _configuration;

    public InlineConfiguredPropertyMember(PropertyConfiguration configuration) => _configuration = configuration;

    public override void AddProperties(object source, object projection, PropertyBag bag)
    {
        List<PropertyGetter> getters = new();
        DiagnosticManager.AddPropertyGetters(getters, null, null, _configuration.Bind(source), false, false, null);
        foreach (PropertyGetter getter in getters)
            getter.GetProperties(projection, bag, null);
    }
}
