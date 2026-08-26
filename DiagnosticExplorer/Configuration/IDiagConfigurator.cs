using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using DiagnosticExplorer.Logging;

namespace DiagnosticExplorer;

public interface IDiagConfigurator
{
    bool ApplyAttributes { get; set; }
    int DrillDownMaxItems { get; set; }
    void RegisterObjects(Func<IServiceProvider, IEnumerable<RegisteredObject>> findObjects);
    void ConfigureHosting(Action<IDiagnosticHostingConfigurator> configure);
    void ConfigureEventRouting(Action<EventSinkRouteOptions> configure);
    void DefaultFormat<T>(string formatString);
    void Configure<T>(Action<ITypeConfigurator<T>> configure);
    void ConfigureDrillDown<T>(Action<ITypeConfigurator<T>> configure);
}

public interface IDiagnosticHostingConfigurator
{
    IDiagnosticHostingConfigurator Enabled(bool enabled = true);
    IDiagnosticHostingConfigurator AddHost(DiagnosticHostType type, string url);
    IDiagnosticHostingConfigurator EventRetention(Action<EventRetentionOptions> configure);
}

public interface ITypeConfigurator<T>
{
    ITypeConfigurator<T> OptIn();
    ITypeConfigurator<T> OptOut();
    IDisposable CreateCategoryScope(string category);
    ITypeConfigurator<T> Include<TProperty>(Expression<Func<T, TProperty>> property);
    ITypeConfigurator<T> Exclude<TProperty>(Expression<Func<T, TProperty>> property);
    IPropertyConfigurator<T, TProperty> Property<TProperty>(Expression<Func<T, TProperty>> property);
    ICustomPropertyConfigurator<T> CustomProperty(string name, Func<T, object> value);
    ICollectionConfigurator<T, TItem> Collection<TItem>(Expression<Func<T, IEnumerable<TItem>>> property);
    IRateConfigurator<T> Rate(Expression<Func<T, RateCounter>> property);
    IDateConfigurator<T> Date(Expression<Func<T, DateTime>> property);
    IDateConfigurator<T> Date(Expression<Func<T, DateTime?>> property);
    IDateConfigurator<T> Date(Expression<Func<T, DateTimeOffset>> property);
    IDateConfigurator<T> Date(Expression<Func<T, DateTimeOffset?>> property);
    IExtendedPropertyConfigurator<T, TProperty> Extended<TProperty>(Expression<Func<T, TProperty>> property);
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
    IPropertyConfigurator<T, TProperty> WithDrillDown(bool enabled = true, int? maxItems = null);
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
    ICustomPropertyConfigurator<T> WithDrillDown(bool enabled = true, int? maxItems = null);
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

public interface ICollectionConfigurator<T, TItem> : IObjectPropertyConfigurator<T, ICollectionConfigurator<T, TItem>>
{
    ICollectionConfigurator<T, TItem> ShowCount(string name = null);
    ICollectionConfigurator<T, TItem> Concatenate(string separator = null, string name = null);
    ICollectionConfigurator<T, TItem> List(Action<ICollectionListConfigurator<TItem>> configure = null);
    ICollectionConfigurator<T, TItem> Categories(Expression<Func<TItem, object>> category, string name = null);
    ICollectionConfigurator<T, TItem> WithMaxItems(int maxItems);
    ICollectionConfigurator<T, TItem> WithDrillDown(bool enabled = true, int? maxItems = null);
}

public interface ICollectionListConfigurator<TItem>
{
    ICollectionListConfigurator<TItem> Name(Func<TItem, string> format);
    ICollectionListConfigurator<TItem> Description(Func<TItem, string> format);
    ICollectionListConfigurator<TItem> Value(Func<TItem, string> format);
    ICollectionListConfigurator<TItem> Category(Func<TItem, string> format);
}

public interface IRateConfigurator<T> : IObjectPropertyConfigurator<T, IRateConfigurator<T>>
{
    IRateConfigurator<T> ShowRate(bool expose = true);
    IRateConfigurator<T> ShowTotal(bool expose = true);
}

public interface IDateConfigurator<T> : IObjectPropertyConfigurator<T, IDateConfigurator<T>>
{
    IDateConfigurator<T> ShowDate(bool expose = true);
    IDateConfigurator<T> ShowElapsed(bool expose = true);
    IDateConfigurator<T> ShowTimeUntil(bool expose = true);
}

public interface IExtendedPropertyConfigurator<T, TProperty> : IObjectPropertyConfigurator<T, IExtendedPropertyConfigurator<T, TProperty>> { }
