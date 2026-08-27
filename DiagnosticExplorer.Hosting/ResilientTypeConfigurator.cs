using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using DiagnosticExplorer.Logging;

namespace DiagnosticExplorer;

internal sealed class ResilientTypeConfigurator<T> : ITypeConfigurator<T>
{
    private readonly ITypeConfigurator<T> _inner;

    public ResilientTypeConfigurator(ITypeConfigurator<T> inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public ITypeConfigurator<T> ExcludeAll() => Try(() => _inner.ExcludeAll(), this, "ExcludeAll");

    public ITypeConfigurator<T> IncludeAll() => Try(() => _inner.IncludeAll(), this, "IncludeAll");

    public IDisposable CreateCategoryScope(string category) =>
        Try(() => _inner.CreateCategoryScope(category), EmptyScope.Instance, "CreateCategoryScope");

    public ITypeConfigurator<T> Include<TProperty>(Expression<Func<T, TProperty>> property) => Try(() => _inner.Include(property), this, "Include");

    public ITypeConfigurator<T> Exclude<TProperty>(Expression<Func<T, TProperty>> property) => Try(() => _inner.Exclude(property), this, "Exclude");

    public IPropertyConfigurator<T, TProperty> Property<TProperty>(Expression<Func<T, TProperty>> property) =>
        Try(() => _inner.Property(property), new NoOpPropertyConfigurator<TProperty>(), "Property");

    public ICustomPropertyConfigurator<T> Property(string name, Func<T, object> value) =>
        Try(() => _inner.Property(name, value), new NoOpCustomPropertyConfigurator(), "Property");

    public ICustomPropertyConfigurator<T> Custom(string name, Action<ICustomObjectConfigurator<T>> configure) =>
        Try(
            () => _inner.Custom(name, projection => configure(new ResilientCustomObjectConfigurator(projection, this))),
            new NoOpCustomPropertyConfigurator(),
            "Custom"
        );

    public ICollectionConfigurator<T, TItem> Collection<TItem>(Expression<Func<T, IEnumerable<TItem>>> property) =>
        Try(() => _inner.Collection(property), new NoOpCollectionConfigurator<TItem>(), "Collection");

    public ICollectionConfigurator<T, TItem> Collection<TItem>(string name, Func<T, IEnumerable<TItem>> value) =>
        Try(() => _inner.Collection(name, value), new NoOpCollectionConfigurator<TItem>(), "Collection");

    public IRateConfigurator<T> Rate(Expression<Func<T, RateCounter>> property) =>
        Try(() => _inner.Rate(property), new NoOpRateConfigurator(), "Rate");

    public IDateConfigurator<T> Date(Expression<Func<T, DateTime>> property) => Try(() => _inner.Date(property), new NoOpDateConfigurator(), "Date");

    public IDateConfigurator<T> Date(Expression<Func<T, DateTime?>> property) => Try(() => _inner.Date(property), new NoOpDateConfigurator(), "Date");

    public IDateConfigurator<T> Date(Expression<Func<T, DateTimeOffset>> property) =>
        Try(() => _inner.Date(property), new NoOpDateConfigurator(), "Date");

    public IDateConfigurator<T> Date(Expression<Func<T, DateTimeOffset?>> property) =>
        Try(() => _inner.Date(property), new NoOpDateConfigurator(), "Date");

    public IExtendedPropertyConfigurator<T, TProperty> Extended<TProperty>(Expression<Func<T, TProperty>> property) =>
        Try(() => _inner.Extended(property), new NoOpExtendedPropertyConfigurator<TProperty>(), "Extended");

    public IExtendedPropertyConfigurator<T, TProperty> Extended<TProperty>(string name, Func<T, TProperty> value) =>
        Try(() => _inner.Extended(name, value), new NoOpExtendedPropertyConfigurator<TProperty>(), "Extended");

    public ITypeConfigurator<T> Route(string loggerName, LoggerNameMatchMode matchMode, Action<DrillDownEventRoute> configure) =>
        Try(() => _inner.Route(loggerName, matchMode, configure), this, "Route");

    public ITypeConfigurator<T> Route(Expression<Func<T, string>> loggerName, LoggerNameMatchMode matchMode, Action<DrillDownEventRoute> configure) =>
        Try(() => _inner.Route(loggerName, matchMode, configure), this, "Route");

    private TResult Try<TResult>(Func<TResult> configure, TResult fallback, string registration)
    {
        try
        {
            return configure();
        }
        catch (Exception exception)
        {
            Trace.TraceError($"Diagnostic Explorer ignored invalid {registration} configuration for '{typeof(T).FullName}': {exception}");
            return fallback;
        }
    }

    private sealed class EmptyScope : IDisposable
    {
        public static readonly EmptyScope Instance = new();

        public void Dispose() { }
    }

    private sealed class NoOpPropertyConfigurator<TProperty> : IPropertyConfigurator<T, TProperty>
    {
        IPropertyConfigurator IPropertyConfigurator.Named(string name) => this;

        IPropertyConfigurator IPropertyConfigurator.Category(string category) => this;

        IPropertyConfigurator IPropertyConfigurator.Description(string description) => this;

        IPropertyConfigurator IPropertyConfigurator.Format(string formatString) => this;

        IPropertyConfigurator IPropertyConfigurator.AllowSet(bool allowSet) => this;

        IPropertyConfigurator<T, TProperty> IObjectPropertyConfigurator<T, IPropertyConfigurator<T, TProperty>>.Named(string name) => this;

        IPropertyConfigurator<T, TProperty> IObjectPropertyConfigurator<T, IPropertyConfigurator<T, TProperty>>.Named(Func<T, string> name) => this;

        IPropertyConfigurator<T, TProperty> IObjectPropertyConfigurator<T, IPropertyConfigurator<T, TProperty>>.Category(string category) => this;

        IPropertyConfigurator<T, TProperty> IObjectPropertyConfigurator<T, IPropertyConfigurator<T, TProperty>>.Category(Func<T, string> category) =>
            this;

        IPropertyConfigurator<T, TProperty> IObjectPropertyConfigurator<T, IPropertyConfigurator<T, TProperty>>.Description(string description) =>
            this;

        IPropertyConfigurator<T, TProperty> IObjectPropertyConfigurator<T, IPropertyConfigurator<T, TProperty>>.Description(
            Func<T, string> description
        ) => this;

        IPropertyConfigurator<T, TProperty> IObjectPropertyConfigurator<T, IPropertyConfigurator<T, TProperty>>.Format(string formatString) => this;

        IPropertyConfigurator<T, TProperty> IObjectPropertyConfigurator<T, IPropertyConfigurator<T, TProperty>>.AllowSet(bool allowSet) => this;

        public IPropertyConfigurator<T, TProperty> Format(Func<TProperty, string> format) => this;

        public IPropertyConfigurator<T, TProperty> WithDrillDown(bool enabled = true, int? maxItems = null) => this;

        public IPropertyConfigurator<T, TProperty> AsDrillDownIcon(int? maxItems = null) => this;

        public IPropertyConfigurator<T, TProperty> Warn(Func<T, bool> condition, string message) => this;

        public IPropertyConfigurator<T, TProperty> Warn(Func<T, bool> condition, string message, string category) => this;

        public IPropertyConfigurator<T, TProperty> Warn(Func<T, bool> condition, Func<T, string> message) => this;

        public IPropertyConfigurator<T, TProperty> Warn(Func<T, bool> condition, Func<T, string> message, string category) => this;

        public IPropertyConfigurator<T, TProperty> Error(Func<T, bool> condition, string message) => this;

        public IPropertyConfigurator<T, TProperty> Error(Func<T, bool> condition, string message, string category) => this;

        public IPropertyConfigurator<T, TProperty> Error(Func<T, bool> condition, Func<T, string> message) => this;

        public IPropertyConfigurator<T, TProperty> Error(Func<T, bool> condition, Func<T, string> message, string category) => this;
    }

    private sealed class NoOpCustomPropertyConfigurator : ICustomPropertyConfigurator<T>
    {
        public ICustomPropertyConfigurator<T> WithDrillDown(bool enabled = true, int? maxItems = null) => this;

        public ICustomPropertyConfigurator<T> AsDrillDownIcon(int? maxItems = null) => this;

        public ICustomPropertyConfigurator<T> Category(string category) => this;

        public ICustomPropertyConfigurator<T> Category(Func<T, string> category) => this;

        public ICustomPropertyConfigurator<T> Description(string description) => this;

        public ICustomPropertyConfigurator<T> Description(Func<T, string> description) => this;

        public ICustomPropertyConfigurator<T> Warn(Func<T, bool> condition, string message) => this;

        public ICustomPropertyConfigurator<T> Warn(Func<T, bool> condition, string message, string category) => this;

        public ICustomPropertyConfigurator<T> Warn(Func<T, bool> condition, Func<T, string> message) => this;

        public ICustomPropertyConfigurator<T> Warn(Func<T, bool> condition, Func<T, string> message, string category) => this;

        public ICustomPropertyConfigurator<T> Error(Func<T, bool> condition, string message) => this;

        public ICustomPropertyConfigurator<T> Error(Func<T, bool> condition, string message, string category) => this;

        public ICustomPropertyConfigurator<T> Error(Func<T, bool> condition, Func<T, string> message) => this;

        public ICustomPropertyConfigurator<T> Error(Func<T, bool> condition, Func<T, string> message, string category) => this;
    }

    private sealed class ResilientCustomObjectConfigurator : ICustomObjectConfigurator<T>
    {
        private readonly ICustomObjectConfigurator<T> _inner;
        private readonly ResilientTypeConfigurator<T> _owner;

        public ResilientCustomObjectConfigurator(ICustomObjectConfigurator<T> inner, ResilientTypeConfigurator<T> owner)
        {
            _inner = inner;
            _owner = owner;
        }

        public ICustomPropertyConfigurator<T> Property(string name, Func<T, object> value) =>
            _owner.Try(() => _inner.Property(name, value), new NoOpCustomPropertyConfigurator(), "Custom property");

        public IExtendedPropertyConfigurator<T, TProperty> Extended<TProperty>(string name, Func<T, TProperty> value) =>
            _owner.Try(() => _inner.Extended(name, value), new NoOpExtendedPropertyConfigurator<TProperty>(), "Custom extended property");

        public ICollectionConfigurator<T, TItem> Collection<TItem>(string name, Func<T, IEnumerable<TItem>> value) =>
            _owner.Try(() => _inner.Collection(name, value), new NoOpCollectionConfigurator<TItem>(), "Custom collection");

        public IRateConfigurator<T> Rate(string name, Func<T, RateCounter> value) =>
            _owner.Try(() => _inner.Rate(name, value), new NoOpRateConfigurator(), "Custom rate");
    }

    private sealed class NoOpCollectionConfigurator<TItem> : ICollectionConfigurator<T, TItem>
    {
        IPropertyConfigurator IPropertyConfigurator.Named(string name) => this;

        IPropertyConfigurator IPropertyConfigurator.Category(string category) => this;

        IPropertyConfigurator IPropertyConfigurator.Description(string description) => this;

        IPropertyConfigurator IPropertyConfigurator.Format(string formatString) => this;

        IPropertyConfigurator IPropertyConfigurator.AllowSet(bool allowSet) => this;

        ICollectionConfigurator<T, TItem> IObjectPropertyConfigurator<T, ICollectionConfigurator<T, TItem>>.Named(string name) => this;

        ICollectionConfigurator<T, TItem> IObjectPropertyConfigurator<T, ICollectionConfigurator<T, TItem>>.Named(Func<T, string> name) => this;

        ICollectionConfigurator<T, TItem> IObjectPropertyConfigurator<T, ICollectionConfigurator<T, TItem>>.Category(string category) => this;

        ICollectionConfigurator<T, TItem> IObjectPropertyConfigurator<T, ICollectionConfigurator<T, TItem>>.Category(Func<T, string> category) =>
            this;

        ICollectionConfigurator<T, TItem> IObjectPropertyConfigurator<T, ICollectionConfigurator<T, TItem>>.Description(string description) => this;

        ICollectionConfigurator<T, TItem> IObjectPropertyConfigurator<T, ICollectionConfigurator<T, TItem>>.Description(
            Func<T, string> description
        ) => this;

        ICollectionConfigurator<T, TItem> IObjectPropertyConfigurator<T, ICollectionConfigurator<T, TItem>>.Format(string formatString) => this;

        ICollectionConfigurator<T, TItem> IObjectPropertyConfigurator<T, ICollectionConfigurator<T, TItem>>.AllowSet(bool allowSet) => this;

        public ICollectionConfigurator<T, TItem> ShowCount(string name = null) => this;

        public ICollectionConfigurator<T, TItem> Concatenate(string separator = null, string name = null) => this;

        public ICollectionConfigurator<T, TItem> AsList(Action<ICollectionListConfigurator<TItem>> configure = null) => this;

        public ICollectionConfigurator<T, TItem> Categories(Expression<Func<TItem, object>> category, string name = null) => this;

        public ICollectionConfigurator<T, TItem> WithMaxItems(int maxItems) => this;

        public ICollectionConfigurator<T, TItem> WithDrillDown(bool enabled = true, int? maxItems = null) => this;
    }

    private sealed class NoOpRateConfigurator : IRateConfigurator<T>
    {
        IPropertyConfigurator IPropertyConfigurator.Named(string name) => this;

        IPropertyConfigurator IPropertyConfigurator.Category(string category) => this;

        IPropertyConfigurator IPropertyConfigurator.Description(string description) => this;

        IPropertyConfigurator IPropertyConfigurator.Format(string formatString) => this;

        IPropertyConfigurator IPropertyConfigurator.AllowSet(bool allowSet) => this;

        IRateConfigurator<T> IObjectPropertyConfigurator<T, IRateConfigurator<T>>.Named(string name) => this;

        IRateConfigurator<T> IObjectPropertyConfigurator<T, IRateConfigurator<T>>.Named(Func<T, string> name) => this;

        IRateConfigurator<T> IObjectPropertyConfigurator<T, IRateConfigurator<T>>.Category(string category) => this;

        IRateConfigurator<T> IObjectPropertyConfigurator<T, IRateConfigurator<T>>.Category(Func<T, string> category) => this;

        IRateConfigurator<T> IObjectPropertyConfigurator<T, IRateConfigurator<T>>.Description(string description) => this;

        IRateConfigurator<T> IObjectPropertyConfigurator<T, IRateConfigurator<T>>.Description(Func<T, string> description) => this;

        IRateConfigurator<T> IObjectPropertyConfigurator<T, IRateConfigurator<T>>.Format(string formatString) => this;

        IRateConfigurator<T> IObjectPropertyConfigurator<T, IRateConfigurator<T>>.AllowSet(bool allowSet) => this;

        public IRateConfigurator<T> ShowRate(bool expose = true) => this;

        public IRateConfigurator<T> ShowTotal(bool expose = true) => this;
    }

    private sealed class NoOpDateConfigurator : IDateConfigurator<T>
    {
        IPropertyConfigurator IPropertyConfigurator.Named(string name) => this;

        IPropertyConfigurator IPropertyConfigurator.Category(string category) => this;

        IPropertyConfigurator IPropertyConfigurator.Description(string description) => this;

        IPropertyConfigurator IPropertyConfigurator.Format(string formatString) => this;

        IPropertyConfigurator IPropertyConfigurator.AllowSet(bool allowSet) => this;

        IDateConfigurator<T> IObjectPropertyConfigurator<T, IDateConfigurator<T>>.Named(string name) => this;

        IDateConfigurator<T> IObjectPropertyConfigurator<T, IDateConfigurator<T>>.Named(Func<T, string> name) => this;

        IDateConfigurator<T> IObjectPropertyConfigurator<T, IDateConfigurator<T>>.Category(string category) => this;

        IDateConfigurator<T> IObjectPropertyConfigurator<T, IDateConfigurator<T>>.Category(Func<T, string> category) => this;

        IDateConfigurator<T> IObjectPropertyConfigurator<T, IDateConfigurator<T>>.Description(string description) => this;

        IDateConfigurator<T> IObjectPropertyConfigurator<T, IDateConfigurator<T>>.Description(Func<T, string> description) => this;

        IDateConfigurator<T> IObjectPropertyConfigurator<T, IDateConfigurator<T>>.Format(string formatString) => this;

        IDateConfigurator<T> IObjectPropertyConfigurator<T, IDateConfigurator<T>>.AllowSet(bool allowSet) => this;

        public IDateConfigurator<T> ShowDate(bool expose = true) => this;

        public IDateConfigurator<T> ShowElapsed(bool expose = true) => this;

        public IDateConfigurator<T> ShowTimeUntil(bool expose = true) => this;
    }

    private sealed class NoOpExtendedPropertyConfigurator<TProperty> : IExtendedPropertyConfigurator<T, TProperty>
    {
        IPropertyConfigurator IPropertyConfigurator.Named(string name) => this;

        IPropertyConfigurator IPropertyConfigurator.Category(string category) => this;

        IPropertyConfigurator IPropertyConfigurator.Description(string description) => this;

        IPropertyConfigurator IPropertyConfigurator.Format(string formatString) => this;

        IPropertyConfigurator IPropertyConfigurator.AllowSet(bool allowSet) => this;

        IExtendedPropertyConfigurator<T, TProperty> IObjectPropertyConfigurator<T, IExtendedPropertyConfigurator<T, TProperty>>.Named(string name) =>
            this;

        IExtendedPropertyConfigurator<T, TProperty> IObjectPropertyConfigurator<T, IExtendedPropertyConfigurator<T, TProperty>>.Named(
            Func<T, string> name
        ) => this;

        IExtendedPropertyConfigurator<T, TProperty> IObjectPropertyConfigurator<T, IExtendedPropertyConfigurator<T, TProperty>>.Category(
            string category
        ) => this;

        IExtendedPropertyConfigurator<T, TProperty> IObjectPropertyConfigurator<T, IExtendedPropertyConfigurator<T, TProperty>>.Category(
            Func<T, string> category
        ) => this;

        IExtendedPropertyConfigurator<T, TProperty> IObjectPropertyConfigurator<T, IExtendedPropertyConfigurator<T, TProperty>>.Description(
            string description
        ) => this;

        IExtendedPropertyConfigurator<T, TProperty> IObjectPropertyConfigurator<T, IExtendedPropertyConfigurator<T, TProperty>>.Description(
            Func<T, string> description
        ) => this;

        IExtendedPropertyConfigurator<T, TProperty> IObjectPropertyConfigurator<T, IExtendedPropertyConfigurator<T, TProperty>>.Format(
            string formatString
        ) => this;

        IExtendedPropertyConfigurator<T, TProperty> IObjectPropertyConfigurator<T, IExtendedPropertyConfigurator<T, TProperty>>.AllowSet(
            bool allowSet
        ) => this;
    }
}
