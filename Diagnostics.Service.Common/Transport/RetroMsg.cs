using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using DiagnosticExplorer;
using MongoDB.Bson;

namespace Diagnostics.Service.Common.Transport
{
    public class RetroMsg
    {

        public int Level { get; set; }

        public DateTime Date { get; set; }

        public string Machine { get; set; }

        public string Process { get; set; }

        public string User { get; set; }

        public string Category { get; set; }

        public string Message { get; set; }

        [JsonIgnore]
        public ObjectId RecordId { get; set; }

        public string MsgId => RecordId.ToString();
    }

    public class DeleteMsg
    {
        public ObjectId RecordId { get; set; }
    }
}
