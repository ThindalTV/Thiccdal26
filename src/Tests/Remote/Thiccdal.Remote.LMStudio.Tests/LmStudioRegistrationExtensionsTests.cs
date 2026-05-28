using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.LmStudio;
using Thiccdal.Infrastructure.Questions;

namespace Thiccdal.Remote.LMStudio.Tests;

public sealed class LmStudioRegistrationExtensionsTests
{
    [Fact]
    public void WhenAddingLmStudioIntegration_ThenRegistersClientOptionsAndDetectionService()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"{LmStudioOptions.SectionName}:BaseAddress"] = "http://lmstudio.local:1234/",
                    [$"{LmStudioOptions.SectionName}:RequestTimeoutSeconds"] = "12",
                    [$"{LmStudioQuestionDetectionOptions.SectionName}:Enabled"] = "true",
                    [$"{LmStudioQuestionDetectionOptions.SectionName}:Model"] = "question-model",
                    [$"{LmStudioQuestionDetectionOptions.SectionName}:MaxTokens"] = "6",
                    [$"{LmStudioQuestionDetectionOptions.SectionName}:Temperature"] = "0.1",
                    [$"{LmStudioQuestionDetectionOptions.SectionName}:SystemPrompt"] = "Return JSON only.",
                    [$"{LmStudioQuestionDetectionOptions.SectionName}:UserPromptTemplate"] = "Message: {{message}}"
                })
            .Build();

        services.AddLogging();
        services.AddLmStudioIntegration(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();

        ILmStudioClient clientService = provider.GetRequiredService<ILmStudioClient>();
        IQuestionDetectionService service = provider.GetRequiredService<IQuestionDetectionService>();
        LmStudioOptions lmStudioOptions = provider.GetRequiredService<IOptions<LmStudioOptions>>().Value;
        LmStudioQuestionDetectionOptions detectionOptions = provider.GetRequiredService<IOptions<LmStudioQuestionDetectionOptions>>().Value;
        HttpClient client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(LmStudioClientNames.Default);

        Assert.IsType<LmStudioClient>(clientService);
        Assert.IsType<LmStudioQuestionDetectionService>(service);
        Assert.Equal("http://lmstudio.local:1234/", lmStudioOptions.BaseAddress);
        Assert.Equal("question-model", detectionOptions.Model);
        Assert.Equal(new Uri("http://lmstudio.local:1234/"), client.BaseAddress);
        Assert.Equal(TimeSpan.FromSeconds(12), client.Timeout);
    }

    [Fact]
    public void WhenPromptTemplateOmitsMessagePlaceholder_ThenOptionsValidationFails()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"{LmStudioQuestionDetectionOptions.SectionName}:Enabled"] = "true",
                    [$"{LmStudioQuestionDetectionOptions.SectionName}:Model"] = "question-model",
                    [$"{LmStudioQuestionDetectionOptions.SectionName}:SystemPrompt"] = "Prompt",
                    [$"{LmStudioQuestionDetectionOptions.SectionName}:UserPromptTemplate"] = "No placeholder here."
                })
            .Build();

        services.AddLogging();
        services.AddLmStudioIntegration(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<LmStudioQuestionDetectionOptions>>().Value);
    }
}
