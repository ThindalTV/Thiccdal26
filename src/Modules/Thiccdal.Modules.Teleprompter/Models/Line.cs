namespace Thiccdal.Modules.Teleprompter.Models;

public record Line(string DukaSender, string Content, string HtmlContent, string Platform, DateTime Timestamp);
