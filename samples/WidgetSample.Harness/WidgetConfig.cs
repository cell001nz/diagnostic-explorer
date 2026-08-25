using System;
using System.Collections.Generic;
using System.Linq;

namespace WidgetSample.Harness;

public sealed class WidgetConfig
{
    private readonly Random _random = new();

    public WidgetConfig()
    {
        Items = new List<WidgetConfigItem>
        {
            new("Northstar", "Telemetry", 12, 1.25m, DateTime.Today.AddDays(-12)),
            new("Orchard", "Fulfilment", 7, 0.85m, DateTime.Today.AddDays(-7)),
            new("Beacon", "Quality", 19, 2.5m, DateTime.Today.AddDays(-19)),
        };
    }

    public string EnvironmentName { get; private set; } = "Workshop";

    public string ReleaseChannel { get; private set; } = "Canary";

    public int RefreshIntervalSeconds { get; private set; } = 30;

    public decimal TemperatureThreshold { get; private set; } = 72.5m;

    public DateTime LastCalibrationDate { get; private set; } = DateTime.Today.AddDays(-14);

    public WidgetConnectionConfig Connection { get; } = new();

    public List<WidgetConfigItem> Items { get; }

    public void RandomlyChangeValues(decimal percentage)
    {
        if (percentage <= 0 || percentage > 1)
            throw new ArgumentOutOfRangeException(nameof(percentage));

        List<Action> mutations = new()
        {
            () => EnvironmentName = Pick("Workshop", "Foundry", "Observatory", "Field Lab"),
            () => ReleaseChannel = Pick("Canary", "Preview", "Stable", "Emergency"),
            () => RefreshIntervalSeconds = _random.Next(5, 121),
            () => TemperatureThreshold = decimal.Round((decimal)_random.NextDouble() * 100m, 2),
            () => LastCalibrationDate = DateTime.Today.AddDays(-_random.Next(1, 120)),
        };

        Connection.AddMutations(mutations, _random);
        foreach (WidgetConfigItem item in Items)
            item.AddMutations(mutations, _random);

        int changes = Math.Max(1, (int)Math.Ceiling(mutations.Count * percentage));
        foreach (Action mutation in mutations.OrderBy(_ => _random.Next()).Take(changes))
            mutation();
    }

    private string Pick(params string[] values) => values[_random.Next(values.Length)];
}

public sealed class WidgetConfigItem
{
    public WidgetConfigItem(string name, string purpose, int capacity, decimal tolerance, DateTime installedDate)
    {
        Name = name;
        Purpose = purpose;
        Capacity = capacity;
        Tolerance = tolerance;
        InstalledDate = installedDate;
    }

    public string Name { get; private set; }

    public string Purpose { get; private set; }

    public int Capacity { get; private set; }

    public decimal Tolerance { get; private set; }

    public DateTime InstalledDate { get; private set; }

    internal void AddMutations(List<Action> mutations, Random random)
    {
        mutations.Add(() => Name = Pick(random, "Northstar", "Orchard", "Beacon", "Sundial"));
        mutations.Add(() => Purpose = Pick(random, "Telemetry", "Fulfilment", "Quality", "Calibration"));
        mutations.Add(() => Capacity = random.Next(1, 50));
        mutations.Add(() => Tolerance = decimal.Round((decimal)random.NextDouble() * 5m, 2));
        mutations.Add(() => InstalledDate = DateTime.Today.AddDays(-random.Next(1, 365)));
    }

    private static string Pick(Random random, params string[] values) => values[random.Next(values.Length)];
}

public sealed class WidgetConnectionConfig
{
    public string Endpoint { get; private set; } = "wss://widgets.example.test/telemetry";

    public int RetryLimit { get; private set; } = 4;

    public decimal BackoffMultiplier { get; private set; } = 1.75m;

    public DateTime CertificateExpiry { get; private set; } = DateTime.Today.AddDays(90);

    internal void AddMutations(List<Action> mutations, Random random)
    {
        mutations.Add(() => Endpoint = $"wss://widgets-{random.Next(1, 10)}.example.test/telemetry");
        mutations.Add(() => RetryLimit = random.Next(1, 10));
        mutations.Add(() => BackoffMultiplier = decimal.Round(1m + (decimal)random.NextDouble() * 4m, 2));
        mutations.Add(() => CertificateExpiry = DateTime.Today.AddDays(random.Next(1, 365)));
    }
}
