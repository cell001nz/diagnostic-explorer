#region Copyright

// Diagnostic Explorer, a .Net diagnostic toolset
// Copyright (C) 2010 Cameron Elliot
// 
// This file is part of Diagnostic Explorer.
// 
// Diagnostic Explorer is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Diagnostic Explorer is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public License
// along with Diagnostic Explorer.  If not, see <http://www.gnu.org/licenses/>.
// 
// http://diagexplorer.sourceforge.net/

#endregion

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using ProtoBuf;

namespace DiagnosticExplorer
{

	[ProtoContract(UseProtoMembersOnly = true)]
	[DataContract(Namespace = "http://diagnosticexplorer.com/2010")]
	public class DiagnosticResponse
	{
		public DiagnosticResponse()
		{
			Events = new List<EventResponse>();
			PropertyBags = new List<PropertyBag>();
			OperationSets = new List<OperationSet>();
		}

		[ProtoMember(1)]
		[DataMember]
		public List<PropertyBag> PropertyBags { get; set; }

		[ProtoMember(2)]
		[DataMember]
		public List<EventResponse> Events { get; set; }

		[ProtoMember(3)]
		[DataMember]
		public string Context { get; set; }

		[ProtoMember(4)]
		[DataMember]
		public string ExceptionMessage { get; set; }

		[ProtoMember(5)]
		[DataMember]
		public string ExceptionDetail { get; set; }

		[ProtoMember(6)]
		[DataMember(Order = 1, IsRequired = false)]
		public List<OperationSet> OperationSets { get; set; }

		[ProtoIgnore]
		[DataMember(Order = 2, IsRequired = false)]
		public byte[] ProtoResponse { get; set; }

	}
}