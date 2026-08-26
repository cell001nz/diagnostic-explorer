using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using DiagnosticExplorer.Logging;

namespace DiagnosticExplorer;

public sealed class DiagnosticConfiguration : IDiagConfigurator
{
    private readonly Dictionary<Type, TypeConfiguration> _types = new();
    private readonly Dictionary<Type, TypeConfiguration> _drillDownTypes = new();
    private readonly Dictionary<Type, string> _defaultFormats = new();
    private readonly List<Func<IServiceProvider, IEnumerable<RegisteredObject>>> _registeredObjectProviders = new();
    private int _drillDownMaxItems = 100;

    public bool ApplyAttributes { get; set; } = true;
    public int DrillDownMaxItems
    {
        get => _drillDownMaxItems;
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Drilldown max items must be greater than zero.");
            _drillDownMaxItems = value;
        }
    }
    public DiagnosticRuntimeOptions RuntimeOptions { get; } = new();

    public void RegisterObjects(Func<IServiceProvider, IEnumerable<RegisteredObject>> findObjects)
    {
        if (findObjects == null)
            throw new ArgumentNullException(nameof(findObjects));

        _registeredObjectProviders.Add(findObjects);
    }

    public void ConfigureHosting(Action<IDiagnosticHostingConfigurator> configure)
    {
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        configure(RuntimeOptions);
    }

    public void ConfigureEventRouting(Action<EventSinkRouteOptions> configure)
    {
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        configure(RuntimeOptions.Routing);
    }

    public void DefaultFormat<T>(string formatString)
    {
        if (formatString == null)
            throw new ArgumentNullException(nameof(formatString));

        Type type = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        _defaultFormats[type] = formatString;
    }

    public void Configure<T>(Action<ITypeConfigurator<T>> configure)
    {
        ConfigureType(_types, configure);
    }

    public void ConfigureDrillDown<T>(Action<ITypeConfigurator<T>> configure)
    {
        ConfigureType(_drillDownTypes, configure);
    }

    private static void ConfigureType<T>(Dictionary<Type, TypeConfiguration> types, Action<ITypeConfigurator<T>> configure)
    {
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        if (!types.TryGetValue(typeof(T), out TypeConfiguration typeConfiguration))
        {
            typeConfiguration = new TypeConfiguration(typeof(T));
            types.Add(typeof(T), typeConfiguration);
        }

        configure(new TypeConfigurator<T>(typeConfiguration));
    }

    internal DiagnosticConfigurationSnapshot CreateSnapshot()
    {
        return new DiagnosticConfigurationSnapshot(
            ApplyAttributes,
            DrillDownMaxItems,
            _types.Values.Select(type => type.Clone()),
            _drillDownTypes.Values.Select(type => type.Clone()),
            _defaultFormats,
            _registeredObjectProviders
        );
    }
}

public sealed class DiagnosticRuntimeOptions : IDiagnosticHostingConfigurator
{
    public bool Enabled { get; private set; } = true;
    public List<DiagnosticHostOptions> Hosts { get; } = new();
    public EventRetentionOptions EventRetention { get; } = new();
    public EventSinkRouteOptions Routing { get; } = new();

    IDiagnosticHostingConfigurator IDiagnosticHostingConfigurator.Enabled(bool enabled)
    {
        Enabled = enabled;
        return this;
    }

    IDiagnosticHostingConfigurator IDiagnosticHostingConfigurator.AddHost(DiagnosticHostType type, string url)
    {
        if (!Enum.IsDefined(typeof(DiagnosticHostType), type))
            throw new ArgumentOutOfRangeException(nameof(type));
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("A diagnostics host URL is required.", nameof(url));

        Hosts.Add(new DiagnosticHostOptions { Type = type, Url = url });
        return this;
    }

    IDiagnosticHostingConfigurator IDiagnosticHostingConfigurator.EventRetention(Action<EventRetentionOptions> configure)
    {
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        configure(EventRetention);
        return this;
    }
}

internal enum PropertyStrategy
{
    Default,
    Collection,
    Rate,
    Date,
    Extended,
}

internal sealed class DiagnosticConfigurationSnapshot
{
    public static readonly DiagnosticConfigurationSnapshot Empty = new(
        true,
        100,
        Array.Empty<TypeConfiguration>(),
        Array.Empty<TypeConfiguration>(),
        new Dictionary<Type, string>(),
        Array.Empty<Func<IServiceProvider, IEnumerable<RegisteredObject>>>()
    );

    private readonly IReadOnlyDictionary<Type, TypeConfiguration> _types;
    private readonly IReadOnlyDictionary<Type, TypeConfiguration> _drillDownTypes;
    private readonly IReadOnlyDictionary<Type, string> _defaultFormats;
    private readonly IReadOnlyList<Func<IServiceProvider, IEnumerable<RegisteredObject>>> _registeredObjectProviders;

    public DiagnosticConfigurationSnapshot(
        bool applyAttributes,
        int drillDownMaxItems,
        IEnumerable<TypeConfiguration> types,
        IEnumerable<TypeConfiguration> drillDownTypes,
        IReadOnlyDictionary<Type, string> defaultFormats,
        IEnumerable<Func<IServiceProvider, IEnumerable<RegisteredObject>>> registeredObjectProviders
    )
    {
        ApplyAttributes = applyAttributes;
        DrillDownMaxItems = drillDownMaxItems;
        _types = types.ToDictionary(type => type.Type);
        _drillDownTypes = drillDownTypes.ToDictionary(type => type.Type);
        _defaultFormats = defaultFormats.ToDictionary(format => format.Key, format => format.Value);
        _registeredObjectProviders = registeredObjectProviders.ToArray();
    }

    public bool ApplyAttributes { get; }
    public int DrillDownMaxItems { get; }

    public TypeConfiguration GetEffectiveTypeConfiguration(Type runtimeType, bool drillDown = false)
    {
        IReadOnlyDictionary<Type, TypeConfiguration> source = drillDown && HasConfiguration(runtimeType, _drillDownTypes) ? _drillDownTypes : _types;
        return MergeTypeConfiguration(runtimeType, source);
    }

    private static TypeConfiguration MergeTypeConfiguration(Type runtimeType, IReadOnlyDictionary<Type, TypeConfiguration> configurations)
    {
        TypeConfiguration effective = null;
        foreach (Type type in GetTypeHierarchy(runtimeType))
        {
            if (!configurations.TryGetValue(type, out TypeConfiguration configured))
                continue;

            if (effective == null)
                effective = new TypeConfiguration(runtimeType);
            effective.Merge(configured);
        }
        return effective;
    }

    private static bool HasConfiguration(Type runtimeType, IReadOnlyDictionary<Type, TypeConfiguration> configurations)
    {
        return GetTypeHierarchy(runtimeType).Any(configurations.ContainsKey);
    }

    public string GetDefaultFormat(Type propertyType)
    {
        Type type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        return _defaultFormats.TryGetValue(type, out string formatString) ? formatString : null;
    }

    public IEnumerable<RegisteredObject> FindRegisteredObjects(IServiceProvider serviceProvider)
    {
        return _registeredObjectProviders.SelectMany(provider => provider(serviceProvider) ?? Array.Empty<RegisteredObject>());
    }

    private static IEnumerable<Type> GetTypeHierarchy(Type type)
    {
        Stack<Type> types = new();
        for (Type current = type; current != null; current = current.BaseType)
            types.Push(current);

        while (types.Count > 0)
            yield return types.Pop();
    }
}

internal sealed class TypeConfiguration
{
    private readonly Dictionary<string, PropertyConfiguration> _properties = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CustomPropertyConfiguration> _customProperties = new(StringComparer.Ordinal);

    public TypeConfiguration(Type type)
    {
        Type = type;
    }

    public Type Type { get; }
    public bool? OptIn { get; set; }
    public IEnumerable<PropertyConfiguration> Properties => _properties.Values;
    public IEnumerable<CustomPropertyConfiguration> CustomProperties => _customProperties.Values;

    public PropertyConfiguration GetOrAdd(PropertyInfo property)
    {
        string key = GetPropertyKey(property);
        if (!_properties.TryGetValue(key, out PropertyConfiguration configuration))
        {
            configuration = new PropertyConfiguration(property);
            _properties.Add(key, configuration);
        }
        return configuration;
    }

    public PropertyConfiguration Find(PropertyInfo property)
    {
        _properties.TryGetValue(GetPropertyKey(property), out PropertyConfiguration configuration);
        return configuration;
    }

    public CustomPropertyConfiguration AddCustomProperty(string name, Func<object, object> value)
    {
        CustomPropertyConfiguration configuration = new(name, value);
        _customProperties.Add(name, configuration);
        return configuration;
    }

    public TypeConfiguration Clone()
    {
        TypeConfiguration clone = new(Type) { OptIn = OptIn };
        foreach (PropertyConfiguration property in _properties.Values)
            clone._properties.Add(GetPropertyKey(property.Property), property.Clone());
        foreach (CustomPropertyConfiguration property in _customProperties.Values)
            clone._customProperties.Add(property.Name, property.Clone());
        return clone;
    }

    public void Merge(TypeConfiguration source)
    {
        if (source.OptIn.HasValue)
            OptIn = source.OptIn;

        foreach (PropertyConfiguration sourceProperty in source.Properties)
        {
            PropertyConfiguration target = GetOrAdd(sourceProperty.Property);
            target.Merge(sourceProperty);
        }

        foreach (CustomPropertyConfiguration sourceProperty in source.CustomProperties)
            _customProperties[sourceProperty.Name] = sourceProperty.Clone();
    }

    private static string GetPropertyKey(PropertyInfo property)
    {
        return property.Name;
    }
}

internal sealed class PropertyConfiguration
{
    public PropertyConfiguration(PropertyInfo property)
    {
        Property = property;
    }

    public PropertyInfo Property { get; }
    public bool? Included { get; set; }
    public bool UsesPropertyDefaults { get; set; }
    public PropertyStrategy? Strategy { get; set; }
    public ConfiguredValue<string> Name { get; set; }
    public Func<object, string> NameFormatter { get; set; }
    public ConfiguredValue<string> Category { get; set; }
    public Func<object, string> CategoryFormatter { get; set; }
    public ConfiguredValue<string> Description { get; set; }
    public Func<object, string> DescriptionFormatter { get; set; }
    public List<PropertyAlertConfiguration> Alerts { get; } = new();
    public ConfiguredValue<string> FormatString { get; set; }
    public Func<object, string> ValueFormatter { get; set; }
    public ConfiguredValue<bool> AllowSet { get; set; }
    public ConfiguredValue<bool> ExposeRate { get; set; }
    public ConfiguredValue<bool> ExposeTotal { get; set; }
    public ConfiguredValue<bool> ExposeDate { get; set; }
    public ConfiguredValue<bool> ExposeElapsed { get; set; }
    public ConfiguredValue<bool> ExposeTimeUntil { get; set; }
    public ConfiguredValue<int> MaxItems { get; set; }
    public ConfiguredValue<bool> DrillDown { get; set; }
    public ConfiguredValue<int> DrillDownMaxItems { get; set; }
    public List<CollectionOutputConfiguration> CollectionOutputs { get; } = new();

    public PropertyConfiguration Clone()
    {
        PropertyConfiguration clone = new(Property);
        clone.Merge(this);
        return clone;
    }

    public void Merge(PropertyConfiguration source)
    {
        Included = source.Included ?? Included;
        UsesPropertyDefaults |= source.UsesPropertyDefaults;
        Strategy = source.Strategy ?? Strategy;
        Name = source.Name.Or(Name);
        NameFormatter = source.NameFormatter ?? NameFormatter;
        Category = source.Category.Or(Category);
        CategoryFormatter = source.CategoryFormatter ?? CategoryFormatter;
        Description = source.Description.Or(Description);
        DescriptionFormatter = source.DescriptionFormatter ?? DescriptionFormatter;
        FormatString = source.FormatString.Or(FormatString);
        ValueFormatter = source.ValueFormatter ?? ValueFormatter;
        AllowSet = source.AllowSet.Or(AllowSet);
        ExposeRate = source.ExposeRate.Or(ExposeRate);
        ExposeTotal = source.ExposeTotal.Or(ExposeTotal);
        ExposeDate = source.ExposeDate.Or(ExposeDate);
        ExposeElapsed = source.ExposeElapsed.Or(ExposeElapsed);
        ExposeTimeUntil = source.ExposeTimeUntil.Or(ExposeTimeUntil);
        MaxItems = source.MaxItems.Or(MaxItems);
        DrillDown = source.DrillDown.Or(DrillDown);
        DrillDownMaxItems = source.DrillDownMaxItems.Or(DrillDownMaxItems);
        Alerts.AddRange(source.Alerts.Select(alert => alert.Clone()));
        if (source.CollectionOutputs.Count > 0)
        {
            CollectionOutputs.Clear();
            CollectionOutputs.AddRange(source.CollectionOutputs.Select(output => output.Clone()));
        }
    }
}

internal sealed class PropertyAlertConfiguration
{
    public PropertyAlertConfiguration(
        PropertyAlertSeverity severity,
        Func<object, bool> condition,
        Func<object, string> message,
        Func<object, string> category
    )
    {
        Severity = severity;
        Condition = condition;
        Message = message;
        Category = category;
    }

    public PropertyAlertSeverity Severity { get; }
    public Func<object, bool> Condition { get; }
    public Func<object, string> Message { get; }
    public Func<object, string> Category { get; }

    public PropertyAlertConfiguration Clone() => new(Severity, Condition, Message, Category);
}

internal sealed class CustomPropertyConfiguration
{
    public CustomPropertyConfiguration(string name, Func<object, object> value)
    {
        Name = name;
        Value = value;
    }

    public string Name { get; }
    public Func<object, object> Value { get; }
    public ConfiguredValue<string> Category { get; set; }
    public Func<object, string> CategoryFormatter { get; set; }
    public ConfiguredValue<string> Description { get; set; }
    public Func<object, string> DescriptionFormatter { get; set; }
    public List<PropertyAlertConfiguration> Alerts { get; } = new();
    public ConfiguredValue<bool> DrillDown { get; set; }
    public ConfiguredValue<int> DrillDownMaxItems { get; set; }

    public CustomPropertyConfiguration Clone()
    {
        CustomPropertyConfiguration clone = new(Name, Value)
        {
            Category = Category,
            CategoryFormatter = CategoryFormatter,
            Description = Description,
            DescriptionFormatter = DescriptionFormatter,
            DrillDown = DrillDown,
            DrillDownMaxItems = DrillDownMaxItems,
        };
        clone.Alerts.AddRange(Alerts.Select(alert => alert.Clone()));
        return clone;
    }
}

internal readonly struct ConfiguredValue<T>
{
    public ConfiguredValue(T value)
    {
        IsSet = true;
        Value = value;
    }

    public bool IsSet { get; }
    public T Value { get; }

    public ConfiguredValue<T> Or(ConfiguredValue<T> fallback) => IsSet ? this : fallback;
}

internal sealed class CollectionOutputConfiguration
{
    public CollectionMode Mode { get; set; }
    public string Name { get; set; }
    public string Separator { get; set; }
    public string NameProperty { get; set; }
    public Func<object, string> NameFormatter { get; set; }
    public string ValueProperty { get; set; }
    public Func<object, string> ValueFormatter { get; set; }
    public string DescriptionProperty { get; set; }
    public Func<object, string> DescriptionFormatter { get; set; }
    public string CategoryProperty { get; set; }
    public Func<object, string> CategoryFormatter { get; set; }

    public CollectionOutputConfiguration Clone() => (CollectionOutputConfiguration)MemberwiseClone();
}

internal sealed class TypeConfigurator<T> : ITypeConfigurator<T>
{
    private readonly TypeConfiguration _configuration;
    private readonly System.Threading.AsyncLocal<CategoryScope> _categoryScope = new();

    public TypeConfigurator(TypeConfiguration configuration)
    {
        _configuration = configuration;
    }

    public ITypeConfigurator<T> OptIn()
    {
        _configuration.OptIn = true;
        return this;
    }

    public ITypeConfigurator<T> OptOut()
    {
        _configuration.OptIn = false;
        return this;
    }

    public IDisposable CreateCategoryScope(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("A category is required.", nameof(category));

        CategoryScope scope = new(_categoryScope, category);
        _categoryScope.Value = scope;
        return scope;
    }

    public ITypeConfigurator<T> Include<TProperty>(Expression<Func<T, TProperty>> property)
    {
        GetProperty(property).Included = true;
        return this;
    }

    public ITypeConfigurator<T> Exclude<TProperty>(Expression<Func<T, TProperty>> property)
    {
        GetProperty(property).Included = false;
        return this;
    }

    public IPropertyConfigurator<T, TProperty> Property<TProperty>(Expression<Func<T, TProperty>> property)
    {
        PropertyConfiguration configuration = GetProperty(property);
        configuration.Included = true;
        configuration.UsesPropertyDefaults = true;
        ApplyCategoryScope(configuration);
        return new PropertyConfigurator<T, TProperty>(configuration);
    }

    public ICustomPropertyConfigurator<T> CustomProperty(string name, Func<T, object> value)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A custom property name is required.", nameof(name));
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        CustomPropertyConfiguration configuration = _configuration.AddCustomProperty(name, item => value((T)item));
        CategoryScope scope = _categoryScope.Value;
        if (scope != null)
            configuration.Category = new ConfiguredValue<string>(scope.Category);
        return new CustomPropertyConfigurator<T>(configuration);
    }

    public ICollectionConfigurator<T, TItem> Collection<TItem>(Expression<Func<T, IEnumerable<TItem>>> property)
    {
        PropertyConfiguration configuration = GetProperty(property);
        configuration.Included = true;
        configuration.UsesPropertyDefaults = true;
        configuration.Strategy = PropertyStrategy.Collection;
        ApplyCategoryScope(configuration);
        return new CollectionConfigurator<T, TItem>(configuration);
    }

    public IRateConfigurator<T> Rate(Expression<Func<T, RateCounter>> property)
    {
        PropertyConfiguration configuration = GetProperty(property);
        configuration.Included = true;
        configuration.UsesPropertyDefaults = true;
        configuration.Strategy = PropertyStrategy.Rate;
        ApplyCategoryScope(configuration);
        return new RateConfigurator<T>(configuration);
    }

    public IDateConfigurator<T> Date(Expression<Func<T, DateTime>> property) => ConfigureDate(property);

    public IDateConfigurator<T> Date(Expression<Func<T, DateTime?>> property) => ConfigureDate(property);

    public IDateConfigurator<T> Date(Expression<Func<T, DateTimeOffset>> property) => ConfigureDate(property);

    public IDateConfigurator<T> Date(Expression<Func<T, DateTimeOffset?>> property) => ConfigureDate(property);

    public IExtendedPropertyConfigurator<T, TProperty> Extended<TProperty>(Expression<Func<T, TProperty>> property)
    {
        PropertyConfiguration configuration = GetProperty(property);
        configuration.Included = true;
        configuration.UsesPropertyDefaults = true;
        configuration.Strategy = PropertyStrategy.Extended;
        ApplyCategoryScope(configuration);
        return new ExtendedPropertyConfigurator<T, TProperty>(configuration);
    }

    private IDateConfigurator<T> ConfigureDate(LambdaExpression property)
    {
        PropertyConfiguration configuration = GetProperty(property);
        configuration.Included = true;
        configuration.UsesPropertyDefaults = true;
        configuration.Strategy = PropertyStrategy.Date;
        ApplyCategoryScope(configuration);
        return new DateConfigurator<T>(configuration);
    }

    private PropertyConfiguration GetProperty(LambdaExpression expression)
    {
        return _configuration.GetOrAdd(ExpressionProperty.Get(expression, typeof(T)));
    }

    private void ApplyCategoryScope(PropertyConfiguration configuration)
    {
        CategoryScope scope = _categoryScope.Value;
        if (scope != null)
            configuration.Category = new ConfiguredValue<string>(scope.Category);
    }

    private sealed class CategoryScope : IDisposable
    {
        private readonly System.Threading.AsyncLocal<CategoryScope> _scope;
        private readonly CategoryScope _previous;
        private bool _disposed;

        public CategoryScope(System.Threading.AsyncLocal<CategoryScope> scope, string category)
        {
            _scope = scope;
            _previous = scope.Value;
            Category = category;
        }

        public string Category { get; }

        public void Dispose()
        {
            if (_disposed)
                return;
            if (_scope.Value != this)
                throw new InvalidOperationException("Category scopes must be disposed in reverse order.");

            _scope.Value = _previous;
            _disposed = true;
        }
    }
}

internal class PropertyConfigurator : IPropertyConfigurator
{
    protected readonly PropertyConfiguration Configuration;

    public PropertyConfigurator(PropertyConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IPropertyConfigurator Named(string name)
    {
        Configuration.Name = new ConfiguredValue<string>(name);
        return this;
    }

    public IPropertyConfigurator Category(string category)
    {
        Configuration.Category = new ConfiguredValue<string>(category);
        return this;
    }

    public IPropertyConfigurator Description(string description)
    {
        Configuration.Description = new ConfiguredValue<string>(description);
        return this;
    }

    public IPropertyConfigurator Format(string formatString)
    {
        Configuration.FormatString = new ConfiguredValue<string>(formatString);
        return this;
    }

    public IPropertyConfigurator AllowSet(bool allowSet = true)
    {
        Configuration.AllowSet = new ConfiguredValue<bool>(allowSet);
        return this;
    }
}

internal abstract class ObjectPropertyConfigurator<T, TSelf> : PropertyConfigurator, IObjectPropertyConfigurator<T, TSelf>
{
    protected ObjectPropertyConfigurator(PropertyConfiguration configuration)
        : base(configuration) { }

    private TSelf Self => (TSelf)(object)this;

    public new TSelf Named(string name)
    {
        base.Named(name);
        return Self;
    }

    public TSelf Named(Func<T, string> name)
    {
        if (name == null)
            throw new ArgumentNullException(nameof(name));

        Configuration.NameFormatter = item => name((T)item);
        return Self;
    }

    public new TSelf Category(string category)
    {
        base.Category(category);
        return Self;
    }

    public TSelf Category(Func<T, string> category)
    {
        if (category == null)
            throw new ArgumentNullException(nameof(category));

        Configuration.CategoryFormatter = item => category((T)item);
        return Self;
    }

    public new TSelf Description(string description)
    {
        base.Description(description);
        return Self;
    }

    public TSelf Description(Func<T, string> description)
    {
        if (description == null)
            throw new ArgumentNullException(nameof(description));

        Configuration.DescriptionFormatter = item => description((T)item);
        return Self;
    }

    public new TSelf Format(string formatString)
    {
        base.Format(formatString);
        return Self;
    }

    public new TSelf AllowSet(bool allowSet = true)
    {
        base.AllowSet(allowSet);
        return Self;
    }
}

internal sealed class PropertyConfigurator<T, TProperty>
    : ObjectPropertyConfigurator<T, IPropertyConfigurator<T, TProperty>>,
        IPropertyConfigurator<T, TProperty>
{
    public PropertyConfigurator(PropertyConfiguration configuration)
        : base(configuration) { }

    public IPropertyConfigurator<T, TProperty> Format(Func<TProperty, string> format)
    {
        if (format == null)
            throw new ArgumentNullException(nameof(format));

        Configuration.ValueFormatter = value => format((TProperty)value);
        return this;
    }

    public IPropertyConfigurator<T, TProperty> WithDrillDown(bool enabled = true, int? maxItems = null)
    {
        ConfigureDrillDown(Configuration, enabled, maxItems);
        return this;
    }

    private static void ConfigureDrillDown(PropertyConfiguration configuration, bool enabled, int? maxItems)
    {
        if (maxItems <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxItems), "Drilldown max items must be greater than zero.");

        configuration.DrillDown = new ConfiguredValue<bool>(enabled);
        if (maxItems.HasValue)
            configuration.DrillDownMaxItems = new ConfiguredValue<int>(maxItems.Value);
    }

    public IPropertyConfigurator<T, TProperty> Warn(Func<T, bool> condition, Func<T, string> message)
    {
        return Warn(condition, message, null);
    }

    public IPropertyConfigurator<T, TProperty> Warn(Func<T, bool> condition, Func<T, string> message, string category)
    {
        return AddAlert(PropertyAlertSeverity.Warning, condition, message, category);
    }

    public IPropertyConfigurator<T, TProperty> Warn(Func<T, bool> condition, string message)
    {
        return Warn(condition, message, null);
    }

    public IPropertyConfigurator<T, TProperty> Warn(Func<T, bool> condition, string message, string category)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        return Warn(condition, _ => message, category);
    }

    public IPropertyConfigurator<T, TProperty> Error(Func<T, bool> condition, Func<T, string> message)
    {
        return Error(condition, message, null);
    }

    public IPropertyConfigurator<T, TProperty> Error(Func<T, bool> condition, Func<T, string> message, string category)
    {
        return AddAlert(PropertyAlertSeverity.Error, condition, message, category);
    }

    public IPropertyConfigurator<T, TProperty> Error(Func<T, bool> condition, string message)
    {
        return Error(condition, message, null);
    }

    public IPropertyConfigurator<T, TProperty> Error(Func<T, bool> condition, string message, string category)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        return Error(condition, _ => message, category);
    }

    private IPropertyConfigurator<T, TProperty> AddAlert(
        PropertyAlertSeverity severity,
        Func<T, bool> condition,
        Func<T, string> message,
        string category
    )
    {
        if (condition == null)
            throw new ArgumentNullException(nameof(condition));
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        Configuration.Alerts.Add(
            new PropertyAlertConfiguration(severity, item => condition((T)item), item => message((T)item), category == null ? null : _ => category)
        );
        return this;
    }
}

internal sealed class CustomPropertyConfigurator<T> : ICustomPropertyConfigurator<T>
{
    private readonly CustomPropertyConfiguration _configuration;

    public CustomPropertyConfigurator(CustomPropertyConfiguration configuration)
    {
        _configuration = configuration;
    }

    public ICustomPropertyConfigurator<T> WithDrillDown(bool enabled = true, int? maxItems = null)
    {
        if (maxItems <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxItems), "Drilldown max items must be greater than zero.");

        _configuration.DrillDown = new ConfiguredValue<bool>(enabled);
        if (maxItems.HasValue)
            _configuration.DrillDownMaxItems = new ConfiguredValue<int>(maxItems.Value);
        return this;
    }

    public ICustomPropertyConfigurator<T> Category(string category)
    {
        _configuration.Category = new ConfiguredValue<string>(category);
        return this;
    }

    public ICustomPropertyConfigurator<T> Category(Func<T, string> category)
    {
        if (category == null)
            throw new ArgumentNullException(nameof(category));

        _configuration.CategoryFormatter = item => category((T)item);
        return this;
    }

    public ICustomPropertyConfigurator<T> Description(string description)
    {
        _configuration.Description = new ConfiguredValue<string>(description);
        return this;
    }

    public ICustomPropertyConfigurator<T> Description(Func<T, string> description)
    {
        if (description == null)
            throw new ArgumentNullException(nameof(description));

        _configuration.DescriptionFormatter = item => description((T)item);
        return this;
    }

    public ICustomPropertyConfigurator<T> Warn(Func<T, bool> condition, Func<T, string> message)
    {
        return Warn(condition, message, null);
    }

    public ICustomPropertyConfigurator<T> Warn(Func<T, bool> condition, Func<T, string> message, string category)
    {
        return AddAlert(PropertyAlertSeverity.Warning, condition, message, category);
    }

    public ICustomPropertyConfigurator<T> Warn(Func<T, bool> condition, string message)
    {
        return Warn(condition, message, null);
    }

    public ICustomPropertyConfigurator<T> Warn(Func<T, bool> condition, string message, string category)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        return Warn(condition, _ => message, category);
    }

    public ICustomPropertyConfigurator<T> Error(Func<T, bool> condition, Func<T, string> message)
    {
        return Error(condition, message, null);
    }

    public ICustomPropertyConfigurator<T> Error(Func<T, bool> condition, Func<T, string> message, string category)
    {
        return AddAlert(PropertyAlertSeverity.Error, condition, message, category);
    }

    public ICustomPropertyConfigurator<T> Error(Func<T, bool> condition, string message)
    {
        return Error(condition, message, null);
    }

    public ICustomPropertyConfigurator<T> Error(Func<T, bool> condition, string message, string category)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        return Error(condition, _ => message, category);
    }

    private ICustomPropertyConfigurator<T> AddAlert(PropertyAlertSeverity severity, Func<T, bool> condition, Func<T, string> message, string category)
    {
        if (condition == null)
            throw new ArgumentNullException(nameof(condition));
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        _configuration.Alerts.Add(
            new PropertyAlertConfiguration(severity, item => condition((T)item), item => message((T)item), category == null ? null : _ => category)
        );
        return this;
    }
}

internal sealed class CollectionConfigurator<T, TItem>
    : ObjectPropertyConfigurator<T, ICollectionConfigurator<T, TItem>>,
        ICollectionConfigurator<T, TItem>
{
    public CollectionConfigurator(PropertyConfiguration configuration)
        : base(configuration) { }

    public ICollectionConfigurator<T, TItem> ShowCount(string name = null)
    {
        AddOutput(CollectionMode.Count, name);
        return this;
    }

    public ICollectionConfigurator<T, TItem> Concatenate(string separator = null, string name = null)
    {
        AddOutput(CollectionMode.Concatenate, name).Separator = separator;
        return this;
    }

    public ICollectionConfigurator<T, TItem> List(Action<ICollectionListConfigurator<TItem>> configure = null)
    {
        CollectionOutputConfiguration output = AddOutput(CollectionMode.List, null);
        configure?.Invoke(new CollectionListConfigurator<TItem>(output));
        return this;
    }

    public ICollectionConfigurator<T, TItem> Categories(Expression<Func<TItem, object>> category, string name = null)
    {
        if (category == null)
            throw new ArgumentNullException(nameof(category));

        CollectionOutputConfiguration output = AddOutput(CollectionMode.Categories, name);
        output.CategoryProperty = ExpressionProperty.Get(category, typeof(TItem)).Name;
        return this;
    }

    public ICollectionConfigurator<T, TItem> WithMaxItems(int maxItems)
    {
        if (maxItems <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxItems), "Max items must be greater than zero.");

        Configuration.MaxItems = new ConfiguredValue<int>(maxItems);
        return this;
    }

    public ICollectionConfigurator<T, TItem> WithDrillDown(bool enabled = true, int? maxItems = null)
    {
        if (maxItems <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxItems), "Drilldown max items must be greater than zero.");

        Configuration.DrillDown = new ConfiguredValue<bool>(enabled);
        if (maxItems.HasValue)
            Configuration.DrillDownMaxItems = new ConfiguredValue<int>(maxItems.Value);
        return this;
    }

    private CollectionOutputConfiguration AddOutput(CollectionMode mode, string name)
    {
        CollectionOutputConfiguration output = new() { Mode = mode, Name = name };
        Configuration.CollectionOutputs.Add(output);
        return output;
    }
}

internal sealed class CollectionListConfigurator<TItem> : ICollectionListConfigurator<TItem>
{
    private readonly CollectionOutputConfiguration _output;

    public CollectionListConfigurator(CollectionOutputConfiguration output)
    {
        _output = output;
    }

    public ICollectionListConfigurator<TItem> Name(Func<TItem, string> format)
    {
        if (format == null)
            throw new ArgumentNullException(nameof(format));

        _output.NameFormatter = item => format((TItem)item);
        return this;
    }

    public ICollectionListConfigurator<TItem> Value(Func<TItem, string> format)
    {
        if (format == null)
            throw new ArgumentNullException(nameof(format));

        _output.ValueFormatter = item => format((TItem)item);
        return this;
    }

    public ICollectionListConfigurator<TItem> Description(Func<TItem, string> format)
    {
        if (format == null)
            throw new ArgumentNullException(nameof(format));

        _output.DescriptionFormatter = item => format((TItem)item);
        return this;
    }

    public ICollectionListConfigurator<TItem> Category(Func<TItem, string> format)
    {
        if (format == null)
            throw new ArgumentNullException(nameof(format));

        _output.CategoryFormatter = item => format((TItem)item);
        return this;
    }
}

internal sealed class RateConfigurator<T> : ObjectPropertyConfigurator<T, IRateConfigurator<T>>, IRateConfigurator<T>
{
    public RateConfigurator(PropertyConfiguration configuration)
        : base(configuration) { }

    public IRateConfigurator<T> ShowRate(bool expose = true)
    {
        Configuration.ExposeRate = new ConfiguredValue<bool>(expose);
        return this;
    }

    public IRateConfigurator<T> ShowTotal(bool expose = true)
    {
        Configuration.ExposeTotal = new ConfiguredValue<bool>(expose);
        return this;
    }
}

internal sealed class DateConfigurator<T> : ObjectPropertyConfigurator<T, IDateConfigurator<T>>, IDateConfigurator<T>
{
    public DateConfigurator(PropertyConfiguration configuration)
        : base(configuration) { }

    public IDateConfigurator<T> ShowDate(bool expose = true)
    {
        Configuration.ExposeDate = new ConfiguredValue<bool>(expose);
        return this;
    }

    public IDateConfigurator<T> ShowElapsed(bool expose = true)
    {
        Configuration.ExposeElapsed = new ConfiguredValue<bool>(expose);
        return this;
    }

    public IDateConfigurator<T> ShowTimeUntil(bool expose = true)
    {
        Configuration.ExposeTimeUntil = new ConfiguredValue<bool>(expose);
        return this;
    }
}

internal sealed class ExtendedPropertyConfigurator<T, TProperty>
    : ObjectPropertyConfigurator<T, IExtendedPropertyConfigurator<T, TProperty>>,
        IExtendedPropertyConfigurator<T, TProperty>
{
    public ExtendedPropertyConfigurator(PropertyConfiguration configuration)
        : base(configuration) { }
}

internal static class ExpressionProperty
{
    public static PropertyInfo Get(LambdaExpression expression, Type expectedDeclaringType)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));

        Expression body = expression.Body;
        while (body is UnaryExpression unary && (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
            body = unary.Operand;

        if (body is not MemberExpression { Member: PropertyInfo property } member || member.Expression != expression.Parameters[0])
            throw new ArgumentException("The expression must select a direct property.", nameof(expression));

        if (!property.DeclaringType.IsAssignableFrom(expectedDeclaringType))
            throw new ArgumentException($"Property '{property.Name}' is not declared on '{expectedDeclaringType.Name}'.", nameof(expression));

        return property;
    }

    public static string GetOptionalName(LambdaExpression expression, Type expectedDeclaringType)
    {
        return expression == null ? null : Get(expression, expectedDeclaringType).Name;
    }
}
