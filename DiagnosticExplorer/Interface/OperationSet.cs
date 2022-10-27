using System.Collections.Generic;
using System.Runtime.Serialization;
using ProtoBuf;

namespace DiagnosticExplorer
{
	[ProtoContract(UseProtoMembersOnly = true)]
	[DataContract(Namespace = "http://diagnosticexplorer.com/2010")]
	public class OperationSet
	{
		public OperationSet()
		{
			Operations = new List<Operation>();
		}

		[ProtoMember(1)]
		[DataMember]
		public string Id { get; set; }

		[ProtoMember(2)]
		[DataMember]
		public List<Operation> Operations { get; set; }

	}
}