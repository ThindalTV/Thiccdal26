using System;
using System.Collections.Generic;
using System.Text;

namespace Thiccdal.Infrastructure.Bot.Models;

public enum ChatSource
{
    Twitch = 1,
    Null = 2
}

public record Message
{
    public required ChatSource Source { get; init; }

    public required string Author { get; init; }

    public required string Channel { get; init; }
}

public record ChatMessage : Message
{
    public required string Content { get; init; }
}
