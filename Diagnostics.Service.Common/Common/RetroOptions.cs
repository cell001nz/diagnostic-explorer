using System;

namespace DiagnosticExplorer.Common;

public class RetroOptions
{
    public const string Retro = "Retro";

    public string Type { get; set; }
    public string Name { get; set; }
    public string Connection { get; set; }

    public IRetroLogger CreateRetroLogger()
    {
        switch (Type.ToLower())
        {
            case "mongo":
                return new MongoRetroLogger(Name, Connection);

            default:
                throw new NotSupportedException($"ILogReader type {Type} not supported");
        }
    }
}