using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AiAnaliz;

public class ParsedSectionItem
{
    public string MaddeNo {get; set;} = string.Empty;
    public string AnaBaslik {get; set;} = string.Empty;
    public string AltBaslik {get; set;} = string.Empty;
    public string Content {get; set;} = string.Empty;
}