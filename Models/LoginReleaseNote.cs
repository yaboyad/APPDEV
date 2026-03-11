using System.Collections.Generic;

namespace Label_CRM_demo.Models;

public sealed class LoginReleaseNote
{
    public string Version { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string PublishedOn { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public List<string>? Highlights { get; init; } = new();
}
