using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using ProtoBuf;

namespace DiagnosticExplorer
{
	[ProtoContract(UseProtoMembersOnly = true)]
	[DataContract(Namespace = "http://diagnosticexplorer.com/2010")]
	public class Category
	{
		public Category()
		{
			Properties = new List<Property>();
		}

		public Category(string name) : this()
		{
			Name = name;
		}

		[ProtoMember(1)]
		[DataMember]
		public string Name { get; set; }

		[ProtoMember(2)]
		[DataMember]
		public string OperationSet { get; set; }

		[ProtoMember(3)]
		[DataMember]
		public List<Property> Properties { get; set; }

		internal object ValueObject { get; set; }

	}

	public static class CategoryExtensions
	{
		private static readonly StringComparer _ignoreCase = StringComparer.CurrentCultureIgnoreCase;

		public static Category FindByName(this IEnumerable<Category> list, string name)
		{
			if (list == null) throw new ArgumentNullException(nameof(list));

			return list.FirstOrDefault(x => _ignoreCase.Equals(x.Name, name));
		}
	}
}
