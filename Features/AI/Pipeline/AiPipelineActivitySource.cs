using System.Diagnostics;

namespace App.Features.AI.Pipeline;

public static class AiPipelineActivitySource
{
    public const string Name = "NoMoreReceipts.AI.Pipeline";

    public static readonly ActivitySource Instance = new(Name, "1.0.0");
}
