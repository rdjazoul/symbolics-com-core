namespace Symbolics.Com.Core.Contract.ExternalServices;

public interface IEmbeddingService
{
    Task<float[]> GenerateEmbedding(string text);
}
