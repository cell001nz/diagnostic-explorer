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
using System.Runtime.Serialization;
using ProtoBuf;

namespace DiagnosticExplorer
{
    /// <summary>
    /// Describes something that happened.
    /// </summary>
    [ProtoContract(UseProtoMembersOnly = true)]
    [DataContract(Namespace = "http://diagnosticexplorer.com/2010")]
    public class SystemEvent
    {
        public SystemEvent()
        {
            Date = DateTime.UtcNow;
        }

        [ProtoMember(1)]
        [DataMember]
        public long Id { get; set; }

        [ProtoMember(2)]
        [DataMember]
        public DateTime Date { get; set; }

        [ProtoMember(3)]
        [DataMember]
        public string Message { get; set; }

        [ProtoMember(4)]
        [DataMember]
        public string Detail { get; set; }

        [ProtoMember(5)]
        [DataMember]
        public EventSeverity Severity { get; set; }

        [ProtoMember(6)]
        [DataMember]
        public int Level { get; set; }

        [ProtoMember(7)]
        [DataMember]
        public string SinkName { get; set; }

        [ProtoMember(8)]
        [DataMember]
        public string SinkCategory { get; set; }

        public override string ToString()
        {
            return $"{Id} {Date:d MMM yyyy H:mm:ss} {Severity} {Message}";
        }
    }
}