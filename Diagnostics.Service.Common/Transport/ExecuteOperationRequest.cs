namespace Diagnostics.Service.Common.Transport;

public class ExecuteOperationRequest
{
    public string Id { get; set; }

    public string Path { get; set; }

    public string Operation { get; set; }

    public string[] Arguments { get; set; }
}