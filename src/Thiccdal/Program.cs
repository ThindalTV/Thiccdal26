using Microsoft.EntityFrameworkCore;
using Thiccdal.Components;
using Thiccdal.Data;
using Thiccdal.Infrastructure.Twitch;
using Thiccdal.Remote.Twitch;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add DbContext
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add services to the container.
builder.Services.Configure<TwitchOptions>(
    builder.Configuration.GetSection("Twitch"));

builder.Services.AddHttpClient("Twitch");

builder.Services.AddScoped<ITwitchTokenManager, TwitchTokenManager>();
builder.Services.AddScoped<ITwitchService, TwitchService>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Twitch OAuth callback
app.MapGet("/auth/twitch/callback", async (
    string code,
    ITwitchTokenManager tokenManager,
    CancellationToken cancellationToken) =>
{
    await tokenManager.StoreToken(code, cancellationToken);
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
