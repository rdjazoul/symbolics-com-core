namespace Symbolics.Com.Core.Contract.ExternalServices.Models;

public sealed record AirtableRecommendationRecord(
    string Name,
    string UrlTwitch,
    string Description,
    string Persona,
    string Language,
    string Email,
    bool Select);
