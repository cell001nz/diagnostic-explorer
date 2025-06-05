namespace Diagnostics.Service.Common.Transport;

public class SetPropertyRequest
{
    public string Id { get; set; }

    public string Path { get; set; }

    public string? Value { get; set; }
}