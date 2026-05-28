using System.Reflection;

namespace Thiccdal;

internal static class RouteAssemblyCatalog
{
    private static readonly Assembly[] _additionalAssemblies =
    [
        typeof(Thiccdal.Modules.Overlay.OverlayRegistrationExtension).Assembly,
        typeof(Thiccdal.Modules.ChatBot.ChatBotRegistrationExtension).Assembly,
        typeof(Thiccdal.Modules.Control.ControlRegistrationExtension).Assembly,
        typeof(Thiccdal.Modules.Teleprompter.TeleprompterRegistrationExtension).Assembly
    ];

    public static IReadOnlyList<Assembly> AdditionalAssemblies => _additionalAssemblies;
}
