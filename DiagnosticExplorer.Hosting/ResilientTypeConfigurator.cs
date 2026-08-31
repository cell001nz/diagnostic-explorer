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

    public ICategoryScope CreateCategoryScope(string category) =>
        new ResilientCategoryScope(this, Try(() => _inner.CreateCategoryScope(category), EmptyScope.Instance, "CreateCategoryScope"));

    public ITypeConfigurator<T> Include<TProperty>(Expression<Func<T, TProperty>> property) => Try(() => _inner.Include(property), this, "Include");

    public ITypeConfigurator<T> Exclude<TProperty>(Expression<Func<T, TProperty>> property) => Try(() => _inner.Exclude(property), this, "Exclude");

    public IPropertyConfigurator<T, TProperty> Property<TProperty>(Expression<Func<T, TProperty>> property) =>
        Try(() => _inner.Property(property), new NoOpPropertyConfigurator<TProperty>(), "Property");

    public IPropertyConfigurator<T, TProperty> Property<TProperty>(string name, Func<T, TProperty> value) =>
        Try(() => _inner.Property(name, value), new NoOpPropertyConfigurator<TProperty>(), "Property");

    public ICustomPropertyConfigurator<T> Custom(string name, Action<ICustomObjectConfigurator<T>> configure) =>
        Try(
            () => _inner.Custom(name, projection => configure(new ResilientCustomObjectConfigurator(projection, this))),
            new NoOpCustomPropertyConfigurator(),
            "Custom"
        );

    public ITypeConfigurator<T> Route(string loggerName, LoggerNameMatchMode matchMode, Action<DrillDownEventRoute> configure) =>
        Try(() => _inner.Route(loggerName, matchMode, configure), this, "Route");

    public ITypeConfigurator<T> Route(Func<T, string> loggerName, LoggerNameMatchMode matchMode, Action<DrillDownEventRoute> configure) =>
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

    private sealed class EmptyScope : ICategoryScope
    {
        public static readonly EmptyScope Instance = new();

        public ICategoryScope Expanded(bool expanded = true) => this;

        public void Dispose() { }
    }

    private sealed class ResilientCategoryScope : ICategoryScope
    {
        private readonly ResilientTypeConfigurator<T> _owner;
        private readonly ICategoryScope _inner;

        public ResilientCategoryScope(ResilientTypeConfigurator<T> owner, ICategoryScope inner)
        {
            _owner = owner;
            _inner = inner;
        }

        public ICategoryScope Expanded(bool expanded = true) =>
            _owner.Try(() => _inner.Expanded(expanded), this, "CreateCategoryScope.Expanded");

        public void Dispose()
        {
            _owner.Try(
                () =>
                {
                    _inner.Dispose();
                    return true;
                },
                false,
                "CreateCategoryScope.Dispose"
            );
        }
    }

    private sealed class NoOpPropertyConfigurator<TProperty> : IPropertyConfigurator<T, TProperty>
    {
        IPropertyConfigurator IPropertyConfigurator.WithLabel(string label) => this;

        IPropertyConfigurator IPropertyConfigurator.WithCategory(string category) => this;

        IPropertyConfigurator IPropertyConfigurator.Description(string description) => this;

        IPropertyConfigurator IPropertyConfigurator.Format(string formatString) => this;

        IPropertyConfigurator IPropertyConfigurator.AllowSet(bool allowSet) => this;

        IPropertyConfigurator<T, TProperty> IObjectPropertyConfigurator<T, IPropertyConfigurator<T, TProperty>>.WithLabel(string label) => this;

        IPropertyConfigurator<T, TProperty> IObjectPropertyConfigurator<T, IPropertyConfigurator<T, TProperty>>.WithLabel(Func<T, string> label) =>
            this;

        IPropertyConfigurator<T, TProperty> IObjectPropertyConfigurator<T, IPropertyConfigurator<T, TProperty>>.WithCategory(string category) => this;

        IPropertyConfigurator<T, TProperty> IObjectPropertyConfigurator<T, IPropertyConfigurator<T, TProperty>>.WithCategory(
            Func<T, string> category
        ) => this;

        IPropertyConfigurator<T, TProperty> IObjectPropertyConfigurator<T, IPropertyConfigurator<T, TProperty>>.Description(string description) =>
            this;

        IPropertyConfigurator<T, TProperty> IObjectPropertyConfigurator<T, IPropertyConfigurator<T, TProperty>>.Description(
            Func<T, string> description
        ) => this;

        IPropertyConfigurator<T, TProperty> IObjectPropertyConfigurator<T, IPropertyConfigurator<T, TProperty>>.Format(string formatString) => this;

        IPropertyConfigurator<T, TProperty> IObjectPropertyConfigurator<T, IPropertyConfigurator<T, TProperty>>.AllowSet(bool allowSet) => this;

        public IPropertyConfigurator<T, TProperty> Format(Func<TProperty, string> format) => this;

        public IPropertyConfigurator<T, TProperty> AsJson(int maxLength = 100) => this;

        public IPropertyConfigurator<T, TProperty> AsDateOnly() => this;

        public IPropertyConfigurator<T, TProperty> WithDrillDown(bool enabled = true, int? maxItems = null) => this;

        public IPropertyConfigurator<T, TProperty> WithDrillDownOnly(int? maxItems = null) => this;

        public IPropertyConfigurator<T, TProperty> WithDrillDownOnly(string text, int? maxItems = null) => this;

        public IPropertyConfigurator<T, TProperty> WithJsonHover(bool enabled = true) => this;

        public IPropertyConfigurator<T, TProperty> WithExpandedHover(bool enabled = true) => this;

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
        public ICustomPropertyConfigurator<T> AsJson(int maxLength = 100) => this;

        public ICustomPropertyConfigurator<T> Expand(bool initiallyExpanded = true) => this;

        public ICustomPropertyConfigurator<T> WithDrillDown(bool enabled = true, int? maxItems = null) => this;

        public ICustomPropertyConfigurator<T> WithDrillDownOnly(int? maxItems = null) => this;

        public ICustomPropertyConfigurator<T> WithDrillDownOnly(string text, int? maxItems = null) => this;

        public ICustomPropertyConfigurator<T> WithJsonHover(bool enabled = true) => this;

        public ICustomPropertyConfigurator<T> WithExpandedHover(bool enabled = true) => this;

        public ICustomPropertyConfigurator<T> WithCategory(string category) => this;

        public ICustomPropertyConfigurator<T> WithCategory(Func<T, string> category) => this;

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

        public IPropertyConfigurator<T, TProperty> Property<TProperty>(Expression<Func<T, TProperty>> property) =>
            _owner.Try(() => _inner.Property(property), new NoOpPropertyConfigurator<TProperty>(), "Custom property");

        public IPropertyConfigurator<T, TProperty> Property<TProperty>(string name, Func<T, TProperty> value) =>
            _owner.Try(() => _inner.Property(name, value), new NoOpPropertyConfigurator<TProperty>(), "Custom property");
    }
}
