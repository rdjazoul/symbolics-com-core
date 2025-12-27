using Symbolics.Com.Core.Contract.ExternalServices.Models;

namespace Symbolics.Com.Core.Contract.ExternalServices;

public interface IEmbeddingService
{
    Task<EmbeddingResponse> GenerateEmbedding(string text);
}
