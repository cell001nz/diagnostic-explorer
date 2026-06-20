
namespace Diagnostics.Service.Common.Transport;

public class OperationRequest
{
    public string ProcessId { get; set; }

    public string Path { get; set; }

    public string Operation { get; set; }

    public string[] Arguments { get; set; }
}