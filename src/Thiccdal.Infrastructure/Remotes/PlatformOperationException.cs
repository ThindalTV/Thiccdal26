namespace Thiccdal.Infrastructure.Remotes;

public sealed class PlatformOperationException : Exception
{
    public PlatformOperationException(string message)
        : base(message)
    {
    }

    public PlatformOperationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
