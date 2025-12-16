using Prism.Core.Enums;

namespace Prism.Core.Models;

public class AppUsage
{
    public string ProcessName { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public AppCategory Category { get; set; }
    public DateTime Date { get; set; }
}
