using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using DiagnosticExplorer.Logging;
using Microsoft.Extensions.Logging;

namespace DiagnosticExplorer;

public sealed class DiagnosticConfiguration : IDiagConfigurator
{
    private readonly Dictionary<Type, TypeConfiguration> _types = new();
    private readonly Dictionary<Type, TypeConfiguration> _drillDownTypes = new();
    private readonly Dictionary<Type, string> _defaultFormats = new();
    private readonly List<Action<IDiagRegistrar>> _registeredObjectProviders = new();
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

    public void RegisterObjects(Action<IDiagRegistrar> configure)
    {
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        _registeredObjectProviders.Add(configure);
    }

    public void ConfigureHosting(Action<IDiagnosticHostingConfigurator> configure)
    {
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        configure(RuntimeOptions);
    }

    public ISystemEnvironmentConfigurator ConfigureSystemEnvironment()
    {
        return RuntimeOptions.SystemEnvironment;
    }

    public void ConfigureEventRouting(Action<EventSinkRouteOptions> configure)
    {
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        configure(RuntimeOptions.Routing);
    }

    public void ConfigureLogEventRetention(Action<LogEventRetentionOptions> configure)
    {
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        configure(RuntimeOptions.LogEventRetention);
    }

    public void DefaultFormat<T>(string formatString)
    {
        if (formatString == null)
            throw new ArgumentNullException(nameof(formatString));

        Type type = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        _defaultFormats[type] = formatString;
    }

    public void ConfigureAssemblies(params Assembly[] assemblies)
    {
        if (assemblies == null)
            throw new ArgumentNullException(nameof(assemblies));

        foreach (Assembly assembly in assemblies.Where(assembly => assembly != null).Distinct())
        {
            foreach (Type type in GetLoadableTypes(assembly).OrderBy(type => type.FullName, StringComparer.Ordinal))
            {
                if (type.ContainsGenericParameters)
                    continue;

                MethodInfo method = type.GetMethod(
                    "ConfigureDiagnostics",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                    null,
                    new[] { typeof(IDiagConfigurator) },
                    null
                );
                if (method?.ReturnType != typeof(void))
                    continue;

                try
                {
                    method.Invoke(null, new object[] { this });
                }
                catch (Exception exception)
                {
                    Trace.TraceError($"Diagnostic Explorer ignored ConfigureDiagnostics on '{type.FullName}': {exception}");
                }
            }
        }
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

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            Trace.TraceError($"Diagnostic Explorer could not load all diagnostic configuration types from '{assembly.FullName}': {exception}");
            return exception.Types.Where(type => type != null);
        }
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
    public SystemEnvironmentOptions SystemEnvironment { get; } = new();
    public LogEventRetentionOptions LogEventRetention { get; } = new();
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

public sealed class SystemEnvironmentOptions : ISystemEnvironmentConfigurator
{
    public bool IsEnabled { get; private set; } = true;
    public string Category { get; private set; } = "System";
    public string Name { get; private set; } = "Environment";

    ISystemEnvironmentConfigurator ISystemEnvironmentConfigurator.Enabled(bool enabled)
    {
        IsEnabled = enabled;
        return this;
    }

    ISystemEnvironmentConfigurator ISystemEnvironmentConfigurator.WithCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("A category is required.", nameof(category));

        Category = category;
        return this;
    }

    ISystemEnvironmentConfigurator ISystemEnvironmentConfigurator.WithName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A name is required.", nameof(name));

        Name = name;
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
        Array.Empty<Action<IDiagRegistrar>>()
    );

    private readonly IReadOnlyDictionary<Type, TypeConfiguration> _types;
    private readonly IReadOnlyDictionary<Type, TypeConfiguration> _drillDownTypes;
    private readonly IReadOnlyDictionary<Type, string> _defaultFormats;
    private readonly IReadOnlyList<Action<IDiagRegistrar>> _registeredObjectProviders;

    public DiagnosticConfigurationSnapshot(
        bool applyAttributes,
        int drillDownMaxItems,
        IEnumerable<TypeConfiguration> types,
        IEnumerable<TypeConfiguration> drillDownTypes,
        IReadOnlyDictionary<Type, string> defaultFormats,
        IEnumerable<Action<IDiagRegistrar>> registeredObjectProviders
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
        TypeConfiguration effective = MergeTypeConfiguration(runtimeType, _types);
        if (!drillDown || !HasConfiguration(runtimeType, _drillDownTypes))
            return effective;

        effective.Merge(MergeTypeConfiguration(runtimeType, _drillDownTypes));
        return effective;
    }

    public bool HasDrillDownConfiguration(Type runtimeType)
    {
        return HasConfiguration(runtimeType, _drillDownTypes);
    }

    public bool HasTypeConfiguration(Type runtimeType)
    {
        return HasConfiguration(runtimeType, _types);
    }

    public bool? GetDeclaredTypeIncludeAll(Type type, bool drillDown = false)
    {
        IReadOnlyDictionary<Type, TypeConfiguration> configurations = drillDown ? _drillDownTypes : _types;
        return configurations.TryGetValue(type, out TypeConfiguration configuration) ? configuration.IncludeAll : null;
    }

    public bool? GetNearestTypeIncludeAll(Type runtimeType, bool drillDown = false)
    {
        for (Type type = runtimeType; type != null; type = type.BaseType)
        {
            bool? includeAll = GetDeclaredTypeIncludeAll(type, drillDown);
            if (includeAll.HasValue)
                return includeAll;
        }

        return null;
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
        return effective ?? new TypeConfiguration(runtimeType);
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
        List<RegisteredObject> registeredObjects = new();
        IDiagRegistrar registrations = new RegisteredObjectProviderConfigurator(serviceProvider, registeredObjects);
        foreach (Action<IDiagRegistrar> provider in _registeredObjectProviders)
            provider(registrations);

        return registeredObjects;
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
    private readonly Dictionary<string, PropertyConfiguration> _delegateProperties = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CustomPropertyConfiguration> _customProperties = new(StringComparer.Ordinal);
    private readonly List<DrillDownEventRouteTemplate> _eventRoutes = new();

    public TypeConfiguration(Type type)
    {
        Type = type;
    }

    public Type Type { get; }
    public bool? IncludeAll { get; set; }
    public IEnumerable<PropertyConfiguration> Properties => _properties.Values;
    public IEnumerable<PropertyConfiguration> DelegateProperties => _delegateProperties.Values;
    public IEnumerable<CustomPropertyConfiguration> CustomProperties => _customProperties.Values;
    public IEnumerable<DrillDownEventRouteTemplate> EventRoutes => _eventRoutes;

    public void AddEventRoute(DrillDownEventRouteTemplate route)
    {
        _eventRoutes.Add(route ?? throw new ArgumentNullException(nameof(route)));
    }

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

    public PropertyConfiguration AddDelegateProperty(string name, Type valueType, Func<object, object> value)
    {
        PropertyConfiguration configuration = new(name, valueType, value);
        _delegateProperties.Add(name, configuration);
        return configuration;
    }

    public TypeConfiguration Clone()
    {
        TypeConfiguration clone = new(Type) { IncludeAll = IncludeAll };
        foreach (PropertyConfiguration property in _properties.Values)
            clone._properties.Add(GetPropertyKey(property.Property), property.Clone());
        foreach (PropertyConfiguration property in _delegateProperties.Values)
            clone._delegateProperties.Add(property.Name.Value, property.Clone());
        foreach (CustomPropertyConfiguration property in _customProperties.Values)
            clone._customProperties.Add(property.Name, property.Clone());
        clone._eventRoutes.AddRange(_eventRoutes.Select(route => route.Clone()));
        return clone;
    }

    public void Merge(TypeConfiguration source)
    {
        if (source.IncludeAll.HasValue)
            IncludeAll = source.IncludeAll;

        foreach (PropertyConfiguration sourceProperty in source.Properties)
        {
            PropertyConfiguration target = GetOrAdd(sourceProperty.Property);
            target.Merge(sourceProperty);
        }

        foreach (PropertyConfiguration sourceProperty in source.DelegateProperties)
            _delegateProperties[sourceProperty.Name.Value] = sourceProperty.Clone();

        foreach (CustomPropertyConfiguration sourceProperty in source.CustomProperties)
            _customProperties[sourceProperty.Name] = sourceProperty.Clone();

        _eventRoutes.AddRange(source.EventRoutes.Select(route => route.Clone()));
    }

    private static string GetPropertyKey(PropertyInfo property)
    {
        return property.Name;
    }
}

public sealed class DrillDownEventRoute
{
    internal string Category { get; private set; }
    internal string Name { get; private set; }
    internal LogLevel? MinLevel { get; private set; }
    internal LogLevel? MaxLevel { get; private set; }

    public DrillDownEventRoute To(string category, string name)
    {
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("An event-view category is required.", nameof(category));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("An event-view name is required.", nameof(name));

        Category = category;
        Name = name;
        return this;
    }

    public DrillDownEventRoute AtLeast(LogLevel minLevel)
    {
        MinLevel = minLevel;
        return this;
    }

    public DrillDownEventRoute AtMost(LogLevel maxLevel)
    {
        MaxLevel = maxLevel;
        return this;
    }

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(Category) || string.IsNullOrWhiteSpace(Name))
            throw new InvalidOperationException("A drilldown event route must define one destination.");
        if (MinLevel > MaxLevel)
            throw new InvalidOperationException("A drilldown event route minimum level cannot exceed its maximum level.");
    }
}

internal sealed class DrillDownEventRouteTemplate
{
    public DrillDownEventRouteTemplate(string staticLoggerName, LoggerNameMatchMode matchMode, DrillDownEventRoute route)
    {
        LoggerName = staticLoggerName;
        MatchMode = matchMode;
        Route = route;
    }

    public DrillDownEventRouteTemplate(Func<object, string> loggerName, LoggerNameMatchMode matchMode, DrillDownEventRoute route)
    {
        LoggerNameFactory = loggerName;
        MatchMode = matchMode;
        Route = route;
    }

    public string LoggerName { get; }
    public Func<object, string> LoggerNameFactory { get; }
    public LoggerNameMatchMode MatchMode { get; }
    public DrillDownEventRoute Route { get; }

    public string ResolveLoggerName(object target) => LoggerNameFactory?.Invoke(target) ?? LoggerName;

    public DrillDownEventRouteTemplate Clone()
    {
        DrillDownEventRoute route = new DrillDownEventRoute().To(Route.Category, Route.Name);
        if (Route.MinLevel.HasValue)
            route.AtLeast(Route.MinLevel.Value);
        if (Route.MaxLevel.HasValue)
            route.AtMost(Route.MaxLevel.Value);
        return LoggerNameFactory == null
            ? new DrillDownEventRouteTemplate(LoggerName, MatchMode, route)
            : new DrillDownEventRouteTemplate(LoggerNameFactory, MatchMode, route);
    }
}

internal sealed class PropertyConfiguration
{
    public PropertyConfiguration(PropertyInfo property)
    {
        Property = property;
    }

    public PropertyConfiguration(string name, Type valueType, Func<object, object> value)
    {
        Name = new ConfiguredValue<string>(name);
        ValueType = valueType;
        Value = value;
        UsesPropertyDefaults = true;
    }

    public PropertyInfo Property { get; }
    public Type ValueType { get; }
    public Func<object, object> Value { get; private set; }
    public bool? Included { get; set; }
    public bool UsesPropertyDefaults { get; set; }
    public PropertyStrategy? Strategy { get; set; }
    public ConfiguredValue<string> Name { get; set; }
    public Func<object, string> NameFormatter { get; set; }
    public ConfiguredValue<string> Category { get; set; }
    public Func<object, string> CategoryFormatter { get; set; }
    public ConfiguredValue<bool> CategoryInitiallyExpanded { get; set; }
    public ConfiguredValue<string> CategoryExpansionScope { get; set; }
    public ConfiguredValue<string> Description { get; set; }
    public Func<object, string> DescriptionFormatter { get; set; }
    public List<PropertyAlertConfiguration> Alerts { get; } = new();
    public List<PropertyStatusConfiguration> Statuses { get; } = new();
    public ConfiguredValue<string> FormatString { get; set; }
    public Func<object, string> ValueFormatter { get; set; }
    public Func<object, string> TextFormatter { get; set; }
    public ConfiguredValue<StatusIconSize> StatusIconSize { get; set; }
    public ConfiguredValue<bool> AllowSet { get; set; }
    public ConfiguredValue<bool> ExposeRate { get; set; }
    public ConfiguredValue<bool> ExposeTotal { get; set; }
    public ConfiguredValue<bool> ExposeDate { get; set; }
    public ConfiguredValue<bool> ExposeElapsed { get; set; }
    public ConfiguredValue<bool> ExposeTimeUntil { get; set; }
    public ConfiguredValue<bool> InitiallyExpanded { get; set; }
    public ConfiguredValue<bool> PrimaryPropertiesOnly { get; set; }
    public ConfiguredValue<int> MaxItems { get; set; }
    public ConfiguredValue<bool> NoTruncate { get; set; }
    public ConfiguredValue<bool> DrillDown { get; set; }
    public ConfiguredValue<int> DrillDownMaxItems { get; set; }
    public ConfiguredValue<bool> DrillDownIconOnly { get; set; }
    public ConfiguredValue<string> DrillDownText { get; set; }
    public Func<object, string> DrillDownTextFormatter { get; set; }
    public ConfiguredValue<bool> JsonHover { get; set; }
    public ConfiguredValue<bool> ExpandedHover { get; set; }
    public List<CollectionOutputConfiguration> CollectionOutputs { get; } = new();

    public PropertyConfiguration Clone()
    {
        PropertyConfiguration clone =
            Property == null ? new PropertyConfiguration(Name.Value, ValueType, Value) : new PropertyConfiguration(Property);
        clone.Merge(this);
        return clone;
    }

    public PropertyConfiguration Bind(object source)
    {
        PropertyConfiguration bound = Clone();
        bound.Value = _ => Value(source);
        bound.NameFormatter = NameFormatter == null ? null : _ => NameFormatter(source);
        bound.CategoryFormatter = CategoryFormatter == null ? null : _ => CategoryFormatter(source);
        bound.DescriptionFormatter = DescriptionFormatter == null ? null : _ => DescriptionFormatter(source);
        bound.TextFormatter = TextFormatter == null ? null : _ => TextFormatter(source);
        bound.DrillDownTextFormatter = DrillDownTextFormatter == null ? null : _ => DrillDownTextFormatter(source);
        bound.Alerts.Clear();
        bound.Alerts.AddRange(
            Alerts.Select(alert => new PropertyAlertConfiguration(
                alert.Severity,
                _ => alert.Condition(source),
                _ => alert.Message(source),
                alert.Category == null ? null : _ => alert.Category(source)
            ))
        );
        bound.Statuses.AddRange(
            Statuses.Select(status => new PropertyStatusConfiguration(status.Status, _ => status.Condition(source), _ => status.Text(source)))
        );
        return bound;
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
        CategoryInitiallyExpanded = source.CategoryInitiallyExpanded.Or(CategoryInitiallyExpanded);
        CategoryExpansionScope = source.CategoryExpansionScope.Or(CategoryExpansionScope);
        Description = source.Description.Or(Description);
        DescriptionFormatter = source.DescriptionFormatter ?? DescriptionFormatter;
        FormatString = source.FormatString.Or(FormatString);
        ValueFormatter = source.ValueFormatter ?? ValueFormatter;
        TextFormatter = source.TextFormatter ?? TextFormatter;
        StatusIconSize = source.StatusIconSize.Or(StatusIconSize);
        AllowSet = source.AllowSet.Or(AllowSet);
        ExposeRate = source.ExposeRate.Or(ExposeRate);
        ExposeTotal = source.ExposeTotal.Or(ExposeTotal);
        ExposeDate = source.ExposeDate.Or(ExposeDate);
        ExposeElapsed = source.ExposeElapsed.Or(ExposeElapsed);
        ExposeTimeUntil = source.ExposeTimeUntil.Or(ExposeTimeUntil);
        InitiallyExpanded = source.InitiallyExpanded.Or(InitiallyExpanded);
        PrimaryPropertiesOnly = source.PrimaryPropertiesOnly.Or(PrimaryPropertiesOnly);
        MaxItems = source.MaxItems.Or(MaxItems);
        NoTruncate = source.NoTruncate.Or(NoTruncate);
        DrillDown = source.DrillDown.Or(DrillDown);
        DrillDownMaxItems = source.DrillDownMaxItems.Or(DrillDownMaxItems);
        DrillDownIconOnly = source.DrillDownIconOnly.Or(DrillDownIconOnly);
        DrillDownText = source.DrillDownText.Or(DrillDownText);
        DrillDownTextFormatter = source.DrillDownTextFormatter ?? DrillDownTextFormatter;
        JsonHover = source.JsonHover.Or(JsonHover);
        ExpandedHover = source.ExpandedHover.Or(ExpandedHover);
        Alerts.AddRange(source.Alerts.Select(alert => alert.Clone()));
        Statuses.AddRange(source.Statuses.Select(status => status.Clone()));
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

internal sealed class PropertyStatusConfiguration
{
    public PropertyStatusConfiguration(StatusCode status, Func<object, bool> condition, Func<object, string> text)
    {
        Status = status;
        Condition = condition;
        Text = text;
    }

    public StatusCode Status { get; }
    public Func<object, bool> Condition { get; }
    public Func<object, string> Text { get; }

    public PropertyStatusConfiguration Clone() => new(Status, Condition, Text);
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
    public ConfiguredValue<bool> CategoryInitiallyExpanded { get; set; }
    public ConfiguredValue<string> CategoryExpansionScope { get; set; }
    public ConfiguredValue<string> Description { get; set; }
    public Func<object, string> DescriptionFormatter { get; set; }
    public Func<object, string> ValueFormatter { get; set; }
    public List<PropertyAlertConfiguration> Alerts { get; } = new();
    public List<PropertyStatusConfiguration> Statuses { get; } = new();
    public ConfiguredValue<bool> InitiallyExpanded { get; set; }
    public ConfiguredValue<bool> DrillDown { get; set; }
    public ConfiguredValue<int> DrillDownMaxItems { get; set; }
    public ConfiguredValue<bool> DrillDownIconOnly { get; set; }
    public ConfiguredValue<string> DrillDownText { get; set; }
    public Func<object, string> DrillDownTextFormatter { get; set; }
    public ConfiguredValue<bool> JsonHover { get; set; }
    public ConfiguredValue<bool> ExpandedHover { get; set; }

    public CustomPropertyConfiguration Clone()
    {
        CustomPropertyConfiguration clone = new(Name, Value)
        {
            Category = Category,
            CategoryFormatter = CategoryFormatter,
            CategoryInitiallyExpanded = CategoryInitiallyExpanded,
            CategoryExpansionScope = CategoryExpansionScope,
            Description = Description,
            DescriptionFormatter = DescriptionFormatter,
            ValueFormatter = ValueFormatter,
            InitiallyExpanded = InitiallyExpanded,
            DrillDown = DrillDown,
            DrillDownMaxItems = DrillDownMaxItems,
            DrillDownIconOnly = DrillDownIconOnly,
            DrillDownText = DrillDownText,
            DrillDownTextFormatter = DrillDownTextFormatter,
            JsonHover = JsonHover,
            ExpandedHover = ExpandedHover,
        };
        clone.Alerts.AddRange(Alerts.Select(alert => alert.Clone()));
        clone.Statuses.AddRange(Statuses.Select(status => status.Clone()));
        return clone;
    }

    public CustomPropertyConfiguration Bind(object source)
    {
        CustomPropertyConfiguration bound = new(Name, _ => Value(source))
        {
            Category = Category,
            CategoryFormatter = CategoryFormatter == null ? null : _ => CategoryFormatter(source),
            CategoryInitiallyExpanded = CategoryInitiallyExpanded,
            CategoryExpansionScope = CategoryExpansionScope,
            Description = Description,
            DescriptionFormatter = DescriptionFormatter == null ? null : _ => DescriptionFormatter(source),
            ValueFormatter = ValueFormatter,
            InitiallyExpanded = InitiallyExpanded,
            DrillDown = DrillDown,
            DrillDownMaxItems = DrillDownMaxItems,
            DrillDownIconOnly = DrillDownIconOnly,
            DrillDownText = DrillDownText,
            DrillDownTextFormatter = DrillDownTextFormatter == null ? null : _ => DrillDownTextFormatter(source),
            JsonHover = JsonHover,
            ExpandedHover = ExpandedHover,
        };
        bound.Alerts.AddRange(
            Alerts.Select(alert => new PropertyAlertConfiguration(
                alert.Severity,
                _ => alert.Condition(source),
                _ => alert.Message(source),
                alert.Category == null ? null : _ => alert.Category(source)
            ))
        );
        bound.Statuses.AddRange(
            Statuses.Select(status => new PropertyStatusConfiguration(status.Status, _ => status.Condition(source), _ => status.Text(source)))
        );
        return bound;
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
    public ConfiguredValue<bool> NoTruncate { get; set; }
    public ConfiguredValue<bool> DrillDown { get; set; }
    public ConfiguredValue<int> DrillDownMaxItems { get; set; }
    public ConfiguredValue<bool> DrillDownIconOnly { get; set; }
    public ConfiguredValue<string> DrillDownText { get; set; }
    public Func<object, string> DrillDownTextFormatter { get; set; }
    public ConfiguredValue<bool> JsonHover { get; set; }
    public ConfiguredValue<bool> ExpandedHover { get; set; }
    public bool InitiallyExpanded { get; set; } = true;
    public bool PrimaryPropertiesOnly { get; set; }
    public List<PropertyStatusConfiguration> ItemStatuses { get; set; } = new();
    public ConfiguredValue<StatusIconSize> ItemStatusIconSize { get; set; }

    public CollectionOutputConfiguration Clone()
    {
        CollectionOutputConfiguration clone = (CollectionOutputConfiguration)MemberwiseClone();
        clone.ItemStatuses = ItemStatuses.Select(status => status.Clone()).ToList();
        return clone;
    }
}

internal sealed class TypeConfigurator<T> : ITypeConfigurator<T>
{
    private readonly TypeConfiguration _configuration;
    private readonly System.Threading.AsyncLocal<CategoryScope> _categoryScope = new();

    public TypeConfigurator(TypeConfiguration configuration)
    {
        _configuration = configuration;
    }

    public ITypeConfigurator<T> ExcludeAll()
    {
        _configuration.IncludeAll = false;
        return this;
    }

    public ITypeConfigurator<T> IncludeAll()
    {
        _configuration.IncludeAll = true;
        return this;
    }

    public ICategoryScope CreateCategoryScope(string category)
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
        if (ExpressionProperty.TryGetDirectField(property, typeof(T), out FieldInfo field))
        {
            PropertyConfiguration fieldConfiguration = AddDelegateProperty(field.Name, typeof(TProperty), property.Compile(), null);
            return new PropertyConfigurator<T, TProperty>(fieldConfiguration);
        }

        PropertyConfiguration configuration = GetProperty(property);
        configuration.Included = true;
        configuration.UsesPropertyDefaults = true;
        ApplyCategoryScope(configuration);
        return new PropertyConfigurator<T, TProperty>(configuration);
    }

    public IPropertyConfigurator<T, TProperty> Property<TProperty>(string name, Func<T, TProperty> value) => ConfigureProperty(name, value);

    public ICustomPropertyConfigurator<T> Custom(string name, Action<ICustomObjectConfigurator<T>> configure)
    {
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        InlineCustomObjectConfigurator<T> projection = new();
        configure(projection);
        CustomPropertyConfiguration configuration = _configuration.AddCustomProperty(
            name,
            item => new InlineCustomObject<T>((T)item, projection.Members)
        );
        ApplyCategoryScope(configuration);
        return new CustomPropertyConfigurator<T>(configuration);
    }

    public ITypeConfigurator<T> Route(string loggerName, LoggerNameMatchMode matchMode, Action<DrillDownEventRoute> configure)
    {
        if (string.IsNullOrWhiteSpace(loggerName))
            throw new ArgumentException("A logger name is required.", nameof(loggerName));
        return AddEventRoute(new DrillDownEventRouteTemplate(loggerName, matchMode, ConfigureRoute(configure)));
    }

    public ITypeConfigurator<T> Route(Func<T, string> loggerName, LoggerNameMatchMode matchMode, Action<DrillDownEventRoute> configure)
    {
        if (loggerName == null)
            throw new ArgumentNullException(nameof(loggerName));

        return AddEventRoute(new DrillDownEventRouteTemplate(target => loggerName((T)target), matchMode, ConfigureRoute(configure)));
    }

    private PropertyConfiguration GetProperty(LambdaExpression expression)
    {
        return _configuration.GetOrAdd(ExpressionProperty.Get(expression, typeof(T)));
    }

    private PropertyConfiguration AddDelegateProperty<TProperty>(string name, Type valueType, Func<T, TProperty> value, PropertyStrategy? strategy)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A property name is required.", nameof(name));
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        PropertyConfiguration configuration = _configuration.AddDelegateProperty(name, valueType, item => value((T)item));
        configuration.Strategy = strategy;
        ApplyCategoryScope(configuration);
        return configuration;
    }

    private IPropertyConfigurator<T, TProperty> ConfigureProperty<TProperty>(string name, Func<T, TProperty> value)
    {
        PropertyConfiguration configuration = AddDelegateProperty(name, typeof(TProperty), value, null);
        return new PropertyConfigurator<T, TProperty>(configuration);
    }

    private void ApplyCategoryScope(PropertyConfiguration configuration)
    {
        CategoryScope scope = _categoryScope.Value;
        if (scope != null)
        {
            configuration.Category = new ConfiguredValue<string>(scope.Category);
            ApplyCategoryExpansion(configuration, scope);
        }
    }

    private void ApplyCategoryScope(CustomPropertyConfiguration configuration)
    {
        CategoryScope scope = _categoryScope.Value;
        if (scope != null)
        {
            configuration.Category = new ConfiguredValue<string>(scope.Category);
            ApplyCategoryExpansion(configuration, scope);
        }
    }

    private static void ApplyCategoryExpansion(PropertyConfiguration configuration, CategoryScope scope)
    {
        if (!scope.InitiallyExpanded.HasValue)
            return;

        configuration.CategoryInitiallyExpanded = new ConfiguredValue<bool>(scope.InitiallyExpanded.Value);
        configuration.CategoryExpansionScope = new ConfiguredValue<string>(scope.Category);
    }

    private static void ApplyCategoryExpansion(CustomPropertyConfiguration configuration, CategoryScope scope)
    {
        if (!scope.InitiallyExpanded.HasValue)
            return;

        configuration.CategoryInitiallyExpanded = new ConfiguredValue<bool>(scope.InitiallyExpanded.Value);
        configuration.CategoryExpansionScope = new ConfiguredValue<string>(scope.Category);
    }

    private ITypeConfigurator<T> AddEventRoute(DrillDownEventRouteTemplate route)
    {
        if (!Enum.IsDefined(typeof(LoggerNameMatchMode), route.MatchMode))
            throw new ArgumentOutOfRangeException(nameof(route));
        route.Route.Validate();
        _configuration.AddEventRoute(route);
        return this;
    }

    private static DrillDownEventRoute ConfigureRoute(Action<DrillDownEventRoute> configure)
    {
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        DrillDownEventRoute route = new();
        configure(route);
        return route;
    }

    private sealed class CategoryScope : ICategoryScope
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
        public bool? InitiallyExpanded { get; private set; }

        public ICategoryScope Expanded(bool expanded = true)
        {
            InitiallyExpanded = expanded;
            return this;
        }

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

    public IPropertyConfigurator WithLabel(string label)
    {
        Configuration.Name = new ConfiguredValue<string>(label);
        return this;
    }

    public IPropertyConfigurator WithCategory(string category)
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

    public new TSelf WithLabel(string label)
    {
        base.WithLabel(label);
        return Self;
    }

    public TSelf WithLabel(Func<T, string> label)
    {
        if (label == null)
            throw new ArgumentNullException(nameof(label));

        Configuration.NameFormatter = item => label((T)item);
        return Self;
    }

    public new TSelf WithCategory(string category)
    {
        base.WithCategory(category);
        return Self;
    }

    public TSelf WithCategory(Func<T, string> category)
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

internal interface IDateStrategyConfigurator
{
    void ConfigureDate(bool? exposeDate, bool? exposeElapsed, bool? exposeTimeUntil);
}

internal interface IRateStrategyConfigurator
{
    void ConfigureRate(bool? exposeRate, bool? exposeTotal);
}

internal interface IExtendedStrategyConfigurator
{
    void ConfigureExtended(bool initiallyExpanded);
    void ConfigurePrimaryPropertiesOnly();
}

internal interface ICollectionStrategyConfigurator<T>
{
    ICollectionConfigurator<T, TItem> ConfigureCollection<TItem>();
}

internal sealed class PropertyConfigurator<T, TProperty>
    : ObjectPropertyConfigurator<T, IPropertyConfigurator<T, TProperty>>,
        IPropertyConfigurator<T, TProperty>,
        IDateStrategyConfigurator,
        IRateStrategyConfigurator,
        IExtendedStrategyConfigurator,
        ICollectionStrategyConfigurator<T>
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

    public IPropertyConfigurator<T, TProperty> WithText(string text)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));

        return WithText(_ => text);
    }

    public IPropertyConfigurator<T, TProperty> WithText(Func<T, string> text)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));

        Configuration.TextFormatter = item => text((T)item);
        return this;
    }

    public IPropertyConfigurator<T, TProperty> WithIconSize(StatusIconSize size)
    {
        if (!Enum.IsDefined(typeof(StatusIconSize), size))
            throw new ArgumentOutOfRangeException(nameof(size));

        Configuration.StatusIconSize = new ConfiguredValue<StatusIconSize>(size);
        return this;
    }

    public IPropertyConfigurator<T, TProperty> AsJson(int maxLength = 100)
    {
        if (maxLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxLength), "JSON max length must be greater than zero.");

        Configuration.ValueFormatter = value =>
        {
            string json = JsonSerializer.Serialize((TProperty)value);
            return json.Substring(0, Math.Min(maxLength, json.Length));
        };
        return this;
    }

    public IPropertyConfigurator<T, TProperty> AsDateOnly()
    {
        Type valueType = Nullable.GetUnderlyingType(typeof(TProperty)) ?? typeof(TProperty);
        if (valueType != typeof(DateTime) && valueType != typeof(DateTimeOffset))
            throw new InvalidOperationException("Date-only formatting requires a DateTime or DateTimeOffset property.");

        Configuration.ValueFormatter = value =>
        {
            if (value is DateTime date)
                return date.ToString("d");
            if (value is DateTimeOffset dateTimeOffset)
                return dateTimeOffset.ToString("d");
            return null;
        };
        return this;
    }

    public IPropertyConfigurator<T, TProperty> WithDrillDown(bool enabled = true, int? maxItems = null)
    {
        ConfigureDrillDown(Configuration, enabled, maxItems, false);
        return this;
    }

    public IPropertyConfigurator<T, TProperty> WithDrillDownOnly(int? maxItems = null)
    {
        ConfigureDrillDown(Configuration, true, maxItems, true);
        return this;
    }

    public IPropertyConfigurator<T, TProperty> WithDrillDownOnly(string text, int? maxItems = null)
    {
        ConfigureDrillDown(Configuration, true, maxItems, true, text);
        return this;
    }

    public IPropertyConfigurator<T, TProperty> WithDrillDownOnly(Func<T, string> text, int? maxItems = null)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));

        ConfigureDrillDown(Configuration, true, maxItems, true);
        Configuration.DrillDownTextFormatter = item => text((T)item);
        return this;
    }

    public IPropertyConfigurator<T, TProperty> WithJsonHover(bool enabled = true)
    {
        Configuration.JsonHover = new ConfiguredValue<bool>(enabled);
        Configuration.ExpandedHover = new ConfiguredValue<bool>(false);
        return this;
    }

    public IPropertyConfigurator<T, TProperty> WithExpandedHover(bool enabled = true)
    {
        Configuration.JsonHover = new ConfiguredValue<bool>(false);
        Configuration.ExpandedHover = new ConfiguredValue<bool>(enabled);
        return this;
    }

    public IPropertyConfigurator<T, TProperty> WithStatus(StatusCode status, Func<T, bool> condition)
    {
        return WithStatus(status, condition, status.ToString());
    }

    public IPropertyConfigurator<T, TProperty> WithStatus(StatusCode status, Func<T, bool> condition, string text)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));

        return WithStatus(status, condition, _ => text);
    }

    public IPropertyConfigurator<T, TProperty> WithStatus(StatusCode status, Func<T, bool> condition, Func<T, string> text)
    {
        return AddStatus(status, condition, text);
    }

    public IPropertyConfigurator<T, TProperty> Warn(Func<T, bool> condition)
    {
        return Warn(condition, "Warning");
    }

    void IDateStrategyConfigurator.ConfigureDate(bool? exposeDate, bool? exposeElapsed, bool? exposeTimeUntil)
    {
        Configuration.Strategy = PropertyStrategy.Date;
        if (exposeDate.HasValue)
            Configuration.ExposeDate = new ConfiguredValue<bool>(exposeDate.Value);
        if (exposeElapsed.HasValue)
            Configuration.ExposeElapsed = new ConfiguredValue<bool>(exposeElapsed.Value);
        if (exposeTimeUntil.HasValue)
            Configuration.ExposeTimeUntil = new ConfiguredValue<bool>(exposeTimeUntil.Value);
    }

    void IRateStrategyConfigurator.ConfigureRate(bool? exposeRate, bool? exposeTotal)
    {
        Configuration.Strategy = PropertyStrategy.Rate;
        if (exposeRate.HasValue)
            Configuration.ExposeRate = new ConfiguredValue<bool>(exposeRate.Value);
        if (exposeTotal.HasValue)
            Configuration.ExposeTotal = new ConfiguredValue<bool>(exposeTotal.Value);
    }

    void IExtendedStrategyConfigurator.ConfigureExtended(bool initiallyExpanded)
    {
        Configuration.Strategy = PropertyStrategy.Extended;
        Configuration.InitiallyExpanded = new ConfiguredValue<bool>(initiallyExpanded);
    }

    void IExtendedStrategyConfigurator.ConfigurePrimaryPropertiesOnly()
    {
        Configuration.PrimaryPropertiesOnly = new ConfiguredValue<bool>(true);
    }

    ICollectionConfigurator<T, TItem> ICollectionStrategyConfigurator<T>.ConfigureCollection<TItem>()
    {
        Configuration.Strategy = PropertyStrategy.Collection;
        return new CollectionConfigurator<T, TItem>(Configuration);
    }

    private static void ConfigureDrillDown(PropertyConfiguration configuration, bool enabled, int? maxItems, bool iconOnly, string text = null)
    {
        if (maxItems <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxItems), "Drilldown max items must be greater than zero.");
        if (text != null && string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Drilldown text is required.", nameof(text));

        configuration.DrillDown = new ConfiguredValue<bool>(enabled);
        configuration.DrillDownIconOnly = new ConfiguredValue<bool>(iconOnly);
        if (text != null)
            configuration.DrillDownText = new ConfiguredValue<string>(text);
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

    public IPropertyConfigurator<T, TProperty> Error(Func<T, bool> condition)
    {
        return Error(condition, "Error");
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

    private IPropertyConfigurator<T, TProperty> AddStatus(StatusCode status, Func<T, bool> condition, Func<T, string> text)
    {
        if (!Enum.IsDefined(typeof(StatusCode), status))
            throw new ArgumentOutOfRangeException(nameof(status));
        if (condition == null)
            throw new ArgumentNullException(nameof(condition));
        if (text == null)
            throw new ArgumentNullException(nameof(text));

        Configuration.Statuses.Add(new PropertyStatusConfiguration(status, item => condition((T)item), item => text((T)item)));
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

    public ICustomPropertyConfigurator<T> AsJson(int maxLength = 100)
    {
        if (maxLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxLength), "JSON max length must be greater than zero.");

        _configuration.ValueFormatter = value =>
        {
            string json = JsonSerializer.Serialize(value);
            return json.Substring(0, Math.Min(maxLength, json.Length));
        };
        return this;
    }

    public ICustomPropertyConfigurator<T> Expand(bool initiallyExpanded = true)
    {
        _configuration.InitiallyExpanded = new ConfiguredValue<bool>(initiallyExpanded);
        return this;
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

    public ICustomPropertyConfigurator<T> WithDrillDownOnly(int? maxItems = null)
    {
        if (maxItems <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxItems), "Drilldown max items must be greater than zero.");

        _configuration.DrillDown = new ConfiguredValue<bool>(true);
        _configuration.DrillDownIconOnly = new ConfiguredValue<bool>(true);
        if (maxItems.HasValue)
            _configuration.DrillDownMaxItems = new ConfiguredValue<int>(maxItems.Value);
        return this;
    }

    public ICustomPropertyConfigurator<T> WithDrillDownOnly(string text, int? maxItems = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Drilldown text is required.", nameof(text));

        WithDrillDownOnly(maxItems);
        _configuration.DrillDownText = new ConfiguredValue<string>(text);
        return this;
    }

    public ICustomPropertyConfigurator<T> WithDrillDownOnly(Func<T, string> text, int? maxItems = null)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));

        WithDrillDownOnly(maxItems);
        _configuration.DrillDownTextFormatter = item => text((T)item);
        return this;
    }

    public ICustomPropertyConfigurator<T> WithJsonHover(bool enabled = true)
    {
        _configuration.JsonHover = new ConfiguredValue<bool>(enabled);
        _configuration.ExpandedHover = new ConfiguredValue<bool>(false);
        return this;
    }

    public ICustomPropertyConfigurator<T> WithExpandedHover(bool enabled = true)
    {
        _configuration.JsonHover = new ConfiguredValue<bool>(false);
        _configuration.ExpandedHover = new ConfiguredValue<bool>(enabled);
        return this;
    }

    public ICustomPropertyConfigurator<T> Warn(Func<T, bool> condition)
    {
        return Warn(condition, "Warning");
    }

    public ICustomPropertyConfigurator<T> WithCategory(string category)
    {
        _configuration.Category = new ConfiguredValue<string>(category);
        return this;
    }

    public ICustomPropertyConfigurator<T> WithCategory(Func<T, string> category)
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

    public ICustomPropertyConfigurator<T> Error(Func<T, bool> condition)
    {
        return Error(condition, "Error");
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

    public ICustomPropertyConfigurator<T> WithStatus(StatusCode status, Func<T, bool> condition)
    {
        return WithStatus(status, condition, status.ToString());
    }

    public ICustomPropertyConfigurator<T> WithStatus(StatusCode status, Func<T, bool> condition, string text)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));

        return WithStatus(status, condition, _ => text);
    }

    public ICustomPropertyConfigurator<T> WithStatus(StatusCode status, Func<T, bool> condition, Func<T, string> text)
    {
        return AddStatus(status, condition, text);
    }

    private ICustomPropertyConfigurator<T> AddStatus(StatusCode status, Func<T, bool> condition, Func<T, string> text)
    {
        if (!Enum.IsDefined(typeof(StatusCode), status))
            throw new ArgumentOutOfRangeException(nameof(status));
        if (condition == null)
            throw new ArgumentNullException(nameof(condition));
        if (text == null)
            throw new ArgumentNullException(nameof(text));

        _configuration.Statuses.Add(new PropertyStatusConfiguration(status, item => condition((T)item), item => text((T)item)));
        return this;
    }
}

internal sealed class CollectionConfigurator<T, TItem>
    : ObjectPropertyConfigurator<T, ICollectionConfigurator<T, TItem>>,
        ICollectionConfigurator<T, TItem>
{
    private CollectionOutputConfiguration _lastOutput;

    public CollectionConfigurator(PropertyConfiguration configuration)
        : base(configuration) { }

    public ICollectionConfigurator<T, TItem> ShowCount(string name = null)
    {
        AddOutput(CollectionMode.Count, name);
        return this;
    }

    public ICollectionConfigurator<T, TItem> ConcatItems(string separator = null, Func<TItem, string> format = null)
    {
        CollectionOutputConfiguration output = AddOutput(CollectionMode.Concatenate, null);
        output.Separator = separator;
        output.ValueFormatter = format == null ? null : item => format((TItem)item);
        return this;
    }

    public ICollectionConfigurator<T, TItem> ConcatItems(Func<TItem, string> format) => ConcatItems(null, format);

    public ICollectionConfigurator<T, TItem> ListItems(Action<ICollectionListConfigurator<TItem>> configure = null)
    {
        CollectionOutputConfiguration output = AddOutput(CollectionMode.List, null);
        configure?.Invoke(new CollectionListConfigurator<TItem>(output));
        return this;
    }

    public ICollectionConfigurator<T, TItem> ExpandItems(
        Action<ICollectionExpandedItemConfigurator<TItem>> configure = null,
        string name = null,
        bool initiallyExpanded = true
    )
    {
        CollectionOutputConfiguration output = AddOutput(CollectionMode.ExpandedItems, name);
        output.InitiallyExpanded = initiallyExpanded;
        configure?.Invoke(new CollectionExpandedItemConfigurator<TItem>(output));
        return this;
    }

    public ICollectionConfigurator<T, TItem> WithPrimaryPropertiesOnly()
    {
        if (_lastOutput?.Mode != CollectionMode.ExpandedItems)
            throw new InvalidOperationException("WithPrimaryPropertiesOnly requires ExpandItems to be configured first.");

        _lastOutput.PrimaryPropertiesOnly = true;
        return this;
    }

    public ICollectionConfigurator<T, TItem> WithMaxItems(int maxItems)
    {
        if (maxItems <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxItems), "Max items must be greater than zero.");

        Configuration.MaxItems = new ConfiguredValue<int>(maxItems);
        return this;
    }

    public ICollectionConfigurator<T, TItem> WithTextWrap()
    {
        if (_lastOutput == null)
            Configuration.NoTruncate = new ConfiguredValue<bool>(true);
        else
            _lastOutput.NoTruncate = new ConfiguredValue<bool>(true);
        return this;
    }

    public ICollectionConfigurator<T, TItem> WithDrillDown(bool enabled = true, int? maxItems = null)
    {
        if (maxItems <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxItems), "Drilldown max items must be greater than zero.");

        if (_lastOutput == null)
        {
            Configuration.DrillDown = new ConfiguredValue<bool>(enabled);
            if (maxItems.HasValue)
                Configuration.DrillDownMaxItems = new ConfiguredValue<int>(maxItems.Value);
        }
        else
        {
            _lastOutput.DrillDown = new ConfiguredValue<bool>(enabled);
            if (maxItems.HasValue)
                _lastOutput.DrillDownMaxItems = new ConfiguredValue<int>(maxItems.Value);
        }
        return this;
    }

    public ICollectionConfigurator<T, TItem> WithDrillDownOnly(int? maxItems = null)
    {
        if (maxItems <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxItems), "Drilldown max items must be greater than zero.");

        if (_lastOutput == null)
        {
            Configuration.DrillDown = new ConfiguredValue<bool>(true);
            Configuration.DrillDownIconOnly = new ConfiguredValue<bool>(true);
            if (maxItems.HasValue)
                Configuration.DrillDownMaxItems = new ConfiguredValue<int>(maxItems.Value);
        }
        else
        {
            _lastOutput.DrillDown = new ConfiguredValue<bool>(true);
            _lastOutput.DrillDownIconOnly = new ConfiguredValue<bool>(true);
            if (maxItems.HasValue)
                _lastOutput.DrillDownMaxItems = new ConfiguredValue<int>(maxItems.Value);
        }
        return this;
    }

    public ICollectionConfigurator<T, TItem> WithDrillDownOnly(string text, int? maxItems = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Drilldown text is required.", nameof(text));

        WithDrillDownOnly(maxItems);
        if (_lastOutput == null)
            Configuration.DrillDownText = new ConfiguredValue<string>(text);
        else
            _lastOutput.DrillDownText = new ConfiguredValue<string>(text);
        return this;
    }

    public ICollectionConfigurator<T, TItem> WithDrillDownOnly(Func<T, string> text, int? maxItems = null)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));

        WithDrillDownOnly(maxItems);
        if (_lastOutput == null)
            Configuration.DrillDownTextFormatter = item => text((T)item);
        else
            _lastOutput.DrillDownTextFormatter = item => text((T)item);
        return this;
    }

    public ICollectionConfigurator<T, TItem> WithJsonHover(bool enabled = true)
    {
        if (_lastOutput == null)
        {
            Configuration.JsonHover = new ConfiguredValue<bool>(enabled);
            Configuration.ExpandedHover = new ConfiguredValue<bool>(false);
        }
        else
        {
            _lastOutput.JsonHover = new ConfiguredValue<bool>(enabled);
            _lastOutput.ExpandedHover = new ConfiguredValue<bool>(false);
        }
        return this;
    }

    public ICollectionConfigurator<T, TItem> WithExpandedHover(bool enabled = true)
    {
        if (_lastOutput == null)
        {
            Configuration.JsonHover = new ConfiguredValue<bool>(false);
            Configuration.ExpandedHover = new ConfiguredValue<bool>(enabled);
        }
        else
        {
            _lastOutput.JsonHover = new ConfiguredValue<bool>(false);
            _lastOutput.ExpandedHover = new ConfiguredValue<bool>(enabled);
        }
        return this;
    }

    private CollectionOutputConfiguration AddOutput(CollectionMode mode, string name)
    {
        CollectionOutputConfiguration output = new() { Mode = mode, Name = name };
        Configuration.CollectionOutputs.Add(output);
        _lastOutput = output;
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

    public ICollectionListConfigurator<TItem> WithName(Func<TItem, string> format)
    {
        _output.NameFormatter = CreateFormatter(format);
        return this;
    }

    public ICollectionListConfigurator<TItem> WithValue(Func<TItem, string> format)
    {
        _output.ValueFormatter = CreateFormatter(format);
        return this;
    }

    public ICollectionListConfigurator<TItem> AsJson(int maxLength = 100)
    {
        if (maxLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxLength), "JSON max length must be greater than zero.");

        _output.ValueFormatter = item =>
        {
            string json = JsonSerializer.Serialize((TItem)item);
            return json.Substring(0, Math.Min(maxLength, json.Length));
        };
        return this;
    }

    public ICollectionListConfigurator<TItem> WithDescription(Func<TItem, string> format)
    {
        _output.DescriptionFormatter = CreateFormatter(format);
        return this;
    }

    public ICollectionListConfigurator<TItem> WithCategory(Func<TItem, string> format)
    {
        _output.CategoryFormatter = CreateFormatter(format);
        return this;
    }

    public ICollectionListConfigurator<TItem> WithStatus(StatusCode status, Func<TItem, bool> condition)
    {
        return AddStatus(status, condition, _ => status.ToString());
    }

    public ICollectionListConfigurator<TItem> WithStatus(StatusCode status, Func<TItem, bool> condition, string text)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));

        return AddStatus(status, condition, _ => text);
    }

    public ICollectionListConfigurator<TItem> WithStatus(StatusCode status, Func<TItem, bool> condition, Func<TItem, string> text)
    {
        return AddStatus(status, condition, text);
    }

    private static Func<object, string> CreateFormatter(Func<TItem, string> format)
    {
        if (format == null)
            throw new ArgumentNullException(nameof(format));

        return item => format((TItem)item);
    }

    private ICollectionListConfigurator<TItem> AddStatus(StatusCode status, Func<TItem, bool> condition, Func<TItem, string> text)
    {
        if (!Enum.IsDefined(typeof(StatusCode), status))
            throw new ArgumentOutOfRangeException(nameof(status));
        if (condition == null)
            throw new ArgumentNullException(nameof(condition));
        if (text == null)
            throw new ArgumentNullException(nameof(text));

        _output.ItemStatuses.Add(new PropertyStatusConfiguration(status, item => condition((TItem)item), item => text((TItem)item)));
        return this;
    }
}

internal sealed class CollectionExpandedItemConfigurator<TItem> : ICollectionExpandedItemConfigurator<TItem>
{
    private readonly CollectionOutputConfiguration _output;

    public CollectionExpandedItemConfigurator(CollectionOutputConfiguration output)
    {
        _output = output;
    }

    public ICollectionExpandedItemConfigurator<TItem> WithName(Func<TItem, string> format)
    {
        if (format == null)
            throw new ArgumentNullException(nameof(format));

        _output.CategoryFormatter = item => format((TItem)item);
        return this;
    }

    public ICollectionExpandedItemConfigurator<TItem> WithInitiallyExpanded()
    {
        _output.InitiallyExpanded = true;
        return this;
    }

    public ICollectionExpandedItemConfigurator<TItem> WithPrimaryPropertiesOnly()
    {
        _output.PrimaryPropertiesOnly = true;
        return this;
    }

    public ICollectionExpandedItemConfigurator<TItem> WithStatus(StatusCode status, Func<TItem, bool> condition)
    {
        return AddStatus(status, condition, _ => status.ToString());
    }

    public ICollectionExpandedItemConfigurator<TItem> WithStatus(StatusCode status, Func<TItem, bool> condition, string text)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));

        return AddStatus(status, condition, _ => text);
    }

    public ICollectionExpandedItemConfigurator<TItem> WithStatus(StatusCode status, Func<TItem, bool> condition, Func<TItem, string> text)
    {
        return AddStatus(status, condition, text);
    }

    public ICollectionExpandedItemConfigurator<TItem> WithIconSize(StatusIconSize size)
    {
        if (!Enum.IsDefined(typeof(StatusIconSize), size))
            throw new ArgumentOutOfRangeException(nameof(size));

        _output.ItemStatusIconSize = new ConfiguredValue<StatusIconSize>(size);
        return this;
    }

    public ICollectionExpandedItemConfigurator<TItem> WithDrillDown(bool enabled = true, int? maxItems = null)
    {
        if (maxItems <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxItems), "Drilldown max items must be greater than zero.");

        _output.DrillDown = new ConfiguredValue<bool>(enabled);
        if (maxItems.HasValue)
            _output.DrillDownMaxItems = new ConfiguredValue<int>(maxItems.Value);
        return this;
    }

    private ICollectionExpandedItemConfigurator<TItem> AddStatus(StatusCode status, Func<TItem, bool> condition, Func<TItem, string> text)
    {
        if (!Enum.IsDefined(typeof(StatusCode), status))
            throw new ArgumentOutOfRangeException(nameof(status));
        if (condition == null)
            throw new ArgumentNullException(nameof(condition));
        if (text == null)
            throw new ArgumentNullException(nameof(text));

        _output.ItemStatuses.Add(new PropertyStatusConfiguration(status, item => condition((TItem)item), item => text((TItem)item)));
        return this;
    }
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

    public static bool TryGetDirectField(LambdaExpression expression, Type expectedDeclaringType, out FieldInfo field)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));

        Expression body = expression.Body;
        while (body is UnaryExpression unary && (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
            body = unary.Operand;

        if (body is not MemberExpression { Member: FieldInfo candidate } member || member.Expression != expression.Parameters[0])
        {
            field = null;
            return false;
        }

        if (!candidate.DeclaringType.IsAssignableFrom(expectedDeclaringType))
            throw new ArgumentException($"Field '{candidate.Name}' is not declared on '{expectedDeclaringType.Name}'.", nameof(expression));

        field = candidate;
        return true;
    }
}
