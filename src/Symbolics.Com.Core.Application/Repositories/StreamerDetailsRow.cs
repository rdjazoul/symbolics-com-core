using System;

namespace Symbolics.Com.Core.Application.Repositories;

public sealed record StreamerDetailsRow(
    Guid StreamerId,
    string? VectorDescription,
    string? PersonaDescription,
    string? Email,
    string? Language);
