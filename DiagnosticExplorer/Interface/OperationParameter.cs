using System.Runtime.Serialization;
using ProtoBuf;

namespace DiagnosticExplorer
{
	[ProtoContract(UseProtoMembersOnly = true)]
	[DataContract(Namespace = "http://diagnosticexplorer.com/2010")]
	public class OperationParameter
	{
		public OperationParameter()
		{
		}

		public OperationParameter(string name, string type)
		{
			Name = name;
			Type = type;
		}

		[ProtoMember(1)]
		[DataMember]
		public string Name { get; set; }

		[ProtoMember(2)]
		[DataMember]
		public string Type { get; set; }

	}
}