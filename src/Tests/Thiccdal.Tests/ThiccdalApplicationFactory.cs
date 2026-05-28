using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Thiccdal.Tests;

public sealed class ThiccdalApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration(
            static (_, configurationBuilder) =>
            {
                string testDatabasePath = Path.Combine(AppContext.BaseDirectory, "thiccdal-route-rendering.db");
                Dictionary<string, string?> settings = new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = $"Data Source={testDatabasePath}",
                    ["Twitch:ClientId"] = "route-test-client-id",
                    ["Twitch:ClientSecret"] = "route-test-client-secret",
                    ["Twitch:RedirectUri"] = "https://localhost/auth/twitch/callback"
                };

                configurationBuilder.AddInMemoryCollection(settings);
            });
    }
}
