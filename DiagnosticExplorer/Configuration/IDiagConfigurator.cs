using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using DiagnosticExplorer.Logging;

namespace DiagnosticExplorer;

public interface IDiagConfigurator
{
    bool ApplyAttributes { get; set; }
    int DrillDownMaxItems { get; set; }
    void RegisterObjects(Action<IDiagRegistrar> configure);
    void ConfigureHosting(Action<IDiagnosticHostingConfigurator> configure);
    void ConfigureEventRouting(Action<EventSinkRouteOptions> configure);
    void ConfigureLogEventRetention(Action<LogEventRetentionOptions> configure);
    void DefaultFormat<T>(string formatString);
    void Configure<T>(Action<ITypeConfigurator<T>> configure);
    void ConfigureDrillDown<T>(Action<ITypeConfigurator<T>> configure);
}

public interface IDiagRegistrar : IServiceProvider
{
    void Register(object value, string category, string name);
    void RegisterService<TService>(string category, string name);
}

public interface IDiagnosticHostingConfigurator
{
    IDiagnosticHostingConfigurator Enabled(bool enabled = true);
    IDiagnosticHostingConfigurator AddHost(DiagnosticHostType type, string url);
    IDiagnosticHostingConfigurator EventRetention(Action<EventRetentionOptions> configure);
}

public interface ITypeConfigurator<T>
{
    ITypeConfigurator<T> ExcludeAll();
    ITypeConfigurator<T> IncludeAll();
    IDisposable CreateCategoryScope(string category);
    ITypeConfigurator<T> Include<TProperty>(Expression<Func<T, TProperty>> property);
    ITypeConfigurator<T> Exclude<TProperty>(Expression<Func<T, TProperty>> property);
    IPropertyConfigurator<T, TProperty> Property<TProperty>(Expression<Func<T, TProperty>> property);
    IPropertyConfigurator<T, TProperty> Property<TProperty>(string name, Func<T, TProperty> value);
    ICustomPropertyConfigurator<T> Custom(string name, Action<ICustomObjectConfigurator<T>> configure);
    ITypeConfigurator<T> Route(string loggerName, LoggerNameMatchMode matchMode, Action<DrillDownEventRoute> configure);
    ITypeConfigurator<T> Route(Func<T, string> loggerName, LoggerNameMatchMode matchMode, Action<DrillDownEventRoute> configure);
}

public interface IPropertyConfigurator
{
    IPropertyConfigurator Named(string name);
    IPropertyConfigurator Category(string category);
    IPropertyConfigurator Description(string description);
    IPropertyConfigurator Format(string formatString);
    IPropertyConfigurator AllowSet(bool allowSet = true);
}

public interface IObjectPropertyConfigurator<T, TSelf> : IPropertyConfigurator
{
    new TSelf Named(string name);
    TSelf Named(Func<T, string> name);
    new TSelf Category(string category);
    TSelf Category(Func<T, string> category);
    new TSelf Description(string description);
    TSelf Description(Func<T, string> description);
    new TSelf Format(string formatString);
    new TSelf AllowSet(bool allowSet = true);
}

public interface IPropertyConfigurator<T, TProperty> : IObjectPropertyConfigurator<T, IPropertyConfigurator<T, TProperty>>
{
    IPropertyConfigurator<T, TProperty> Format(Func<TProperty, string> format);
    IPropertyConfigurator<T, TProperty> AsJson(int maxLength = 100);
    IPropertyConfigurator<T, TProperty> AsDateOnly();
    IPropertyConfigurator<T, TProperty> WithDrillDown(bool enabled = true, int? maxItems = null);
    IPropertyConfigurator<T, TProperty> AsDrillDown(int? maxItems = null);
    IPropertyConfigurator<T, TProperty> AsDrillDownIcon(int? maxItems = null);
    IPropertyConfigurator<T, TProperty> AsDrillDownIcon(string text, int? maxItems = null);
    IPropertyConfigurator<T, TProperty> WithJsonHover(bool enabled = true);
    IPropertyConfigurator<T, TProperty> WithExpandedHover(bool enabled = true);
    IPropertyConfigurator<T, TProperty> Warn(Func<T, bool> condition, string message);
    IPropertyConfigurator<T, TProperty> Warn(Func<T, bool> condition, string message, string category);
    IPropertyConfigurator<T, TProperty> Warn(Func<T, bool> condition, Func<T, string> message);
    IPropertyConfigurator<T, TProperty> Warn(Func<T, bool> condition, Func<T, string> message, string category);
    IPropertyConfigurator<T, TProperty> Error(Func<T, bool> condition, string message);
    IPropertyConfigurator<T, TProperty> Error(Func<T, bool> condition, string message, string category);
    IPropertyConfigurator<T, TProperty> Error(Func<T, bool> condition, Func<T, string> message);
    IPropertyConfigurator<T, TProperty> Error(Func<T, bool> condition, Func<T, string> message, string category);
}

public interface ICustomPropertyConfigurator<T>
{
    ICustomPropertyConfigurator<T> AsJson(int maxLength = 100);
    ICustomPropertyConfigurator<T> WithDrillDown(bool enabled = true, int? maxItems = null);
    ICustomPropertyConfigurator<T> AsDrillDown(int? maxItems = null);
    ICustomPropertyConfigurator<T> AsDrillDownIcon(int? maxItems = null);
    ICustomPropertyConfigurator<T> AsDrillDownIcon(string text, int? maxItems = null);
    ICustomPropertyConfigurator<T> WithJsonHover(bool enabled = true);
    ICustomPropertyConfigurator<T> WithExpandedHover(bool enabled = true);
    ICustomPropertyConfigurator<T> Category(string category);
    ICustomPropertyConfigurator<T> Category(Func<T, string> category);
    ICustomPropertyConfigurator<T> Description(string description);
    ICustomPropertyConfigurator<T> Description(Func<T, string> description);
    ICustomPropertyConfigurator<T> Warn(Func<T, bool> condition, string message);
    ICustomPropertyConfigurator<T> Warn(Func<T, bool> condition, string message, string category);
    ICustomPropertyConfigurator<T> Warn(Func<T, bool> condition, Func<T, string> message);
    ICustomPropertyConfigurator<T> Warn(Func<T, bool> condition, Func<T, string> message, string category);
    ICustomPropertyConfigurator<T> Error(Func<T, bool> condition, string message);
    ICustomPropertyConfigurator<T> Error(Func<T, bool> condition, string message, string category);
    ICustomPropertyConfigurator<T> Error(Func<T, bool> condition, Func<T, string> message);
    ICustomPropertyConfigurator<T> Error(Func<T, bool> condition, Func<T, string> message, string category);
}

public interface ICustomObjectConfigurator<T>
{
    IPropertyConfigurator<T, TProperty> Property<TProperty>(Expression<Func<T, TProperty>> property);
    IPropertyConfigurator<T, TProperty> Property<TProperty>(string name, Func<T, TProperty> value);
}

public interface ICollectionConfigurator<T, TItem> : IObjectPropertyConfigurator<T, ICollectionConfigurator<T, TItem>>
{
    ICollectionConfigurator<T, TItem> ShowCount(string name = null);
    ICollectionConfigurator<T, TItem> ConcatItems(string separator = null, Func<TItem, string> format = null);
    ICollectionConfigurator<T, TItem> ConcatItems(Func<TItem, string> format);
    ICollectionConfigurator<T, TItem> ListItems(Action<ICollectionListConfigurator<TItem>> configure = null);
    ICollectionConfigurator<T, TItem> SectionByItem(Func<TItem, object> category, string name = null);
    ICollectionConfigurator<T, TItem> WithMaxItems(int maxItems);
    ICollectionConfigurator<T, TItem> WithDrillDown(bool enabled = true, int? maxItems = null);
    ICollectionConfigurator<T, TItem> AsDrillDown(bool enabled = true, int? maxItems = null);
    ICollectionConfigurator<T, TItem> AsDrillDownIcon(int? maxItems = null);
    ICollectionConfigurator<T, TItem> AsDrillDownIcon(string text, int? maxItems = null);
}

public interface ICollectionListConfigurator<TItem>
{
    ICollectionListConfigurator<TItem> Name(Func<TItem, string> format);
    ICollectionListConfigurator<TItem> Description(Func<TItem, string> format);
    ICollectionListConfigurator<TItem> Value(Func<TItem, string> format);
    ICollectionListConfigurator<TItem> Category(Func<TItem, string> format);
}
