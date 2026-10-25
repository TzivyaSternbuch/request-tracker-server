using System.ComponentModel.DataAnnotations;

namespace RequestTracker.IntegrationTests.TestHost;

// init properties, not a positional record: with a positional record, model-binding errors for this
// property came back keyed "SomeValue" instead of the camelCase "someValue".
public sealed record TestPayload
{
    [Required]
    public string? SomeValue { get; init; }

    public TestKind Kind { get; init; }
}
