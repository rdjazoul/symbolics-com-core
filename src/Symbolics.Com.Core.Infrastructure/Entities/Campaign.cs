namespace Symbolics.Com.Core.Infrastructure.Entities;

public sealed class Campaign
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? UserDescription { get; set; }
    public string? OptimizedDescription { get; set; }
}
