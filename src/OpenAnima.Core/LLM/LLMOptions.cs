using System.ComponentModel.DataAnnotations;

namespace OpenAnima.Core.LLM;

public class LLMOptions
{
    public const string SectionName = "LLM";

    public string Endpoint { get; set; } = "https://api.openai.com/v1";

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gpt-4";

    public int MaxRetries { get; set; } = 3;

    public int TimeoutSeconds { get; set; } = 120;
}
