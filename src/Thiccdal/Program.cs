using Thiccdal;
using Thiccdal.API.Status;
using Thiccdal.API.Restream;
using Thiccdal.API.StreamDeck;
using Thiccdal.AI;
using Thiccdal.Components;
using Thiccdal.Data;
using Thiccdal.HealthChecks;
using Thiccdal.Infrastructure.Operators;
using Thiccdal.Infrastructure.Questions;
using Thiccdal.Infrastructure.Streaming;
using Thiccdal.Infrastructure.Teleprompter;
using Thiccdal.Modules.ChatBot;
using Thiccdal.Modules.Overlay;
using Thiccdal.Modules.Teleprompter;
using Thiccdal.Modules.Control.Services;
using Thiccdal.Remote.Discord;
using Thiccdal.Remote.LinkedIn;
using Thiccdal.Remote.TikTok;
using Thiccdal.Remote.Twitch;
using Thiccdal.Remote.YouTube;
using Thiccdal.Streaming;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddThiccdalData(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddCheck<ApplicationDbContextHealthCheck>("sqlite", tags: ["ready"]);
builder.Services.AddAiIntegration(builder.Configuration);
builder.Services.AddStatusApi();
builder.Services.AddHttpClient();

// Add services to the container.
builder.Services.AddChatBotServices(builder.Configuration);
builder.Services.AddTwitchIntegration(builder.Configuration);
builder.Services.AddYouTubeIntegration(builder.Configuration);
builder.Services.AddDiscordIntegration(builder.Configuration);
builder.Services.AddLinkedInIntegration(builder.Configuration);
builder.Services.AddTikTokIntegration(builder.Configuration);
builder.Services.Configure<PrompterOptions>(builder.Configuration.GetSection(PrompterOptions.SectionName));
builder.Services.AddTeleprompterServices();
builder.Services.AddOverlay();
builder.Services.Configure<StreamingOptions>(builder.Configuration.GetSection(StreamingOptions.SectionName));
builder.Services.Configure<RtmpServerOptions>(builder.Configuration.GetSection(RtmpServerOptions.SectionName));
builder.Services.AddStreamingServices();
builder.Services.AddSingleton<IRecordingStorageProbe, RecordingStorageProbe>();
builder.Services.AddScoped<IRestreamControlClient, RestreamControlClient>();
builder.Services.AddSingleton<IOperatorStateService, OperatorStateService>();
builder.Services.AddSingleton<QuestionOverlayService>();
builder.Services.AddSingleton<IQuestionOverlayService>(static serviceProvider => serviceProvider.GetRequiredService<QuestionOverlayService>());
builder.Services.AddSingleton<PreLiveChecklistService>();
builder.Services.AddSingleton<IPreLiveChecklistService>(static serviceProvider => serviceProvider.GetRequiredService<PreLiveChecklistService>());
builder.Services.AddSingleton<IGoLiveActionService, GoLiveActionService>();
builder.Services.AddHostedService(static serviceProvider => serviceProvider.GetRequiredService<PreLiveChecklistService>());

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

await app.Services.InitializeDatabase(app.Lifetime.ApplicationStopping);
app.MapDefaultEndpoints();
app.MapStatusEndpoints();
app.MapRestreamEndpoints();
app.MapStreamDeckEndpoints();
app.MapTwitchEndpoints();
app.MapYouTubeEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseCors();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(RouteAssemblyCatalog.AdditionalAssemblies.ToArray())
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program
{
}