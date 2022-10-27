using System;

namespace Diagnostics.Service.Common.Transport;

public class Event
{

    public string Id { get; set; }

    public DateTime Date { get; set; }

    public string Message { get; set; }

    public string Detail { get; set; }

    public string Severity { get; set; }
}