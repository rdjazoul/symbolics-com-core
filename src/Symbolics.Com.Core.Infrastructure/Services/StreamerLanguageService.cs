using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Symbolics.Com.Core.Application.Services;
using Symbolics.Com.Core.Infrastructure.Persistence;

namespace Symbolics.Com.Core.Infrastructure.Services;

public sealed class StreamerLanguageService(CoreDbContext dbContext, ILogger<StreamerLanguageService> logger)
    : IStreamerLanguageService
{
    private readonly CoreDbContext _dbContext = dbContext;
    private readonly ILogger<StreamerLanguageService> _logger = logger;

    public async Task UpdateStreamerLanguageAsync(Guid streamerId, CancellationToken cancellationToken = default)
    {
        var topLanguage = await _dbContext.GamePlays
            .AsNoTracking()
            .Where(entry => entry.StreamerId == streamerId)
            .GroupBy(entry => entry.Language)
            .Select(group => new { Language = group.Key, Count = group.Count() })
            .OrderByDescending(entry => entry.Count)
            .ThenBy(entry => entry.Language)
            .Select(entry => entry.Language)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(topLanguage))
        {
            return;
        }

        var streamer = await _dbContext.Streamers
            .SingleOrDefaultAsync(entry => entry.Id == streamerId, cancellationToken);

        if (streamer is null)
        {
            return;
        }

        if (!string.Equals(streamer.Language, topLanguage, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "Streamer {StreamerId} language updated: {PreviousLanguage} -> {NewLanguage}",
                streamerId,
                streamer.Language ?? "unknown",
                topLanguage);
            streamer.Language = topLanguage;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
