using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Thiccdal.Infrastructure.Bot;
using Thiccdal.Infrastructure.Operators;
using Thiccdal.Infrastructure.Overlay;
using Thiccdal.Infrastructure.Questions;
using Thiccdal.Infrastructure.Streaming;
using Thiccdal.Infrastructure.Teleprompter;

#pragma warning disable CA1724 // Type names should not match namespaces

namespace Thiccdal.API.StreamDeck;

/// <summary>
/// Registers the Stream Deck API endpoints optimized for Stream Deck integration.
/// All POST endpoints accept empty bodies for Stream Deck compatibility.
/// </summary>
public static class StreamDeckApiExtensions
{
    /// <summary>
    /// Maps all Stream Deck API endpoints.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The updated endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapStreamDeckEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/api/streamdeck")
            .WithTags("StreamDeck");

        MapStreamingEndpoints(group);
        MapRestreamEndpoints(group);
        MapTeleprompterEndpoints(group);
        MapOverlayEndpoints(group);
        MapQuestionsEndpoints(group);
        MapChatEndpoints(group);
        MapOperatorEndpoints(group);

        return endpoints;
    }

    private static void MapStreamingEndpoints(RouteGroupBuilder parent)
    {
        RouteGroupBuilder group = parent.MapGroup("/streaming");

        group.MapGet(
                "/status",
                static (IStreamingService streamingService) =>
                {
                    StreamingStatusData data = new StreamingStatusData(
                        streamingService.IsRunning,
                        streamingService.State.ToString());
                    return Results.Ok(StreamDeckResponse<StreamingStatusData>.Ok(data));
                })
            .WithName("StreamDeck_GetStreamingStatus")
            .Produces<StreamDeckResponse<StreamingStatusData>>(StatusCodes.Status200OK);

        group.MapPost(
                "/go-live",
                static async (IGoLiveActionService goLiveService, CancellationToken cancellationToken) =>
                {
                    try
                    {
                        await goLiveService.Execute(cancellationToken);
                        return Results.Ok(StreamDeckResponse.Ok("Go-live workflow executed"));
                    }
                    catch (Exception ex)
                    {
                        return Results.Ok(StreamDeckResponse.Fail(ex.Message));
                    }
                })
            .WithName("StreamDeck_GoLive")
            .Produces<StreamDeckResponse>(StatusCodes.Status200OK);

        group.MapPost(
                "/stop",
                static async (IStreamingService streamingService, CancellationToken cancellationToken) =>
                {
                    try
                    {
                        await streamingService.Stop(cancellationToken);
                        return Results.Ok(StreamDeckResponse.Ok("Streaming stopped"));
                    }
                    catch (Exception ex)
                    {
                        return Results.Ok(StreamDeckResponse.Fail(ex.Message));
                    }
                })
            .WithName("StreamDeck_StopStreaming")
            .Produces<StreamDeckResponse>(StatusCodes.Status200OK);
    }

    private static void MapRestreamEndpoints(RouteGroupBuilder parent)
    {
        RouteGroupBuilder group = parent.MapGroup("/restream");

        group.MapGet(
                "/status",
                static async (IRestreamRuntimeService restreamService, CancellationToken cancellationToken) =>
                {
                    RestreamControlState state = await restreamService.GetState(cancellationToken);
                    return Results.Ok(StreamDeckResponse<RestreamControlState>.Ok(state));
                })
            .WithName("StreamDeck_GetRestreamStatus")
            .Produces<StreamDeckResponse<RestreamControlState>>(StatusCodes.Status200OK);

        group.MapPost(
                "/start",
                static async (IRestreamRuntimeService restreamService, CancellationToken cancellationToken) =>
                {
                    try
                    {
                        await restreamService.Start(cancellationToken);
                        return Results.Ok(StreamDeckResponse.Ok("Restream started"));
                    }
                    catch (Exception ex)
                    {
                        return Results.Ok(StreamDeckResponse.Fail(ex.Message));
                    }
                })
            .WithName("StreamDeck_StartRestream")
            .Produces<StreamDeckResponse>(StatusCodes.Status200OK);

        group.MapPost(
                "/stop",
                static async (IRestreamRuntimeService restreamService, CancellationToken cancellationToken) =>
                {
                    try
                    {
                        await restreamService.Stop(cancellationToken);
                        return Results.Ok(StreamDeckResponse.Ok("Restream stopped"));
                    }
                    catch (Exception ex)
                    {
                        return Results.Ok(StreamDeckResponse.Fail(ex.Message));
                    }
                })
            .WithName("StreamDeck_StopRestream")
            .Produces<StreamDeckResponse>(StatusCodes.Status200OK);

        group.MapPost(
                "/destinations/{platform}/enable",
                static async (
                    string platform,
                    IRestreamRuntimeService restreamService,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        RestreamDestinationUpdateRequest request = new RestreamDestinationUpdateRequest
                        {
                            PlatformName = platform,
                            IsEnabled = true
                        };
                        await restreamService.UpdateDestination(request, cancellationToken);
                        return Results.Ok(StreamDeckResponse.Ok($"{platform} enabled"));
                    }
                    catch (Exception ex)
                    {
                        return Results.Ok(StreamDeckResponse.Fail(ex.Message));
                    }
                })
            .WithName("StreamDeck_EnableDestination")
            .Produces<StreamDeckResponse>(StatusCodes.Status200OK);

        group.MapPost(
                "/destinations/{platform}/disable",
                static async (
                    string platform,
                    IRestreamRuntimeService restreamService,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        RestreamDestinationUpdateRequest request = new RestreamDestinationUpdateRequest
                        {
                            PlatformName = platform,
                            IsEnabled = false
                        };
                        await restreamService.UpdateDestination(request, cancellationToken);
                        return Results.Ok(StreamDeckResponse.Ok($"{platform} disabled"));
                    }
                    catch (Exception ex)
                    {
                        return Results.Ok(StreamDeckResponse.Fail(ex.Message));
                    }
                })
            .WithName("StreamDeck_DisableDestination")
            .Produces<StreamDeckResponse>(StatusCodes.Status200OK);

        group.MapPost(
                "/destinations/{platform}/toggle",
                static async (
                    string platform,
                    IRestreamRuntimeService restreamService,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        RestreamControlState state = await restreamService.GetState(cancellationToken);
                        RestreamDestinationState? destination = state.Destinations
                            .FirstOrDefault(d => string.Equals(d.PlatformName, platform, StringComparison.OrdinalIgnoreCase));

                        if (destination is null)
                        {
                            return Results.Ok(StreamDeckResponse.Fail($"Platform '{platform}' not found"));
                        }

                        RestreamDestinationUpdateRequest request = new RestreamDestinationUpdateRequest
                        {
                            PlatformName = platform,
                            IsEnabled = !destination.IsEnabled
                        };
                        await restreamService.UpdateDestination(request, cancellationToken);

                        string newState = request.IsEnabled == true ? "enabled" : "disabled";
                        return Results.Ok(StreamDeckResponse.Ok($"{platform} {newState}"));
                    }
                    catch (Exception ex)
                    {
                        return Results.Ok(StreamDeckResponse.Fail(ex.Message));
                    }
                })
            .WithName("StreamDeck_ToggleDestination")
            .Produces<StreamDeckResponse>(StatusCodes.Status200OK);
    }

    private static void MapTeleprompterEndpoints(RouteGroupBuilder parent)
    {
        RouteGroupBuilder group = parent.MapGroup("/teleprompter");

        group.MapPost(
                "/scroll/up",
                static (IOperatorStateService operatorState) =>
                {
                    operatorState.ScrollTeleprompter(ScrollDirection.Up);
                    return Results.Ok(StreamDeckResponse.Ok("Scrolled up"));
                })
            .WithName("StreamDeck_TeleprompterScrollUp")
            .Produces<StreamDeckResponse>(StatusCodes.Status200OK);

        group.MapPost(
                "/scroll/down",
                static (IOperatorStateService operatorState) =>
                {
                    operatorState.ScrollTeleprompter(ScrollDirection.Down);
                    return Results.Ok(StreamDeckResponse.Ok("Scrolled down"));
                })
            .WithName("StreamDeck_TeleprompterScrollDown")
            .Produces<StreamDeckResponse>(StatusCodes.Status200OK);
    }

    private static void MapOverlayEndpoints(RouteGroupBuilder parent)
    {
        RouteGroupBuilder group = parent.MapGroup("/overlay");

        group.MapGet(
                "/components",
                static (IOverlayService overlayService) =>
                {
                    IReadOnlyList<IOverlayComponent> components = overlayService.GetComponents();
                    IReadOnlyList<string> names = components.Select(c => c.ComponentName).ToList();
                    return Results.Ok(StreamDeckResponse<IReadOnlyList<string>>.Ok(names));
                })
            .WithName("StreamDeck_GetOverlayComponents")
            .Produces<StreamDeckResponse<IReadOnlyList<string>>>(StatusCodes.Status200OK);

        group.MapPost(
                "/{componentName}/test",
                static (string componentName, IOperatorStateService operatorState) =>
                {
                    operatorState.TriggerOverlayTest(componentName);
                    return Results.Ok(StreamDeckResponse.Ok($"Triggered test for {componentName}"));
                })
            .WithName("StreamDeck_TestOverlayComponent")
            .Produces<StreamDeckResponse>(StatusCodes.Status200OK);
    }

    private static void MapQuestionsEndpoints(RouteGroupBuilder parent)
    {
        RouteGroupBuilder group = parent.MapGroup("/questions");

        group.MapGet(
                string.Empty,
                static (IQuestionOverlayService questionService) =>
                {
                    QuestionDashboardState state = questionService.GetState();
                    return Results.Ok(StreamDeckResponse<QuestionDashboardState>.Ok(state));
                })
            .WithName("StreamDeck_GetQuestions")
            .Produces<StreamDeckResponse<QuestionDashboardState>>(StatusCodes.Status200OK);

        group.MapPost(
                "/next",
                static (IQuestionOverlayService questionService) =>
                {
                    bool promoted = questionService.TryPromoteSelectedQuestion();
                    if (promoted)
                    {
                        return Results.Ok(StreamDeckResponse.Ok("Next question promoted"));
                    }
                    return Results.Ok(StreamDeckResponse.Fail("No question to promote"));
                })
            .WithName("StreamDeck_NextQuestion")
            .Produces<StreamDeckResponse>(StatusCodes.Status200OK);

        group.MapPost(
                "/dismiss",
                static (IQuestionOverlayService questionService) =>
                {
                    bool dismissed = questionService.TryDismissLiveQuestion();
                    if (dismissed)
                    {
                        return Results.Ok(StreamDeckResponse.Ok("Live question dismissed"));
                    }
                    return Results.Ok(StreamDeckResponse.Fail("No live question to dismiss"));
                })
            .WithName("StreamDeck_DismissQuestion")
            .Produces<StreamDeckResponse>(StatusCodes.Status200OK);

        group.MapPost(
                "/clear",
                static (IQuestionOverlayService questionService) =>
                {
                    questionService.ClearWaitingQuestions();
                    return Results.Ok(StreamDeckResponse.Ok("Waiting questions cleared"));
                })
            .WithName("StreamDeck_ClearQuestions")
            .Produces<StreamDeckResponse>(StatusCodes.Status200OK);

        group.MapPost(
                "/autodetect/enable",
                static (IQuestionOverlayService questionService) =>
                {
                    questionService.SetAutoDetect(true);
                    return Results.Ok(StreamDeckResponse.Ok("Auto-detect enabled"));
                })
            .WithName("StreamDeck_EnableAutoDetect")
            .Produces<StreamDeckResponse>(StatusCodes.Status200OK);

        group.MapPost(
                "/autodetect/disable",
                static (IQuestionOverlayService questionService) =>
                {
                    questionService.SetAutoDetect(false);
                    return Results.Ok(StreamDeckResponse.Ok("Auto-detect disabled"));
                })
            .WithName("StreamDeck_DisableAutoDetect")
            .Produces<StreamDeckResponse>(StatusCodes.Status200OK);

        group.MapPost(
                "/autodetect/toggle",
                static (IQuestionOverlayService questionService) =>
                {
                    QuestionDashboardState state = questionService.GetState();
                    bool newState = !state.AutoDetectEnabled;
                    questionService.SetAutoDetect(newState);
                    string message = newState ? "Auto-detect enabled" : "Auto-detect disabled";
                    return Results.Ok(StreamDeckResponse.Ok(message));
                })
            .WithName("StreamDeck_ToggleAutoDetect")
            .Produces<StreamDeckResponse>(StatusCodes.Status200OK);
    }

    private static void MapChatEndpoints(RouteGroupBuilder parent)
    {
        RouteGroupBuilder group = parent.MapGroup("/chat");

        group.MapPost(
                "/send",
                static async (
                    string message,
                    IChatService chatService,
                    CancellationToken cancellationToken) =>
                {
                    if (string.IsNullOrWhiteSpace(message))
                    {
                        return Results.Ok(StreamDeckResponse.Fail("Message cannot be empty"));
                    }

                    try
                    {
                        await chatService.SendMessage(message, cancellationToken);
                        return Results.Ok(StreamDeckResponse.Ok("Message sent"));
                    }
                    catch (Exception ex)
                    {
                        return Results.Ok(StreamDeckResponse.Fail(ex.Message));
                    }
                })
            .WithName("StreamDeck_SendChat")
            .Produces<StreamDeckResponse>(StatusCodes.Status200OK);
    }

    private static void MapOperatorEndpoints(RouteGroupBuilder parent)
    {
        RouteGroupBuilder group = parent.MapGroup("/operator");

        group.MapGet(
                "/mode",
                static (IOperatorStateService operatorState) =>
                {
                    OperatorModeData data = new OperatorModeData(operatorState.Mode.ToString());
                    return Results.Ok(StreamDeckResponse<OperatorModeData>.Ok(data));
                })
            .WithName("StreamDeck_GetOperatorMode")
            .Produces<StreamDeckResponse<OperatorModeData>>(StatusCodes.Status200OK);

        group.MapPost(
                "/mode/prelive",
                static (IOperatorStateService operatorState) =>
                {
                    operatorState.SetMode(OperatorMode.PreLive);
                    return Results.Ok(StreamDeckResponse.Ok("Mode set to PreLive"));
                })
            .WithName("StreamDeck_SetPreLiveMode")
            .Produces<StreamDeckResponse>(StatusCodes.Status200OK);

        group.MapPost(
                "/mode/live",
                static (IOperatorStateService operatorState) =>
                {
                    operatorState.SetMode(OperatorMode.Live);
                    return Results.Ok(StreamDeckResponse.Ok("Mode set to Live"));
                })
            .WithName("StreamDeck_SetLiveMode")
            .Produces<StreamDeckResponse>(StatusCodes.Status200OK);
    }
}

/// <summary>
/// Streaming status data payload.
/// </summary>
public sealed record StreamingStatusData(bool IsRunning, string State);

/// <summary>
/// Operator mode data payload.
/// </summary>
public sealed record OperatorModeData(string Mode);
