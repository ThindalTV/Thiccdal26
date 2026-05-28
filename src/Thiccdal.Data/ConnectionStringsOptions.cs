namespace Thiccdal.Data;

public class ConnectionStringsOptions
{
    public const string SectionName = "ConnectionStrings";

    public string DefaultConnection { get; set; } = "Data Source=thiccdal.db";
}
