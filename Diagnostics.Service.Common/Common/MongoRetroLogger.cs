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
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Diagnostics.Service.Common.Transport;
using log4net;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Newtonsoft.Json;

namespace DiagnosticExplorer;

public class MongoRetroLogger : IRetroLogger
{

    private static ILog _log = LogManager.GetLogger(typeof(MongoRetroLogger));

    static MongoRetroLogger()
    {
        BsonClassMap<DiagnosticMsg> map = new();
        map.MapProperty(nameof(DiagnosticMsg.Category));
        map.MapProperty(nameof(DiagnosticMsg.Date));
        map.MapProperty(nameof(DiagnosticMsg.Level));
        map.MapProperty(nameof(DiagnosticMsg.Machine));
        map.MapProperty(nameof(DiagnosticMsg.Message));
        map.MapProperty(nameof(DiagnosticMsg.Process));
        map.MapProperty(nameof(DiagnosticMsg.User));
        BsonClassMap.RegisterClassMap(map);       
        
        BsonClassMap<RetroMsg> map2 = new();
        map2.MapIdProperty(nameof(RetroMsg.RecordId));
        map2.MapProperty(nameof(RetroMsg.Category));
        map2.MapProperty(nameof(RetroMsg.Date));
        map2.MapProperty(nameof(RetroMsg.Level));
        map2.MapProperty(nameof(RetroMsg.Machine));
        map2.MapProperty(nameof(RetroMsg.Message));
        map2.MapProperty(nameof(RetroMsg.Process));
        map2.MapProperty(nameof(RetroMsg.User));
        BsonClassMap.RegisterClassMap(map2);
        
        BsonClassMap<DeleteMsg> map3 = new();
        map3.MapIdProperty(nameof(DeleteMsg.RecordId));
        BsonClassMap.RegisterClassMap(map3);
    }

    public MongoRetroLogger(string name, string connectionString)
    {
        Name = name;
        ConnectionString = connectionString;
    }

    public string Name { get; }
    public string ConnectionString { get; set; }


    public async Task<long> Delete(string[] recordList)
    {
        MongoClient client = new(ConnectionString);
        IMongoDatabase db = client.GetDatabase("Diagnostics");
        IMongoCollection<RetroMsg> collection = db.GetCollection<RetroMsg>("Log");

        ObjectId[] ids = recordList.Select(ObjectId.Parse).ToArray();

        FilterDefinition<RetroMsg> filter = new ExpressionFilterDefinition<RetroMsg>(msg => ids.Contains(msg.RecordId));

        DeleteResult? result = await collection
            .DeleteManyAsync(filter, CancellationToken.None)
            .ConfigureAwait(false);

        return result.DeletedCount;
    }

    public async IAsyncEnumerable<RetroMsg[]> GetMessages(RetroQuery query, [EnumeratorCancellation] CancellationToken cancel)
    {
        MongoClient client = new(ConnectionString);
        IMongoDatabase db = client.GetDatabase("Diagnostics");
        IMongoCollection<RetroMsg> collection = db.GetCollection<RetroMsg>("Log");
        

        FindOptions<RetroMsg> options = new() {
            Limit = query.MaxRecords,
            BatchSize = 250,
            Sort = Builders<RetroMsg>.Sort.Descending(msg => msg.Date)
        };

        FilterDefinition<RetroMsg> filter = new ExpressionFilterDefinition<RetroMsg>(msg =>
            msg.Level >= query.MinLevel
            && msg.Date >= query.StartDate
            && msg.Date < query.EndDate);

        if (!string.IsNullOrWhiteSpace(query.Machine))
            filter &= new ExpressionFilterDefinition<RetroMsg>(msg => Regex.IsMatch(msg.Machine, query.Machine, RegexOptions.IgnoreCase));

        if (!string.IsNullOrWhiteSpace(query.User))
            filter &= new ExpressionFilterDefinition<RetroMsg>(msg => Regex.IsMatch(msg.User, query.User, RegexOptions.IgnoreCase));

        if (!string.IsNullOrWhiteSpace(query.Process))
            filter &= new ExpressionFilterDefinition<RetroMsg>(msg => Regex.IsMatch(msg.Process, query.Process, RegexOptions.IgnoreCase));

        if (!string.IsNullOrWhiteSpace(query.Message))
            filter &= new ExpressionFilterDefinition<RetroMsg>(msg => Regex.IsMatch(msg.Message, query.Message, RegexOptions.IgnoreCase));

        IAsyncCursor<RetroMsg> searchResult = await collection.FindAsync(filter, options, cancel)
            .ConfigureAwait(false);

        while (await searchResult.MoveNextAsync(cancel))
        {
            foreach (var item in searchResult.Current)
                item.Date = item.Date.ToLocalTime();

            yield return searchResult.Current.ToArray();
        }
    }

    public async Task WriteMessages(ICollection<DiagnosticMsg> msg, CancellationToken cancel)
    {
        MongoClient client = new(ConnectionString);
        IMongoDatabase database = client.GetDatabase("Diagnostics");
        IMongoCollection<DiagnosticMsg> collection = database.GetCollection<DiagnosticMsg>("Log");
        await collection.InsertManyAsync(msg, options: null, cancel);
    }
}