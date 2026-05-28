using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.AI;
using Thiccdal.Infrastructure.Questions;

namespace Thiccdal.AI.Tests;

public sealed class AiRegistrationExtensionsTests
{
    [Fact]
    public void WhenAddingAiIntegration_ThenRegistersClientOptionsAndDetectionService()
    {
        ServiceCollection services = new ServiceCollection();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"{OpenAiOptions.SectionName}:Endpoint"] = "http://lmstudio.local:1234/v1",
                    [$"{OpenAiOptions.SectionName}:RequestTimeoutSeconds"] = "12",
                    [$"{QuestionDetectionOptions.SectionName}:Enabled"] = "true",
                    [$"{QuestionDetectionOptions.SectionName}:Model"] = "question-model",
                    [$"{QuestionDetectionOptions.SectionName}:MaxOutputTokenCount"] = "6",
                    [$"{QuestionDetectionOptions.SectionName}:Temperature"] = "0.1",
                    [$"{QuestionDetectionOptions.SectionName}:SystemPrompt"] = "Return JSON only.",
                    [$"{QuestionDetectionOptions.SectionName}:UserPromptTemplate"] = "Message: {{message}}"
                })
            .Build();

        services.AddLogging();
        services.AddAiIntegration(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();

        IChatCompletionClient clientService = provider.GetRequiredService<IChatCompletionClient>();
        IQuestionDetectionService detectionService = provider.GetRequiredService<IQuestionDetectionService>();
        OpenAiOptions openAiOptions = provider.GetRequiredService<IOptions<OpenAiOptions>>().Value;
        QuestionDetectionOptions detectionOptions = provider.GetRequiredService<IOptions<QuestionDetectionOptions>>().Value;

        Assert.IsType<OpenAiCompatibleChatClient>(clientService);
        Assert.IsType<OpenAiQuestionDetectionService>(detectionService);
        Assert.Equal("http://lmstudio.local:1234/v1", openAiOptions.Endpoint);
        Assert.Equal("question-model", detectionOptions.Model);
    }

    [Fact]
    public void WhenPromptTemplateOmitsMessagePlaceholder_ThenOptionsValidationFails()
    {
        ServiceCollection services = new ServiceCollection();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"{QuestionDetectionOptions.SectionName}:Enabled"] = "true",
                    [$"{QuestionDetectionOptions.SectionName}:Model"] = "question-model",
                    [$"{QuestionDetectionOptions.SectionName}:SystemPrompt"] = "Prompt",
                    [$"{QuestionDetectionOptions.SectionName}:UserPromptTemplate"] = "No placeholder here."
                })
            .Build();

        services.AddLogging();
        services.AddAiIntegration(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<QuestionDetectionOptions>>().Value);
    }
}
