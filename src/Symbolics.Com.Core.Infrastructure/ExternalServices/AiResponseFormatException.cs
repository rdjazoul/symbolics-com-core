namespace Symbolics.Com.Core.Infrastructure.ExternalServices;

public sealed class AiResponseFormatException : Exception
{
    public AiResponseFormatException(string message) : base(message)
    {
    }

    public AiResponseFormatException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
