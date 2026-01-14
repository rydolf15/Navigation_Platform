using System.Text.Json;
using System.Text.Json.Serialization;

namespace NavigationPlatform.Api.Converters;

/// <summary>
/// JSON converter that ensures nullable DateTime values are treated as UTC.
/// Converts Unspecified DateTime to UTC to avoid PostgreSQL errors.
/// </summary>
public sealed class NullableDateTimeUtcConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;
            
        var value = reader.GetDateTime();
        
        // Convert Unspecified to UTC to avoid PostgreSQL errors
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }
        
        // Ensure UTC before writing
        var utcValue = value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
        
        writer.WriteStringValue(utcValue);
    }
}
