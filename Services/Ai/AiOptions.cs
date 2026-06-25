namespace DailyPilot.Services.Ai;

public enum AiProvider
{
    None = 0,
    AzureOpenAI = 1,
    OpenAI = 2
}

/// <summary>
/// AI configuration bound from the "Ai" section (PRD §12: Azure OpenAI + Semantic Kernel).
/// When Provider is None the app uses the built-in heuristic assistant.
/// </summary>
public class AiOptions
{
    public AiProvider Provider { get; set; } = AiProvider.None;

    /// <summary>Azure OpenAI endpoint, e.g. https://my-resource.openai.azure.com/</summary>
    public string? Endpoint { get; set; }

    public string? ApiKey { get; set; }

    /// <summary>Azure: deployment name. OpenAI: model id (e.g. gpt-4o-mini).</summary>
    public string? Model { get; set; }

    public bool IsConfigured =>
        Provider switch
        {
            AiProvider.AzureOpenAI => !string.IsNullOrWhiteSpace(Endpoint)
                                       && !string.IsNullOrWhiteSpace(ApiKey)
                                       && !string.IsNullOrWhiteSpace(Model),
            AiProvider.OpenAI => !string.IsNullOrWhiteSpace(ApiKey)
                                  && !string.IsNullOrWhiteSpace(Model),
            _ => false
        };
}
