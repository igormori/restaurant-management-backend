using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RestaurantManagement.Shared.Models
{
    /// <summary>
    /// Required rule for an ID that is a non-nullable <see cref="Guid"/>: the empty ID counts as missing,
    /// which is what a missing, null or malformed value is read as (see <see cref="LenientGuidConverter"/>).
    /// </summary>
    public sealed class RequiredIdAttribute : RequiredAttribute
    {
        public override bool IsValid(object? value) => value is Guid id ? id != Guid.Empty : base.IsValid(value);
    }

    /// <summary>
    /// Reads null, non-text and text that is not an ID as the empty ID, so the required rule answers
    /// in the client's language instead of the platform's English read error.
    /// </summary>
    public sealed class LenientGuidConverter : JsonConverter<Guid>
    {
        public override bool HandleNull => true;

        public override Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String && reader.TryGetGuid(out var id))
                return id;

            reader.Skip();
            return Guid.Empty;
        }

        public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options)
            => writer.WriteStringValue(value);
    }
}
