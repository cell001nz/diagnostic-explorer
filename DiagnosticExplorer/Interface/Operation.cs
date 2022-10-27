using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using DiagnosticExplorer.Util;
using ProtoBuf;

namespace DiagnosticExplorer
{

	[ProtoContract(UseProtoMembersOnly = true)]
	[DataContract(Namespace = "http://diagnosticexplorer.com/2010")]
	public class Operation
	{
		public Operation()
		{
		}

		public Operation(MethodInfo methodInfo) : this()
		{
			MethodInfo = methodInfo;
			Signature = methodInfo.Name;

			Parameters = methodInfo.GetParameters()
				.Select(x => new OperationParameter(x.Name, TypeUtil.GetFriendlyTypeName(x.ParameterType)))
				.ToList();

			string[] paramTypes = Parameters.Select(x => x.Type).ToArray();
			Signature = string.Format("{0}({1})", methodInfo.Name, string.Join(", ", paramTypes));
			ReturnType = TypeUtil.GetFriendlyTypeName(methodInfo.ReturnType);
		}

		[ProtoMember(1)]
		[DataMember]
		public string ReturnType { get; set; }

		[ProtoMember(2)]
		[DataMember]
		public string Signature { get; set; }

		[ProtoMember(3)]
		[DataMember]
		public string Description { get; set; }

		[ProtoMember(4)]
		[DataMember]
		public List<OperationParameter> Parameters { get; set; }
		
		internal MethodInfo MethodInfo { get; private set; }

	}
}
