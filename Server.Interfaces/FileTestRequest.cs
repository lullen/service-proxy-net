
using System;
using System.IO;
using System.Text.Json.Serialization;

namespace Server.Interfaces;

public class FileTestRequest
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    [JsonIgnore]
    public Stream File { get; set; } = Stream.Null;
}
