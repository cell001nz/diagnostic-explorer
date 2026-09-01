using System.Collections.Generic;
using System.Runtime.Serialization;

namespace DiagnosticExplorer;
public class OperationSet
{
	public OperationSet()
	{
		Operations = new List<Operation>();
	}
	public string Id { get; set; }
	public List<Operation> Operations { get; set; }

}