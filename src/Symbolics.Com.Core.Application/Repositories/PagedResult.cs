namespace Symbolics.Com.Core.Application.Repositories;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount);
