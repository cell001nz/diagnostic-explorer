using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using DiagnosticExplorer.Logging;

namespace DiagnosticExplorer;

internal sealed class DeferredDiagnosticConfigurator : IDiagConfigurator
{
    private readonly DiagnosticConfiguration _configuration;
    private readonly List<Action<IDiagConfigurator>> _deferred = new();
    private bool _applyAttributes;
    private int _drillDownMaxItems;

    public DeferredDiagnosticConfigurator(DiagnosticConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _applyAttributes = configuration.ApplyAttributes;
        _drillDownMaxItems = configuration.DrillDownMaxItems;
    }

    public bool ApplyAttributes
    {
        get => _applyAttributes;
        set
        {
            _applyAttributes = value;
            _deferred.Add(configure => configure.ApplyAttributes = value);
        }
    }

    public int DrillDownMaxItems
    {
        get => _drillDownMaxItems;
        set
        {
            _drillDownMaxItems = value;
            _deferred.Add(configure => configure.DrillDownMaxItems = value);
        }
    }

    public void RegisterObjects(Action<IDiagRegistrar> configure)
    {
        _deferred.Add(configurator => configurator.RegisterObjects(configure));
    }

    public void ConfigureHosting(Action<IDiagnosticHostingConfigurator> configure)
    {
        _configuration.ConfigureHosting(configure);
    }

    public ISystemEnvironmentConfigurator ConfigureSystemEnvironment()
    {
        return _configuration.ConfigureSystemEnvironment();
    }

    public void ConfigureEventRouting(Action<EventSinkRouteOptions> configure)
    {
        _configuration.ConfigureEventRouting(configure);
    }

    public void ConfigureLogEventRetention(Action<LogEventRetentionOptions> configure)
    {
        _configuration.ConfigureLogEventRetention(configure);
    }

    public void DefaultFormat<T>(string formatString)
    {
        _deferred.Add(configure => configure.DefaultFormat<T>(formatString));
    }

    public void ConfigureAssemblies(params Assembly[] assemblies)
    {
        _deferred.Add(configure => configure.ConfigureAssemblies(assemblies));
    }

    public void Configure<T>(Action<ITypeConfigurator<T>> configure)
    {
        _deferred.Add(configurator => configurator.Configure<T>(options => configure(new ResilientTypeConfigurator<T>(options))));
    }

    public void ConfigureDrillDown<T>(Action<ITypeConfigurator<T>> configure)
    {
        _deferred.Add(configurator => configurator.ConfigureDrillDown<T>(options => configure(new ResilientTypeConfigurator<T>(options))));
    }

    public void ApplyDeferredConfiguration(IDiagConfigurator configurator)
    {
        foreach (Action<IDiagConfigurator> configure in _deferred)
        {
            try
            {
                configure(configurator);
            }
            catch (Exception exception)
            {
                Trace.TraceError($"Diagnostic Explorer ignored invalid deferred configuration: {exception}");
            }
        }
    }
}
