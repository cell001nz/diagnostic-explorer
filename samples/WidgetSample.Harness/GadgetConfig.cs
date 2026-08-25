using System;
using System.Collections.Generic;
using System.Linq;

namespace WidgetSample.Harness;

public sealed class GadgetConfig
{
    private readonly Random _random = new();

    public string ProfileName { get; private set; } = "Assembly Line";
    public string OperatingMode { get; private set; } = "Balanced";
    public string LocationCode { get; private set; } = "PLANT-A-14";
    public string FirmwareChannel { get; private set; } = "Stable";
    public int MaxConcurrentJobs { get; private set; } = 8;
    public int PollingIntervalSeconds { get; private set; } = 15;
    public int BatchSize { get; private set; } = 50;
    public decimal VoltageLimit { get; private set; } = 240m;
    public decimal EnergyBudgetKwh { get; private set; } = 18.5m;
    public decimal QualityThreshold { get; private set; } = 98.5m;
    public DateTime CommissionedOn { get; private set; } = DateTime.Today.AddYears(-2);
    public DateTime LastProfileReview { get; private set; } = DateTime.Today.AddDays(-30);
    public GadgetPowerConfig Power { get; } = new();
    public GadgetNetworkConfig Network { get; } = new();
    public GadgetMaintenanceConfig Maintenance { get; } = new();

    public void RefreshValues(decimal percentage)
    {
        if (percentage <= 0 || percentage > 1)
            throw new ArgumentOutOfRangeException(nameof(percentage));

        List<Action> mutations = new()
        {
            () => ProfileName = Pick("Assembly Line", "Precision Bench", "Night Shift", "Field Kit"),
            () => OperatingMode = Pick("Eco", "Balanced", "Performance", "Diagnostic"),
            () => LocationCode = $"PLANT-{Pick("A", "B", "C")}-{_random.Next(1, 50):D2}",
            () => FirmwareChannel = Pick("Stable", "Preview", "Canary", "Pinned"),
            () => MaxConcurrentJobs = _random.Next(1, 33),
            () => PollingIntervalSeconds = _random.Next(1, 121),
            () => BatchSize = _random.Next(10, 501),
            () => VoltageLimit = DecimalBetween(_random, 110m, 480m),
            () => EnergyBudgetKwh = DecimalBetween(_random, 5m, 50m),
            () => QualityThreshold = DecimalBetween(_random, 90m, 100m),
            () => CommissionedOn = DateTime.Today.AddDays(-_random.Next(30, 3650)),
            () => LastProfileReview = DateTime.Today.AddDays(-_random.Next(1, 180)),
        };

        Power.AddMutations(mutations, _random);
        Network.AddMutations(mutations, _random);
        Maintenance.AddMutations(mutations, _random);

        int changes = Math.Max(1, (int)Math.Ceiling(mutations.Count * percentage));
        foreach (Action mutation in mutations.OrderBy(_ => _random.Next()).Take(changes))
            mutation();
    }

    internal static decimal DecimalBetween(Random random, decimal minimum, decimal maximum) =>
        decimal.Round(minimum + (decimal)random.NextDouble() * (maximum - minimum), 2);

    internal static T Pick<T>(Random random, params T[] values) => values[random.Next(values.Length)];

    private string Pick(params string[] values) => Pick(_random, values);
}

public sealed class GadgetPowerConfig
{
    public string SupplyType { get; private set; } = "Three Phase";
    public decimal NominalVoltage { get; private set; } = 230m;
    public decimal MaxCurrentAmps { get; private set; } = 16m;
    public decimal IdleDrawWatts { get; private set; } = 42m;
    public decimal PeakDrawWatts { get; private set; } = 1850m;
    public decimal BatteryCapacityWh { get; private set; } = 950m;
    public int LowBatteryPercent { get; private set; } = 20;
    public int ShutdownDelaySeconds { get; private set; } = 45;
    public DateTime LastLoadTest { get; private set; } = DateTime.Today.AddDays(-21);
    public DateTime BatteryReplacementDue { get; private set; } = DateTime.Today.AddMonths(9);

    internal void AddMutations(List<Action> mutations, Random random)
    {
        mutations.Add(() => SupplyType = GadgetConfig.Pick(random, "Single Phase", "Three Phase", "Battery", "Hybrid"));
        mutations.Add(() => NominalVoltage = GadgetConfig.DecimalBetween(random, 110m, 480m));
        mutations.Add(() => MaxCurrentAmps = GadgetConfig.DecimalBetween(random, 5m, 40m));
        mutations.Add(() => IdleDrawWatts = GadgetConfig.DecimalBetween(random, 10m, 100m));
        mutations.Add(() => PeakDrawWatts = GadgetConfig.DecimalBetween(random, 500m, 3000m));
        mutations.Add(() => BatteryCapacityWh = GadgetConfig.DecimalBetween(random, 250m, 2500m));
        mutations.Add(() => LowBatteryPercent = random.Next(5, 41));
        mutations.Add(() => ShutdownDelaySeconds = random.Next(5, 181));
        mutations.Add(() => LastLoadTest = DateTime.Today.AddDays(-random.Next(1, 120)));
        mutations.Add(() => BatteryReplacementDue = DateTime.Today.AddDays(random.Next(30, 730)));
    }
}

public sealed class GadgetNetworkConfig
{
    public string HostName { get; private set; } = "gadget-floor-14";
    public string Address { get; private set; } = "10.42.14.20";
    public string Gateway { get; private set; } = "10.42.14.1";
    public string Transport { get; private set; } = "MQTT";
    public int Port { get; private set; } = 8883;
    public int ConnectionTimeoutSeconds { get; private set; } = 10;
    public int RetryLimit { get; private set; } = 5;
    public decimal BackoffMultiplier { get; private set; } = 1.75m;
    public DateTime CertificateExpiry { get; private set; } = DateTime.Today.AddMonths(6);
    public DateTime LastConnected { get; private set; } = DateTime.Now;

    internal void AddMutations(List<Action> mutations, Random random)
    {
        mutations.Add(() => HostName = $"gadget-floor-{random.Next(1, 100)}");
        mutations.Add(() => Address = $"10.42.{random.Next(1, 255)}.{random.Next(2, 255)}");
        mutations.Add(() => Gateway = $"10.42.{random.Next(1, 255)}.1");
        mutations.Add(() => Transport = GadgetConfig.Pick(random, "MQTT", "AMQP", "HTTPS", "WebSocket"));
        mutations.Add(() => Port = GadgetConfig.Pick(random, 443, 1883, 5671, 8883));
        mutations.Add(() => ConnectionTimeoutSeconds = random.Next(2, 61));
        mutations.Add(() => RetryLimit = random.Next(1, 16));
        mutations.Add(() => BackoffMultiplier = GadgetConfig.DecimalBetween(random, 1m, 5m));
        mutations.Add(() => CertificateExpiry = DateTime.Today.AddDays(random.Next(1, 730)));
        mutations.Add(() => LastConnected = DateTime.Now.AddMinutes(-random.Next(0, 240)));
    }
}

public sealed class GadgetMaintenanceConfig
{
    public string ServiceTier { get; private set; } = "Gold";
    public string AssignedTeam { get; private set; } = "Reliability";
    public string LastServiceResult { get; private set; } = "Passed";
    public int ServiceIntervalDays { get; private set; } = 90;
    public int OperatingHours { get; private set; } = 4250;
    public int RemainingCycles { get; private set; } = 18000;
    public decimal WearPercent { get; private set; } = 14.25m;
    public decimal VibrationLimitMmPerSecond { get; private set; } = 4.5m;
    public DateTime LastServiceDate { get; private set; } = DateTime.Today.AddDays(-45);
    public DateTime NextServiceDate { get; private set; } = DateTime.Today.AddDays(45);

    internal void AddMutations(List<Action> mutations, Random random)
    {
        mutations.Add(() => ServiceTier = GadgetConfig.Pick(random, "Bronze", "Silver", "Gold", "Platinum"));
        mutations.Add(() => AssignedTeam = GadgetConfig.Pick(random, "Reliability", "Facilities", "Vendor", "Night Shift"));
        mutations.Add(() => LastServiceResult = GadgetConfig.Pick(random, "Passed", "Advisory", "Parts Ordered", "Follow-up"));
        mutations.Add(() => ServiceIntervalDays = random.Next(30, 366));
        mutations.Add(() => OperatingHours = random.Next(100, 20000));
        mutations.Add(() => RemainingCycles = random.Next(1000, 50001));
        mutations.Add(() => WearPercent = GadgetConfig.DecimalBetween(random, 0m, 100m));
        mutations.Add(() => VibrationLimitMmPerSecond = GadgetConfig.DecimalBetween(random, 1m, 12m));
        mutations.Add(() => LastServiceDate = DateTime.Today.AddDays(-random.Next(1, 365)));
        mutations.Add(() => NextServiceDate = DateTime.Today.AddDays(random.Next(1, 365)));
    }
}
