namespace Thiccdal.API.StreamDeck;

/// <summary>
/// Standard response envelope for Stream Deck API endpoints.
/// </summary>
/// <typeparam name="T">The data payload type.</typeparam>
public sealed record StreamDeckResponse<T>
{
    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets the optional response message.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Gets the optional data payload.
    /// </summary>
    public T? Data { get; init; }

    /// <summary>
    /// Gets the optional error message when <see cref="Success"/> is false.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Creates a successful response with data.
    /// </summary>
    public static StreamDeckResponse<T> Ok(T data, string? message = null)
    {
        return new StreamDeckResponse<T>
        {
            Success = true,
            Data = data,
            Message = message
        };
    }

    /// <summary>
    /// Creates a successful response without data.
    /// </summary>
    public static StreamDeckResponse<T> Ok(string? message = null)
    {
        return new StreamDeckResponse<T>
        {
            Success = true,
            Message = message
        };
    }

    /// <summary>
    /// Creates a failure response.
    /// </summary>
    public static StreamDeckResponse<T> Fail(string error)
    {
        return new StreamDeckResponse<T>
        {
            Success = false,
            Error = error
        };
    }
}

/// <summary>
/// Standard response envelope for Stream Deck API endpoints without data payloads.
/// </summary>
public sealed record StreamDeckResponse
{
    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets the optional response message.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Gets the optional error message when <see cref="Success"/> is false.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Creates a successful response.
    /// </summary>
    public static StreamDeckResponse Ok(string? message = null)
    {
        return new StreamDeckResponse
        {
            Success = true,
            Message = message
        };
    }

    /// <summary>
    /// Creates a failure response.
    /// </summary>
    public static StreamDeckResponse Fail(string error)
    {
        return new StreamDeckResponse
        {
            Success = false,
            Error = error
        };
    }
}
