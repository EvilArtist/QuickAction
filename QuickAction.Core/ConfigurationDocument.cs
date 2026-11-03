using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuickAction.Core;

// One envelope for on-disk settings and portable backups. Assets are embedded only in backups.
internal sealed class ConfigurationDocument
{
    public string Format { get; set; } = "quickaction";
    public int SchemaVersion { get; set; } = 1;
    public Configuration? Configuration { get; set; }
    public Dictionary<string, byte[]> Assets { get; set; } = new();
    internal static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
    internal static ConfigurationDocument Read(string text)
    {
        using var json = JsonDocument.Parse(text);
        if (json.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Invalid configuration document.");
        ConfigurationDocument document;
        if (json.RootElement.EnumerateObject().Any(p => p.Name.Equals("schemaVersion", StringComparison.OrdinalIgnoreCase) || p.Name.Equals("format", StringComparison.OrdinalIgnoreCase)))
        {
            document = JsonSerializer.Deserialize<ConfigurationDocument>(text, Options)!;
            if (document.Format != "quickaction" || document.SchemaVersion != 1 || document.Assets == null)
                throw new InvalidDataException("Unsupported QuickAction schema.");
        }
        else document = new() { Configuration = JsonSerializer.Deserialize<Configuration>(text, Options) };
        if (document.Configuration == null) throw new InvalidDataException("Missing configuration.");
        document.Configuration.Validate();
        return document;
    }
    internal string Serialize() => JsonSerializer.Serialize(this, Options);
}
