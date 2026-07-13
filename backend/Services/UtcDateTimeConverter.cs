using System.Text.Json;
using System.Text.Json.Serialization;

namespace backend.Services;

/// <summary>
/// Forces DateTime to serialize as UTC ISO 8601 with "Z" suffix,
/// even when the Kind is Unspecified (which happens with SQLite reads).
/// </summary>
public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return DateTime.Parse(reader.GetString() ?? "").ToUniversalTime();
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"));
    }
}
