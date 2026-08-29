using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DiagnosticExplorer.Logging;
using DiagnosticExplorer.Util;

namespace DiagnosticExplorer;

internal enum DiagnosticRenderMode
{
    Normal,
    DrillDown,
}

public static class DiagnosticManager
{
    private static readonly StringComparer _ignoreCase = StringComparer.CurrentCultureIgnoreCase;
    private static List<RegisteredObject> RegisteredObjects { get; set; }

    private static Dictionary<string, List<PropertyGetter>> _typeHash = new();
    private static readonly object _propertyGetterLock = new();
    private static readonly object _deferredConfigurationLock = new();
    private static DiagnosticConfigurationSnapshot _configuration = DiagnosticConfigurationSnapshot.Empty;
    private static Lazy<DiagnosticConfiguration> _deferredConfiguration;
    private static Exception _deferredConfigurationException;
    private static readonly AsyncLocal<DiagnosticRenderMode?> _renderMode = new();

    private static readonly Dictionary<Type, OperationSet> _operationLookup = new();
    public const string EnabledConfigurationKey = "DiagnosticExplorer:Enabled";
    public static bool Enabled { get; set; } = true;
    public static DiagnosticConfiguration CurrentConfiguration { get; private set; } = new();
    public static LogEventStore LogEventStore { get; } = new();
    internal static int DrillDownMaxItems => _configuration.DrillDownMaxItems;

    static DiagnosticManager()
    {
        RegisteredObjects = new List<RegisteredObject>();
    }

    internal static void Clear()
    {
        _operationLookup.Clear();
        lock (_propertyGetterLock)
            _typeHash.Clear();
        RegisteredObjects.Clear();
    }

    public static DiagnosticConfiguration Configure(Action<IDiagConfigurator> configure)
    {
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        DiagnosticConfiguration configuration = new();
        configure(configuration);
        UseConfiguration(configuration);
        return configuration;
    }

    public static void UseConfiguration(DiagnosticConfiguration configuration)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        lock (_deferredConfigurationLock)
        {
            _deferredConfiguration = null;
            _deferredConfigurationException = null;
        }

        Enabled = configuration.RuntimeOptions.Enabled;
        EventSinkRepo.Default.ConfigureEventRetention(configuration.RuntimeOptions.EventRetention);
        LogEventStore.Configure(configuration.RuntimeOptions.LogEventRetention, configuration.RuntimeOptions.Routing.CreateSnapshot());
        DiagnosticConfigurationSnapshot snapshot = configuration.CreateSnapshot();
        lock (_propertyGetterLock)
        {
            CurrentConfiguration = configuration;
            _configuration = snapshot;
            _typeHash.Clear();
        }
    }

    public static void ConfigureOnDemand(Action<IDiagConfigurator> configure)
    {
        ConfigureOnDemand(new DiagnosticConfiguration(), configure);
    }

    public static void ConfigureOnDemand(DiagnosticConfiguration configuration, Action<IDiagConfigurator> configure)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        lock (_deferredConfigurationLock)
        {
            if (_deferredConfiguration != null)
                throw new InvalidOperationException("Diagnostic configuration has already been deferred.");

            _deferredConfigurationException = null;
            _deferredConfiguration = new Lazy<DiagnosticConfiguration>(
                () =>
                {
                    configure(configuration);
                    UseConfiguration(configuration);
                    return configuration;
                },
                LazyThreadSafetyMode.ExecutionAndPublication
            );
        }
    }

    private static void EnsureConfiguration()
    {
        Lazy<DiagnosticConfiguration> deferredConfiguration;
        lock (_deferredConfigurationLock)
            deferredConfiguration = _deferredConfiguration;

        if (deferredConfiguration == null)
            return;

        try
        {
            _ = deferredConfiguration.Value;
        }
        catch (Exception exception)
        {
            bool reportFailure;
            lock (_deferredConfigurationLock)
            {
                reportFailure = _deferredConfigurationException == null;
                _deferredConfigurationException ??= exception;
            }

            Enabled = false;
            lock (_propertyGetterLock)
            {
                CurrentConfiguration = new DiagnosticConfiguration();
                _configuration = DiagnosticConfigurationSnapshot.Empty;
                _typeHash.Clear();
            }

            if (reportFailure)
                Trace.TraceError($"Diagnostic Explorer configuration failed and has been disabled: {exception}");
        }
    }

    public static void Register(object o, string bagName, string bagCategory)
    {
        if (!Enabled)
            return;

        lock (RegisteredObjects)
        {
            RegisteredObject existing = RegisteredObjects.Find(ro => ReferenceEquals(ro.Object, o));

            bagName = MakeNameUnique(existing, bagName, bagCategory);
            if (existing == null)
            {
                RegisteredObjects.Add(new RegisteredObject(o, bagCategory, bagName));
            }
            else
            {
                existing.BagName = bagName;
                existing.BagCategory = bagCategory;
            }
        }
    }

    private static string MakeNameUnique(RegisteredObject obj, string name, string category)
    {
        if (name == null)
            return name;

        if (!NameAlreadyTaken(name, category))
            return name;

        for (int i = 2; ; i++)
        {
            string extension = $" {i}";
            string newName = $"{name}{extension}";
            if (!NameAlreadyTaken(newName, category))
                return newName;
        }

        bool NameAlreadyTaken(string proposedName, string proposedCat)
        {
            return RegisteredObjects.Any(ro =>
                !ReferenceEquals(ro, obj) && _ignoreCase.Equals(proposedName, ro.BagName) && _ignoreCase.Equals(proposedCat, ro.BagCategory)
            );
        }
    }

    public static void Unregister(object obj)
    {
        lock (RegisteredObjects)
        {
            RegisteredObject existing = RegisteredObjects.Find(ro => ReferenceEquals(ro.Object, obj));

            if (existing != null)
                RegisteredObjects.Remove(existing);
        }
    }

    public static DiagnosticResponse GetDiagnostics()
    {
        return GetDiagnostics((IServiceProvider)null);
    }

    public static DiagnosticResponse GetDiagnostics(IServiceProvider serviceProvider)
    {
        return GetDiagnostics(GetRegisteredObjects(serviceProvider));
    }

    public static DiagnosticResponse GetDiagnostics(IEnumerable<RegisteredObject> registeredObjects)
    {
        return GetDiagnostics(registeredObjects, DiagnosticRenderMode.Normal);
    }

    private static DiagnosticResponse GetDiagnostics(IEnumerable<RegisteredObject> registeredObjects, DiagnosticRenderMode renderMode)
    {
        try
        {
            DiagnosticResponse response = new();

            response.PropertyBags.AddRange(registeredObjects.Select(x => ObjectToPropertyBag(x.Object, x.BagName, x.BagCategory, renderMode)));

            AddOperationSets(response);
            return response;
        }
        catch (Exception ex)
        {
            return new DiagnosticResponse { ExceptionMessage = ex.Message, ExceptionDetail = ex.ToString() };
        }
    }

    private static void AddOperationSets(DiagnosticResponse response)
    {
        HashSet<OperationSet> operationSets = new();

        foreach (PropertyBag bag in response.PropertyBags)
        {
            OperationSet bagOperations = GetOperationSet(bag.SourceObject);
            if (bagOperations != null)
            {
                bag.OperationSet = bagOperations.Id;
                operationSets.Add(bagOperations);
            }

            foreach (Category cat in bag.Categories)
            {
                OperationSet catOperations = GetOperationSet(cat.ValueObject);
                if (catOperations != null)
                {
                    cat.OperationSet = catOperations.Id;
                    operationSets.Add(catOperations);
                }
            }

            foreach (Property prop in bag.Categories.SelectMany(x => x.Properties))
            {
                OperationSet propOperations = GetOperationSet(prop.ValueObject);
                if (propOperations != null)
                {
                    prop.OperationSet = propOperations.Id;
                    operationSets.Add(propOperations);
                }
            }
        }
        response.OperationSets.AddRange(operationSets);
    }

    private static OperationSet GetOperationSet(object sourceObject)
    {
        if (sourceObject == null)
            return null;

        Type propType = sourceObject.GetType();

        if (_operationLookup.TryGetValue(propType, out OperationSet existing))
            return existing;

        lock (_operationLookup)
        {
            if (!_operationLookup.ContainsKey(propType))
            {
                OperationSet operationSet = CreateOperationSet(propType);
                _operationLookup[propType] = operationSet;
                if (operationSet != null)
                    operationSet.Id = _operationLookup.Values.Count(x => x != null).ToString();
            }
            return _operationLookup[propType];
        }
    }

    private static OperationSet CreateOperationSet(Type propType)
    {
        if (propType == null)
            throw new ArgumentNullException(nameof(propType));

        if (propType.FullName == null)
            return null;
        if (propType.FullName.StartsWith("System"))
            return null;

        OperationSet operationSet = new();

        foreach (MethodInfo method in propType.GetMethods(PublicMethods).OrderBy(x => x.Name))
        {
            if (IsMethodValidOperationTarget(method))
                operationSet.Operations.Add(new Operation(method));
        }

        return operationSet.Operations.Count == 0 ? null : operationSet;
    }

    /// <summary>
    /// To be a valid operation target, a method must contain no ref/out parameters,
    /// no generic parameters apart from Nullable, and the must be allowed either by the DiagnosticClassAttribute
    /// or DiagnosticMethodAttribute
    /// </summary>
    private static bool IsMethodValidOperationTarget(MethodInfo method)
    {
        if (method.IsSpecialName)
            return false;
        if (method.GetParameters().Any(x => x.IsOut))
            return false;
        if (method.GetParameters().Any(x => x.ParameterType.IsByRef))
            return false;

        return AttributeUtil.GetAttribute<DiagnosticMethodAttribute>(method) != null;
    }

    public static RegisteredObject[] GetRegisteredObjects(IServiceProvider serviceProvider = null)
    {
        EnsureConfiguration();
        List<RegisteredObject> list = new();

        lock (RegisteredObjects)
        {
            for (int i = RegisteredObjects.Count - 1; i >= 0; i--)
            {
                RegisteredObject obj = RegisteredObjects[i];
                if (obj.Object == null)
                    RegisteredObjects.RemoveAt(i);
                else
                    list.Add(obj);
            }
        }

        foreach (RegisteredObject discovered in _configuration.FindRegisteredObjects(serviceProvider))
        {
            object discoveredObject = discovered?.Object;
            if (discoveredObject != null && !list.Any(existing => ReferenceEquals(existing.Object, discoveredObject)))
                list.Add(discovered);
        }

        return list.ToArray();
    }

    public static PropertyBag ObjectToPropertyBag(object obj, string bagName, string bagCategory)
    {
        EnsureConfiguration();
        return ObjectToPropertyBag(obj, bagName, bagCategory, DiagnosticRenderMode.Normal);
    }

    private static PropertyBag ObjectToPropertyBag(object obj, string bagName, string bagCategory, DiagnosticRenderMode renderMode)
    {
        DiagnosticRenderMode? previousMode = _renderMode.Value;
        _renderMode.Value = renderMode;
        try
        {
            PropertyBag bag = new();
            bag.Name = bagName;
            bag.Category = bagCategory;
            bag.SourceObject = obj;
            if (obj is IInlineCustomObject inlineCustomObject)
            {
                inlineCustomObject.AddProperties(bag);
                return bag;
            }

            bag.CanDrillDown =
                renderMode == DiagnosticRenderMode.Normal
                && obj is not Type
                && !IsUserInterfaceElement(obj.GetType())
                && _configuration.HasDrillDownConfiguration(obj.GetType());

            List<PropertyGetter> valueGetters = GetPropertyGetters(obj);

            foreach (PropertyGetter getter in valueGetters)
                getter.GetProperties(obj, bag, null);

            return bag;
        }
        finally
        {
            _renderMode.Value = previousMode;
        }
    }

    public const BindingFlags PublicInstancePropertyFlags = BindingFlags.Public | BindingFlags.GetProperty | BindingFlags.Instance;
    private const BindingFlags PublicStaticPropertyFlags = BindingFlags.Public | BindingFlags.GetProperty | BindingFlags.Static;
    private const BindingFlags PublicMethods = BindingFlags.Public | BindingFlags.InvokeMethod | BindingFlags.Instance;
    private const BindingFlags PublicStaticMethods = BindingFlags.Static | BindingFlags.InvokeMethod | BindingFlags.Public;

    internal static List<PropertyGetter> GetPropertyGetters(object obj)
    {
        if (obj == null)
            return new List<PropertyGetter>();

        Type type = obj.GetType();
        string typeKey = type.AssemblyQualifiedName;
        if (obj is Type)
        {
            type = (Type)obj;
            typeKey = "Static: " + type.AssemblyQualifiedName;
        }
        DiagnosticRenderMode renderMode = _renderMode.Value ?? DiagnosticRenderMode.Normal;
        typeKey = renderMode + ": " + typeKey;

        lock (_propertyGetterLock)
        {
            if (_typeHash.TryGetValue(typeKey, out List<PropertyGetter> cachedProperties))
                return cachedProperties;

            List<PropertyGetter> propertyList = new();
            bool applyAttributes = _configuration.ApplyAttributes;
            TypeConfiguration typeConfiguration =
                obj is Type ? null : _configuration.GetEffectiveTypeConfiguration(type, renderMode == DiagnosticRenderMode.DrillDown);

            bool isStatic = obj is Type;
            bool useUnconfiguredDefaults = !isStatic && !_configuration.HasTypeConfiguration(type);
            bool drillDown = renderMode == DiagnosticRenderMode.DrillDown;
            IEnumerable<PropertyInfo> properties = isStatic
                ? GetStaticProperties(type, applyAttributes)
                : GetInstanceProperties(
                    type,
                    null,
                    typeConfiguration,
                    applyAttributes,
                    useUnconfiguredDefaults,
                    drillDown,
                    _configuration.GetNearestTypeIncludeAll(type),
                    drillDown ? _configuration.GetNearestTypeIncludeAll(type, drillDown: true) : null
                );
            foreach (PropertyInfo info in properties)
            {
                DiagnosticPropertyAttribute propAttr = applyAttributes ? GetAttribute<DiagnosticPropertyAttribute>(info) : null;
                PropertyConfiguration propertyConfiguration = typeConfiguration?.Find(info);
                string defaultFormat = _configuration.GetDefaultFormat(info.PropertyType);
                bool useDefaultPropertyPresentation = propAttr == null && propertyConfiguration == null;
                AddPropertyGetters(
                    propertyList,
                    info,
                    propAttr,
                    propertyConfiguration,
                    isStatic,
                    applyAttributes,
                    defaultFormat,
                    useDefaultPropertyPresentation
                );
            }
            if (typeConfiguration != null)
            {
                foreach (PropertyConfiguration delegateProperty in typeConfiguration.DelegateProperties)
                {
                    string defaultFormat = _configuration.GetDefaultFormat(delegateProperty.ValueType);
                    AddPropertyGetters(propertyList, null, null, delegateProperty, false, false, defaultFormat);
                }
                foreach (CustomPropertyConfiguration customProperty in typeConfiguration.CustomProperties)
                    propertyList.Add(new CustomPropertyGetter(customProperty));
            }
            _typeHash[typeKey] = propertyList;
            return propertyList;
        }
    }

    internal static void AddPropertyGetters(
        ICollection<PropertyGetter> getters,
        PropertyInfo info,
        DiagnosticPropertyAttribute metadata,
        PropertyConfiguration configuration,
        bool isStatic,
        bool applyAttributes,
        string defaultFormat,
        bool useDefaultPropertyPresentation = false
    )
    {
        PropertyStrategy strategy = GetPropertyStrategy(info, metadata, configuration, useDefaultPropertyPresentation);
        switch (strategy)
        {
            case PropertyStrategy.Collection:
                AddCollectionGetters(getters, info, metadata, configuration, isStatic, applyAttributes, defaultFormat);
                break;
            case PropertyStrategy.Extended:
                getters.Add(
                    new ExtendedPropertyGetter(
                        info,
                        new ExtendedPropertyAttribute(),
                        metadata,
                        configuration,
                        isStatic,
                        applyAttributes,
                        defaultFormat
                    )
                );
                break;
            case PropertyStrategy.Rate:
                getters.Add(
                    new RateGetter(
                        info,
                        CreateRateOptions(metadata as RatePropertyAttribute, configuration),
                        metadata,
                        configuration,
                        isStatic,
                        applyAttributes,
                        defaultFormat
                    )
                );
                break;
            case PropertyStrategy.Date:
                getters.Add(
                    new DateGetter(
                        info,
                        CreateDateOptions(metadata as DatePropertyAttribute, configuration),
                        metadata,
                        configuration,
                        isStatic,
                        applyAttributes,
                        defaultFormat
                    )
                );
                break;
            default:
                getters.Add(
                    new PropertyGetter(
                        info,
                        metadata,
                        configuration,
                        isStatic,
                        applyAttributes,
                        defaultFormat,
                        useDefaultPropertyPresentation && IsDefaultObjectType(info.PropertyType) && !HasUsefulToString(info.PropertyType)
                    )
                );
                break;
        }
    }

    private static PropertyStrategy GetPropertyStrategy(
        PropertyInfo info,
        DiagnosticPropertyAttribute attribute,
        PropertyConfiguration configuration,
        bool useDefaultPropertyPresentation
    )
    {
        if (configuration?.Strategy != null)
            return configuration.Strategy.Value;
        if (attribute is CollectionPropertyAttribute)
            return PropertyStrategy.Collection;
        if (attribute is ExtendedPropertyAttribute)
            return PropertyStrategy.Extended;
        Type propertyType = info?.PropertyType ?? configuration?.ValueType;
        if (attribute is RatePropertyAttribute || propertyType == typeof(RateCounter))
            return PropertyStrategy.Rate;

        Type underlying = GetUnderlyingType(propertyType);
        if (attribute is DatePropertyAttribute || underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset))
            return PropertyStrategy.Date;
        if (configuration?.UsesPropertyDefaults == true && UsesDefaultCollectionPresentation(underlying, configuration))
            return PropertyStrategy.Collection;
        if (useDefaultPropertyPresentation && IsDefaultCollectionType(underlying))
            return PropertyStrategy.Collection;
        return PropertyStrategy.Default;
    }

    private static bool UsesDefaultCollectionPresentation(Type type, PropertyConfiguration configuration)
    {
        return configuration.ValueFormatter == null && !configuration.FormatString.IsSet && IsConfiguredCollectionType(type);
    }

    private static bool IsConfiguredCollectionType(Type type)
    {
        Type underlyingType = GetUnderlyingType(type);
        if (underlyingType == typeof(string))
            return false;
        if (underlyingType.IsArray)
            return true;

        return ImplementsGenericInterface(underlyingType, typeof(ICollection<>))
            || ImplementsGenericInterface(underlyingType, typeof(IList<>))
            || ImplementsGenericInterface(underlyingType, typeof(IReadOnlyCollection<>))
            || ImplementsGenericInterface(underlyingType, typeof(IReadOnlyList<>))
            || ImplementsGenericInterface(underlyingType, typeof(ISet<>));
    }

    private static bool ImplementsGenericInterface(Type type, Type genericInterface)
    {
        return (type.IsGenericType && type.GetGenericTypeDefinition() == genericInterface)
            || type.GetInterfaces().Any(candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == genericInterface);
    }

    private static void AddCollectionGetters(
        ICollection<PropertyGetter> getters,
        PropertyInfo info,
        DiagnosticPropertyAttribute metadata,
        PropertyConfiguration configuration,
        bool isStatic,
        bool applyAttributes,
        string defaultFormat
    )
    {
        CollectionOptions source = (metadata as CollectionPropertyAttribute)?.CreateOptions();
        IReadOnlyList<CollectionOutputConfiguration> outputs = configuration?.CollectionOutputs;
        if (outputs == null || outputs.Count == 0)
        {
            CollectionOptions options = CloneCollectionOptions(source);
            ApplyCollectionConfiguration(options, configuration);
            getters.Add(new CollectionGetter(info, options, metadata, configuration, isStatic, applyAttributes, defaultFormat));
            return;
        }

        foreach (CollectionOutputConfiguration output in outputs)
        {
            CollectionOptions options = CloneCollectionOptions(source);
            options.Mode = output.Mode;
            options.NameProperty = output.NameProperty ?? options.NameProperty;
            options.NameFormatter = output.NameFormatter ?? options.NameFormatter;
            options.ValueProperty = output.ValueProperty ?? options.ValueProperty;
            options.ValueFormatter = output.ValueFormatter ?? options.ValueFormatter;
            options.DescriptionProperty = output.DescriptionProperty ?? options.DescriptionProperty;
            options.DescriptionFormatter = output.DescriptionFormatter ?? options.DescriptionFormatter;
            options.CategoryProperty = output.CategoryProperty ?? options.CategoryProperty;
            options.CategoryFormatter = output.CategoryFormatter ?? options.CategoryFormatter;
            options.Separator = output.Separator ?? options.Separator;
            options.InitiallyExpanded = output.InitiallyExpanded;
            options.PrimaryPropertiesOnly = output.PrimaryPropertiesOnly;
            ApplyCollectionConfiguration(options, configuration);

            PropertyConfiguration outputConfiguration = configuration.Clone();
            ApplyCollectionOutputConfiguration(outputConfiguration, output);
            string outputName = output.Name;
            if (outputName == null && outputs.Count > 1 && output.Mode == CollectionMode.Count)
            {
                string baseName = configuration.Name.IsSet ? configuration.Name.Value : metadata?.Name ?? info?.Name;
                outputName = baseName + " count";
            }
            if (outputName != null)
            {
                outputConfiguration = configuration.Clone();
                outputConfiguration.Name = new ConfiguredValue<string>(outputName);
            }

            getters.Add(new CollectionGetter(info, options, metadata, outputConfiguration, isStatic, applyAttributes, defaultFormat));
        }
    }

    private static CollectionOptions CloneCollectionOptions(CollectionOptions source)
    {
        if (source == null)
            return new CollectionOptions(CollectionMode.Count);

        return new CollectionOptions(source.Mode)
        {
            NameProperty = source.NameProperty,
            NameFormatter = source.NameFormatter,
            ValueProperty = source.ValueProperty,
            ValueFormatter = source.ValueFormatter,
            DescriptionProperty = source.DescriptionProperty,
            DescriptionFormatter = source.DescriptionFormatter,
            CategoryProperty = source.CategoryProperty,
            CategoryFormatter = source.CategoryFormatter,
            Separator = source.Separator,
            MaxItems = source.MaxItems,
            InitiallyExpanded = source.InitiallyExpanded,
            PrimaryPropertiesOnly = source.PrimaryPropertiesOnly,
        };
    }

    private static void ApplyCollectionConfiguration(CollectionOptions options, PropertyConfiguration configuration)
    {
        if (configuration != null && configuration.MaxItems.IsSet)
            options.MaxItems = configuration.MaxItems.Value;
    }

    private static void ApplyCollectionOutputConfiguration(PropertyConfiguration configuration, CollectionOutputConfiguration output)
    {
        configuration.NoTruncate = output.NoTruncate.Or(configuration.NoTruncate);
        configuration.DrillDown = output.DrillDown.Or(configuration.DrillDown);
        configuration.DrillDownMaxItems = output.DrillDownMaxItems.Or(configuration.DrillDownMaxItems);
        configuration.DrillDownIconOnly = output.DrillDownIconOnly.Or(configuration.DrillDownIconOnly);
        configuration.DrillDownText = output.DrillDownText.Or(configuration.DrillDownText);
        configuration.JsonHover = output.JsonHover.Or(configuration.JsonHover);
        configuration.ExpandedHover = output.ExpandedHover.Or(configuration.ExpandedHover);
    }

    private static RatePropertyAttribute CreateRateOptions(RatePropertyAttribute source, PropertyConfiguration configuration)
    {
        if (source == null && configuration?.Strategy != PropertyStrategy.Rate)
            return null;

        RatePropertyAttribute options =
            source == null
                ? new RatePropertyAttribute()
                : new RatePropertyAttribute { ExposeRate = source.ExposeRate, ExposeTotal = source.ExposeTotal };

        if (configuration != null)
        {
            if (configuration.ExposeRate.IsSet)
                options.ExposeRate = configuration.ExposeRate.Value;
            if (configuration.ExposeTotal.IsSet)
                options.ExposeTotal = configuration.ExposeTotal.Value;
        }
        return options;
    }

    private static DatePropertyAttribute CreateDateOptions(DatePropertyAttribute source, PropertyConfiguration configuration)
    {
        DatePropertyAttribute options =
            source == null
                ? new DatePropertyAttribute()
                : new DatePropertyAttribute
                {
                    ExposeDate = source.ExposeDate,
                    ExposeElapsed = source.ExposeElapsed,
                    ExposeTimeUntil = source.ExposeTimeUntil,
                    IsUTC = source.IsUTC,
                };

        if (configuration != null)
        {
            if (configuration.ExposeDate.IsSet)
                options.ExposeDate = configuration.ExposeDate.Value;
            if (configuration.ExposeElapsed.IsSet)
                options.ExposeElapsed = configuration.ExposeElapsed.Value;
            if (configuration.ExposeTimeUntil.IsSet)
                options.ExposeTimeUntil = configuration.ExposeTimeUntil.Value;
        }
        return options;
    }

    private static IEnumerable<PropertyInfo> GetInstanceProperties(
        Type type,
        DiagnosticClassAttribute inheritedAttr,
        TypeConfiguration configuration,
        bool applyAttributes,
        bool useUnconfiguredDefaults,
        bool drillDown,
        bool? normalIncludeAll,
        bool? drillDownIncludeAll
    )
    {
        if (type != typeof(object))
        {
            DiagnosticClassAttribute diagAttr = applyAttributes ? GetAttribute<DiagnosticClassAttribute>(type, false) : null;
            bool frameworkUserInterfaceType = IsFrameworkUserInterfaceElement(type);
            bool? declaredNormalIncludeAll = _configuration.GetDeclaredTypeIncludeAll(type);
            if (declaredNormalIncludeAll.HasValue)
                normalIncludeAll = declaredNormalIncludeAll;
            if (drillDown)
            {
                bool? declaredDrillDownIncludeAll = _configuration.GetDeclaredTypeIncludeAll(type, drillDown: true);
                if (declaredDrillDownIncludeAll.HasValue)
                    drillDownIncludeAll = declaredDrillDownIncludeAll;
            }
            bool? includeAll = drillDownIncludeAll ?? normalIncludeAll;

            if (inheritedAttr == null || !inheritedAttr.DeclaringTypeOnly || diagAttr != null)
            {
                foreach (PropertyInfo propInfo in type.GetProperties(PublicInstancePropertyFlags | BindingFlags.DeclaredOnly))
                    if (
                        ShouldIncludeProperty(
                            diagAttr ?? inheritedAttr,
                            propInfo,
                            configuration,
                            applyAttributes,
                            useUnconfiguredDefaults,
                            frameworkUserInterfaceType,
                            includeAll
                        )
                    )
                        yield return propInfo;
            }

            if (frameworkUserInterfaceType && !IsUserInterfaceElement(type.BaseType))
                yield break;

            foreach (
                PropertyInfo propInfo in GetInstanceProperties(
                    type.BaseType,
                    diagAttr ?? inheritedAttr,
                    configuration,
                    applyAttributes,
                    useUnconfiguredDefaults,
                    drillDown,
                    normalIncludeAll,
                    drillDownIncludeAll
                )
            )
                yield return propInfo;
        }
    }

    private static IEnumerable<PropertyInfo> GetStaticProperties(Type type, bool applyAttributes)
    {
        DiagnosticClassAttribute diagAttr = applyAttributes ? GetAttribute<DiagnosticClassAttribute>(type, false) : null;

        return type.GetProperties(PublicStaticPropertyFlags)
            .Where(propInfo => ShouldIncludeProperty(diagAttr, propInfo, applyAttributes: applyAttributes));
    }

    private static bool ShouldIncludeProperty(
        DiagnosticClassAttribute diagAttr,
        PropertyInfo info,
        TypeConfiguration configuration = null,
        bool applyAttributes = true,
        bool useUnconfiguredDefaults = false,
        bool explicitlyConfiguredOnly = false,
        bool? includeAll = null
    )
    {
        if (info.PropertyType == typeof(EventSink))
            return false;

        bool attributedOnly = applyAttributes && diagAttr is { AttributedPropertiesOnly: true };
        DiagnosticPropertyAttribute propAttr = applyAttributes ? GetAttribute<DiagnosticPropertyAttribute>(info) : null;
        PropertyConfiguration propertyConfiguration = configuration?.Find(info);

        if (propertyConfiguration?.Included != null)
            return propertyConfiguration.Included.Value;

        if (propAttr != null)
            return !propAttr.Ignore;

        if (explicitlyConfiguredOnly)
            return false;

        if (includeAll.HasValue)
            return includeAll.Value;

        if (attributedOnly)
            return false;

        if (useUnconfiguredDefaults)
            return !IsExcludedDefaultDiagnosticPropertyType(info.PropertyType)
                && (
                    IsDefaultDiagnosticPropertyType(info.PropertyType)
                    || IsDefaultCollectionType(info.PropertyType)
                    || IsDefaultObjectType(info.PropertyType)
                );

        return IsDefaultDiagnosticPropertyType(info.PropertyType);
    }

    private static bool IsDefaultDiagnosticPropertyType(Type type)
    {
        Type underlyingType = GetUnderlyingType(type);
        return underlyingType == typeof(string)
            || underlyingType.IsPrimitive
            || underlyingType.IsEnum
            || underlyingType == typeof(decimal)
            || underlyingType == typeof(DateTime)
            || underlyingType == typeof(DateTimeOffset)
            || underlyingType == typeof(TimeSpan)
            || underlyingType == typeof(Guid);
    }

    private static bool IsDefaultCollectionType(Type type)
    {
        Type underlyingType = GetUnderlyingType(type);
        return underlyingType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(underlyingType);
    }

    private static bool IsDefaultObjectType(Type type)
    {
        Type underlyingType = GetUnderlyingType(type);
        return !IsDefaultDiagnosticPropertyType(underlyingType)
            && !IsDefaultCollectionType(underlyingType)
            && !IsExcludedDefaultDiagnosticPropertyType(underlyingType);
    }

    private static bool HasUsefulToString(Type type)
    {
        Type underlyingType = GetUnderlyingType(type);
        MethodInfo toString = underlyingType.GetMethod(nameof(ToString), Type.EmptyTypes);
        return toString != null && toString.DeclaringType != typeof(object) && toString.DeclaringType != typeof(ValueType);
    }

    private static bool IsUserInterfaceElement(Type type)
    {
        for (Type baseType = type; baseType != null; baseType = baseType.BaseType)
        {
            if (baseType.FullName == "System.Windows.Forms.Control" || baseType.FullName == "System.Windows.FrameworkElement")
                return true;
        }

        return false;
    }

    private static bool IsFrameworkUserInterfaceElement(Type type)
    {
        if (!IsUserInterfaceElement(type))
            return false;

        string typeNamespace = type.Namespace;
        return typeNamespace == "System.Windows.Forms"
            || typeNamespace == "System.Windows"
            || typeNamespace?.StartsWith("System.Windows.", StringComparison.Ordinal) == true;
    }

    private static bool IsExcludedDefaultDiagnosticPropertyType(Type type)
    {
        Type underlyingType = GetUnderlyingType(type);
        return underlyingType.Namespace?.StartsWith("System.Threading.Tasks", StringComparison.Ordinal) == true;
    }

    public static Type GetUnderlyingType(Type t)
    {
        if (t == null)
            throw new ArgumentNullException(nameof(t));

        if (!t.IsGenericType)
            return t;
        if (t.GetGenericTypeDefinition() != typeof(Nullable<>))
            return t;

        return t.GetGenericArguments()[0];
    }

    private static T GetAttribute<T>(PropertyInfo info)
        where T : Attribute
    {
        object[] attrs = info.GetCustomAttributes(typeof(T), false);
        if (attrs.Length == 0)
            return null;

        return attrs[0] as T;
    }

    private static T GetAttribute<T>(Type info, bool inherit)
        where T : Attribute
    {
        object[] attrs = info.GetCustomAttributes(typeof(T), inherit);
        if (attrs.Length == 0)
            return null;

        return attrs[0] as T;
    }

    public static async Task<OperationResponse> ExecuteOperation(string path, string operation, string[] arguments)
    {
        return await ExecuteOperation((IServiceProvider)null, path, operation, arguments);
    }

    public static async Task<OperationResponse> ExecuteOperation(OperationRequest request)
    {
        return await ExecuteOperation((IServiceProvider)null, request);
    }

    public static async Task<OperationResponse> ExecuteOperation(IServiceProvider serviceProvider, OperationRequest request)
    {
        return await ExecuteOperation(GetRegisteredObjects(serviceProvider), request);
    }

    public static async Task<OperationResponse> ExecuteOperation(IEnumerable<RegisteredObject> registeredObjects, OperationRequest request)
    {
        if (request == null)
            return OperationResponse.Error("Operation request not specified");

        try
        {
            IEnumerable<RegisteredObject> actionObjects = ResolveActionObjects(registeredObjects, request.ObjectPaths);
            return await ExecuteOperation(actionObjects, request.Path, request.Operation, request.Arguments);
        }
        catch (Exception ex)
        {
            return OperationResponse.Error(ex.Message, ex.ToString());
        }
    }

    public static async Task<OperationResponse> ExecuteOperation(IServiceProvider serviceProvider, string path, string operation, string[] arguments)
    {
        return await ExecuteOperation(GetRegisteredObjects(serviceProvider), path, operation, arguments);
    }

    public static async Task<OperationResponse> ExecuteOperation(
        IEnumerable<RegisteredObject> registeredObjects,
        string path,
        string operation,
        string[] arguments
    )
    {
        if (path == null)
            return OperationResponse.Error("Object path not specified");

        try
        {
            if (arguments == null)
                arguments = [];

            PropIdent ident = PropIdent.Parse(path);
            object sourceObject = GetSourceObject(registeredObjects, ident);
            OperationSet opSet = GetOperationSet(sourceObject);
            if (opSet == null)
                throw new ArgumentException($"Can't find operations for {ident}");

            Operation op = opSet.Operations.FirstOrDefault(x => x.Signature == operation);
            if (op == null)
                throw new ArgumentException($"Operation '{operation}' not found");

            ParameterInfo[] parameters = op.MethodInfo.GetParameters();

            if (parameters.Length != arguments.Length)
            {
                string msg = $"Operation {operation} expected {parameters.Length} parameters, only found {arguments.Length}";
                throw new ArgumentException(msg);
            }
            object[] paramVals = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                try
                {
                    paramVals[i] = ConvertValue(parameters[i].ParameterType, arguments[i]);
                }
                catch (Exception ex)
                {
                    string msg =
                        $"Parameter {i + 1} ({parameters[i].Name}) can't convert '{arguments[i]}' to {TypeUtil.GetFriendlyTypeName(parameters[i].ParameterType)}";
                    throw new ArgumentException(msg, ex);
                }
            }

            object result = op.MethodInfo.Invoke(sourceObject, paramVals);

            // The operation may be asynchronous - if it returned a Task/Task<T>,
            // await it and unwrap the underlying value before formatting the result.
            result = await UnwrapTaskResult(result);

            string resultString = OperationResultToString(result);
            return OperationResponse.Success(resultString);
        }
        catch (Exception ex)
        {
            return OperationResponse.Error(ex.Message, ex.ToString());
        }
    }

    private static string OperationResultToString(object obj)
    {
        if (obj == null)
            return null;

        if (obj is string)
            return (string)obj;

        IEnumerable asEnumerable = obj as IEnumerable;
        if (asEnumerable == null)
            return Convert.ToString(obj);

        string[] values = asEnumerable.Cast<object>().Select(Convert.ToString).ToArray();
        if (values.Length == 0)
            return "<Empty>";

        return "[" + string.Join(", ", values) + "]";
    }

    /// <summary>
    /// If the invoked operation returned an awaitable result, await it and return the
    /// underlying value so it can be formatted like a synchronous result. Handles
    /// <see cref="Task"/>, <see cref="Task{TResult}"/>, <c>ValueTask</c> and
    /// <c>ValueTask&lt;TResult&gt;</c>. Synchronous results are returned unchanged.
    /// </summary>
    private static async Task<object> UnwrapTaskResult(object result)
    {
        if (result == null)
            return null;

        // ValueTask / ValueTask<T> are structs that expose AsTask(). Convert them to a
        // Task reflectively (so we take no hard compile-time dependency on ValueTask being
        // present in every target framework) and fall through to the Task path below.
        Type resultType = result.GetType();
        bool isValueTask = resultType.IsGenericType
            ? resultType.GetGenericTypeDefinition().FullName == "System.Threading.Tasks.ValueTask`1"
            : resultType.FullName == "System.Threading.Tasks.ValueTask";

        if (isValueTask)
        {
            MethodInfo asTask = resultType.GetMethod("AsTask", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            result = asTask?.Invoke(result, null) ?? result;
        }

        if (result is Task task)
        {
            await task.ConfigureAwait(false);

            // Non-generic Task has no Result; Task<T> does. An async Task is implemented
            // as Task<VoidTaskResult> at runtime, so treat that placeholder as "no value".
            PropertyInfo resultProperty = task.GetType().GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);
            object value = resultProperty?.GetValue(task);
            return value?.GetType().Name == "VoidTaskResult" ? null : value;
        }

        return result;
    }

    /// <summary>
    /// Given an identifer which specifies the path required, this method finds the object which
    /// represents the given PropertyBag/Property Category/Property
    /// </summary>
    /// <param name="registeredObjects">The objects to search within</param>
    /// <param name="ident">Identifies the BagCat/BagName/PropCat/PropName we are searching for</param>
    /// <returns>An object which represents the Bag/PropCat/Prop, or exception if not found</returns>
    private static object GetSourceObject(IEnumerable<RegisteredObject> registeredObjects, PropIdent ident)
    {
        return GetSourceTarget(registeredObjects, ident, DiagnosticRenderMode.Normal, false).Value;
    }

    private static DrillDownTarget GetSourceTarget(
        IEnumerable<RegisteredObject> registeredObjects,
        PropIdent ident,
        DiagnosticRenderMode renderMode,
        bool drillDown
    )
    {
        PropertyBag bag = GetRegisteredObject(registeredObjects, ident, renderMode);

        if (string.IsNullOrEmpty(ident.PropCategory) && string.IsNullOrEmpty(ident.PropName))
        {
            if (bag.SourceObject == null)
            {
                string msg = $"Can't invoke operation. Property bag {ident.BagCategory}|{ident.BagName} doesn't have a value.";
                throw new ArgumentException(msg);
            }
            return new DrillDownTarget(bag.SourceObject, DrillDownMaxItems);
        }

        Category cat = bag.Categories.FindByName(ident.PropCategory);
        if (cat == null)
        {
            string msg = $"Can't find source category {ident.BagCategory}|{ident.BagName}|{ident.PropCategory}";

            throw new ArgumentException(msg);
        }

        if (string.IsNullOrEmpty(ident.PropName))
        {
            if (cat.ValueObject == null)
            {
                string msg = $"Can't invoke operation. Category {ident.BagCategory}|{ident.BagName}|{ident.PropCategory} doesn't have a value.";
                throw new ArgumentException(msg);
            }
            object categoryValue = drillDown ? cat.DrillDownObject : cat.ValueObject;
            if (categoryValue == null)
                throw new ArgumentException($"Category {ident} is not available for drilldown.");
            return new DrillDownTarget(categoryValue, cat.DrillDownMaxItems);
        }

        Property prop = cat.Properties.FindByName(ident.PropName);
        if (prop == null)
        {
            string msg = $"Can't invoke operation. Property {ident.BagCategory}|{ident.BagName}|{ident.PropCategory} not found.";
            throw new ArgumentException(msg);
        }

        object propertyValue = drillDown ? prop.DrillDownObject : prop.ValueObject;
        if (propertyValue == null)
        {
            string msg = drillDown
                ? $"Property {ident} is not available for drilldown."
                : $"Can't invoke operation. Property {ident.BagCategory}|{ident.BagName}|{ident.PropCategory} doesn't have a value.";
            throw new ArgumentException(msg);
        }

        return new DrillDownTarget(propertyValue, prop.DrillDownMaxItems, ident.PropName);
    }

    #region SetProperty

    public static OperationResponse SetProperty(string path, string value)
    {
        return SetProperty((IServiceProvider)null, path, value);
    }

    public static OperationResponse SetProperty(SetPropertyRequest request)
    {
        return SetProperty((IServiceProvider)null, request);
    }

    public static OperationResponse SetProperty(IServiceProvider serviceProvider, SetPropertyRequest request)
    {
        return SetProperty(GetRegisteredObjects(serviceProvider), request);
    }

    public static OperationResponse SetProperty(IEnumerable<RegisteredObject> registeredObjects, SetPropertyRequest request)
    {
        if (request == null)
            return OperationResponse.Error("Set-property request not specified");

        try
        {
            IEnumerable<RegisteredObject> actionObjects = ResolveActionObjects(registeredObjects, request.ObjectPaths);
            return SetProperty(actionObjects, request.Path, request.Value);
        }
        catch (Exception ex)
        {
            return OperationResponse.Error(ex.Message, ex.ToString());
        }
    }

    public static OperationResponse SetProperty(IServiceProvider serviceProvider, string path, string value)
    {
        return SetProperty(GetRegisteredObjects(serviceProvider), path, value);
    }

    public static OperationResponse SetProperty(IEnumerable<RegisteredObject> registeredObjects, string path, string value)
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));

        PropIdent ident = PropIdent.Parse(path);
        PropertyBag bag = GetRegisteredObject(registeredObjects, ident);
        Property prop = bag.GetProperty(ident.PropName, ident.PropCategory);

        if (prop == null)
        {
            string msg = $"Can't find property [{ident.PropCategory}].[{ident.PropName}]";
            throw new ArgumentException(msg);
        }

        if (prop.SourceObject == null)
        {
            string msg = $"Property [{ident.PropCategory}].[{ident.PropName}] doesn't have a source object!";
            return OperationResponse.Error(msg);
        }

        if (prop.SourceProperty == null)
        {
            string msg = $"Property [{ident.PropCategory}].[{ident.PropName}] doesn't have a source PropertyInfo!";
            return OperationResponse.Error(msg);
        }

        if (!prop.CanSet)
        {
            string msg = $"You are not allowed to set [{ident.PropCategory}].[{ident.PropName}], AllowSet is not enabled!";
            return OperationResponse.Error(msg);
        }

        bool isType = prop.SourceObject is Type;
        if (!isType && !prop.SourceProperty.DeclaringType.IsInstanceOfType(prop.SourceObject))
        {
            string msg =
                $"'{ident.PropCategory}'.'{ident.PropName}' property {prop.SourceProperty.Name} expects type {prop.SourceProperty.DeclaringType.Name}, got {prop.SourceObject.GetType().Name}";
            return OperationResponse.Error(msg);
        }

        try
        {
            object newValue = ConvertValue(prop.SourceProperty.PropertyType, value);
            if (isType)
                prop.SourceProperty.SetValue(null, newValue, null);
            else
                prop.SourceProperty.SetValue(prop.SourceObject, newValue, null);

            return OperationResponse.Success();
        }
        catch (Exception ex)
        {
            return OperationResponse.Error(ex.Message, ex.ToString());
        }
    }

    private static PropertyBag GetRegisteredObject(
        IEnumerable<RegisteredObject> registeredObjects,
        PropIdent ident,
        DiagnosticRenderMode renderMode = DiagnosticRenderMode.Normal
    )
    {
        RegisteredObject regObj = registeredObjects.FindByCategoryAndName(ident.BagCategory, ident.BagName);
        if (regObj == null)
            throw new ArgumentException($"Can't find PropertyBag {ident.BagCategory}.{ident.BagName}");

        object obj = regObj.Object;
        if (obj == null)
        {
            string msg = $"PropertyBag {ident.BagCategory}.{ident.BagName} was garbage collected just before I could set the property.  How bizarre!";
            throw new ArgumentException(msg);
        }

        return ObjectToPropertyBag(obj, ident.BagName, ident.BagCategory, renderMode);
    }

    #endregion

    public static DrillDownResponse GetDrillDown(DrillDownRequest request)
    {
        return GetDrillDown(GetRegisteredObjects(), request);
    }

    public static DrillDownResponse GetDrillDown(IServiceProvider serviceProvider, DrillDownRequest request)
    {
        return GetDrillDown(GetRegisteredObjects(serviceProvider), request);
    }

    public static DrillDownResponse GetDrillDown(IEnumerable<RegisteredObject> registeredObjects, DrillDownRequest request)
    {
        try
        {
            DrillDownTarget target = ResolveDrillDownTarget(registeredObjects, request?.ObjectPaths);
            if (IsUserInterfaceElement(target.Value.GetType()))
                return new DrillDownResponse { ErrorMessage = "Windows Forms and WPF user interface elements cannot be shown in a drilldown." };

            if (request?.JsonHover == true)
            {
                return new DrillDownResponse
                {
                    Json = System.Text.Json.JsonSerializer.Serialize(
                        target.Value,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
                    ),
                };
            }

            DrillDownMaterialization materialized = MaterializeDrillDown(target);
            return new DrillDownResponse
            {
                Diagnostics = GetDiagnostics(materialized.RegisteredObjects, DiagnosticRenderMode.DrillDown),
                DisplayedCount = materialized.DisplayedCount,
                TotalCount = materialized.TotalCount,
                IsTruncated = materialized.IsTruncated,
                EventViews =
                    request?.ExcludeEventViews == true
                        ? new List<DrillDownEventViewDefinition>()
                        : ResolveDrillDownEventViews(materialized.RegisteredObjects),
            };
        }
        catch (Exception ex)
        {
            return new DrillDownResponse { ErrorMessage = ex.Message, ErrorDetail = ex.ToString() };
        }
    }

    internal static bool IsDrillDownValue(object value)
    {
        if (value == null || value is string)
            return false;

        Type type = Nullable.GetUnderlyingType(value.GetType()) ?? value.GetType();
        if (type.IsPrimitive || type.IsEnum || type == typeof(decimal) || type == typeof(DateTime) || type == typeof(DateTimeOffset))
            return false;
        if (type == typeof(TimeSpan) || type == typeof(Guid))
            return false;
        if (IsUserInterfaceElement(type))
            return false;

        return true;
    }

    private static List<DrillDownEventViewDefinition> ResolveDrillDownEventViews(IEnumerable<RegisteredObject> registeredObjects)
    {
        Dictionary<string, DrillDownEventViewDefinition> views = new(StringComparer.OrdinalIgnoreCase);
        foreach (RegisteredObject registeredObject in registeredObjects)
        {
            object target = registeredObject?.Object;
            if (target == null)
                continue;

            TypeConfiguration configuration = _configuration.GetEffectiveTypeConfiguration(target.GetType(), drillDown: true);
            foreach (DrillDownEventRouteTemplate route in configuration.EventRoutes)
            {
                string loggerName = route.ResolveLoggerName(target);
                if (string.IsNullOrWhiteSpace(loggerName))
                    continue;

                string id = $"{route.Route.Category}\u001f{route.Route.Name}";
                if (!views.TryGetValue(id, out DrillDownEventViewDefinition view))
                {
                    view = new DrillDownEventViewDefinition
                    {
                        Id = id,
                        Category = route.Route.Category,
                        Name = route.Route.Name,
                    };
                    views.Add(id, view);
                }

                DrillDownEventMatcher matcher = new()
                {
                    LoggerName = loggerName,
                    MatchMode = route.MatchMode,
                    MinLevel = route.Route.MinLevel.HasValue ? (int)route.Route.MinLevel.Value : null,
                    MaxLevel = route.Route.MaxLevel.HasValue ? (int)route.Route.MaxLevel.Value : null,
                };
                if (
                    !view.Matchers.Any(existing =>
                        existing.LoggerName == matcher.LoggerName
                        && existing.MatchMode == matcher.MatchMode
                        && existing.MinLevel == matcher.MinLevel
                        && existing.MaxLevel == matcher.MaxLevel
                    )
                )
                    view.Matchers.Add(matcher);
            }
        }

        return views.Values.OrderBy(view => view.Category).ThenBy(view => view.Name).ToList();
    }

    private static DrillDownTarget ResolveDrillDownTarget(IEnumerable<RegisteredObject> registeredObjects, IReadOnlyList<string> objectPaths)
    {
        if (objectPaths == null || objectPaths.Count == 0)
            throw new ArgumentException("At least one drilldown object path is required.", nameof(objectPaths));

        IEnumerable<RegisteredObject> currentObjects = registeredObjects;
        DrillDownTarget current = null;
        for (int index = 0; index < objectPaths.Count; index++)
        {
            string path = objectPaths[index];
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException($"Drilldown object path {index + 1} is empty.", nameof(objectPaths));

            try
            {
                current = GetSourceTarget(
                    currentObjects,
                    PropIdent.Parse(path),
                    index == 0 ? DiagnosticRenderMode.Normal : DiagnosticRenderMode.DrillDown,
                    true
                );
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Unable to resolve drilldown path {index + 1} '{path}': {ex.Message}", ex);
            }

            if (index + 1 < objectPaths.Count)
                currentObjects = MaterializeDrillDown(current).RegisteredObjects;
        }
        return current;
    }

    private static IEnumerable<RegisteredObject> ResolveActionObjects(
        IEnumerable<RegisteredObject> registeredObjects,
        IReadOnlyList<string> objectPaths
    )
    {
        if (objectPaths == null || objectPaths.Count == 0)
            return registeredObjects;

        DrillDownTarget target = ResolveDrillDownTarget(registeredObjects, objectPaths);
        return MaterializeDrillDown(target).RegisteredObjects;
    }

    private static DrillDownMaterialization MaterializeDrillDown(DrillDownTarget target)
    {
        IEnumerable enumerable = target.Value as IEnumerable;
        if (enumerable == null || target.Value is string)
        {
            Type type = target.Value.GetType();
            return new DrillDownMaterialization(new[] { new RegisteredObject(target.Value, "DrillDown", type.Name) }, 1, 1, false);
        }

        int maxItems = target.MaxItems > 0 ? target.MaxItems : DrillDownMaxItems;
        List<object> items = enumerable.Cast<object>().Take(maxItems + 1).ToList();
        bool truncated = items.Count > maxItems;
        if (truncated)
            items.RemoveAt(items.Count - 1);

        List<RegisteredObject> registered = new();
        for (int index = 0; index < items.Count; index++)
        {
            object item = items[index];
            if (!IsDrillDownValue(item))
                item = new DrillDownScalarValue(item);
            registered.Add(new RegisteredObject(item, "Items", $"{target.ItemName ?? "Items"}[{index}]"));
        }

        int? totalCount =
            enumerable is ICollection collection ? collection.Count
            : truncated ? null
            : items.Count;
        return new DrillDownMaterialization(registered, items.Count, totalCount, truncated);
    }

    private sealed class DrillDownTarget
    {
        public DrillDownTarget(object value, int maxItems, string itemName = null)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
            MaxItems = maxItems;
            ItemName = itemName;
        }

        public object Value { get; }
        public int MaxItems { get; }
        public string ItemName { get; }
    }

    private sealed class DrillDownMaterialization
    {
        public DrillDownMaterialization(IReadOnlyList<RegisteredObject> registeredObjects, int displayedCount, int? totalCount, bool isTruncated)
        {
            RegisteredObjects = registeredObjects;
            DisplayedCount = displayedCount;
            TotalCount = totalCount;
            IsTruncated = isTruncated;
        }

        public IReadOnlyList<RegisteredObject> RegisteredObjects { get; }
        public int DisplayedCount { get; }
        public int? TotalCount { get; }
        public bool IsTruncated { get; }
    }

    [DiagnosticClass(AttributedPropertiesOnly = true)]
    private sealed class DrillDownScalarValue
    {
        public DrillDownScalarValue(object value) => Value = value;

        [DiagnosticProperty]
        public object Value { get; }
    }

    private class PropIdent
    {
        public string BagCategory { get; private set; }
        public string BagName { get; private set; }
        public string PropCategory { get; private set; }
        public string PropName { get; private set; }

        public static PropIdent Parse(string path)
        {
            string[] elements = path.Split('|');

            PropIdent ident = new();
            ident.BagCategory = NullIfEmpty(elements.ElementAtOrDefault(0));
            ident.BagName = NullIfEmpty(elements.ElementAtOrDefault(1));
            ident.PropCategory = NullIfEmpty(elements.ElementAtOrDefault(2));
            ident.PropName = NullIfEmpty(elements.ElementAtOrDefault(3));
            return ident;
        }

        public override string ToString()
        {
            if (PropName != null)
                return $"{BagCategory}|{BagName}|{PropCategory}|{PropName}";

            if (PropCategory != null)
                return $"{BagCategory}|{BagName}|{PropCategory}";

            return $"{BagCategory}|{BagName}";
        }

        private static string NullIfEmpty(string s)
        {
            return string.IsNullOrEmpty(s) ? null : s;
        }
    }

    private static object ConvertValue(Type type, string value)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            if (string.IsNullOrEmpty(value))
                return null;

            type = type.GetGenericArguments()[0];
        }

        if (type.IsEnum)
            return Enum.Parse(type, value, true);

        try
        {
            return Convert.ChangeType(value, type);
        }
        catch (FormatException)
        {
            throw;
        }
        catch
        {
            object parsed;
            if (TryParseValue(type, value, out parsed))
                return parsed;

            throw;
        }
    }

    private static bool TryParseValue(Type type, string value, out object parsed)
    {
        parsed = null;

        MethodInfo method = type.GetMethod("Parse", PublicStaticMethods, null, new[] { typeof(string) }, null);

        if (method == null)
            return false;

        parsed = method.Invoke(null, new object[] { value });
        return true;
    }
}
