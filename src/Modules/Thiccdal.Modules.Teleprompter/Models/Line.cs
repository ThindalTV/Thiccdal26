namespace Thiccdal.Modules.Teleprompter.Models;

public record Line(string Sender, string Content, string HtmlContent, string Platform, DateTime Timestamp);
