namespace App.Features.AI.Services;

public sealed class AiFeatureException : Exception
{
    public int StatusCode { get; }

    public AiFeatureException(string detail, int statusCode, Exception? innerException = null)
        : base(detail, innerException)
    {
        StatusCode = statusCode;
    }
}
