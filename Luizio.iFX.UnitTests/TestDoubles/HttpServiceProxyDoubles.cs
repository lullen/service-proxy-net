using System.Text.Json.Serialization;

namespace Luizio.iFX.UnitTests.TestDoubles;

public class StreamRequest
{
    [JsonIgnore]
    public Stream? File { get; set; }
    public string FileName { get; set; } = string.Empty;
}
