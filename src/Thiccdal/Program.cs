using Microsoft.EntityFrameworkCore;
using Thiccdal.Components;
using Thiccdal.Data;
using Thiccdal.Infrastructure.Twitch;
using Thiccdal.Modules.ChatBot;
using Thiccdal.Modules.Teleprompter;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add DbContext
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add services to the container.
builder.Services.Configure<TwitchOptions>(
    builder.Configuration.GetSection("Twitch"));

builder.Services.AddHttpClient("Twitch");
builder.Services.AddTransient<CancellationTokenSource>();

builder.Services.AddChatBotServices();
builder.Services.AddTeleprompterServices();


builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Run migrations at startup. Antipattern but for dev
/*if (builder.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();
        var awaitingMigrations = await db.Database.GetPendingMigrationsAsync();
        foreach (var migration in awaitingMigrations)
            await db.Database.MigrateAsync(migration);
    }
}*/

// Twitch OAuth callback
app.MapGet("/auth/twitch/callback", async (
    string? code,
    string? state,
    string? error,
    string? error_description,
    ITwitchTokenManager tokenManager,
    ITwitchService twitchService,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    var logger = loggerFactory.CreateLogger("Thiccdal.TwitchCallback");

    if (!string.IsNullOrEmpty(error))
    {
        logger.LogWarning("Twitch OAuth callback returned error: {Error} — {Description}", error, error_description);
        return Results.Redirect("/twitch/connect?error=oauth_denied");
    }

    if (string.IsNullOrEmpty(code))
    {
        logger.LogWarning("Twitch OAuth callback received no code and no error — possible misconfiguration");
        return Results.Redirect("/twitch/connect?error=missing_code");
    }

    if (string.IsNullOrEmpty(state) || !tokenManager.ValidateAndConsumeState(state))
    {
        logger.LogWarning("Twitch OAuth callback state validation failed — possible CSRF attempt (state={State})", state);
        return Results.Redirect("/twitch/connect?error=invalid_state");
    }

    await tokenManager.StoreToken(code, cancellationToken);
    await twitchService.RefreshConnectionState(cancellationToken);
    return Results.Redirect("/twitch/connect");
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
