using Serilog.Events;

namespace Symbolics.Com.Core.Api.Logging;

public sealed class LogOptions
{
    public string SeqUrl { get; init; } = string.Empty;
    public LogEventLevel MinimumLevel { get; init; } = LogEventLevel.Information;
}
