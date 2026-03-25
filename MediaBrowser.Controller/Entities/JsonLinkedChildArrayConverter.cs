using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MediaBrowser.Controller.Entities;

/// <summary>
/// Converts a LinkedChild array that may contain legacy string paths
/// (from older database entries) into LinkedChild objects.
/// </summary>
public class JsonLinkedChildArrayConverter : JsonConverter<LinkedChild[]>
{
    /// <inheritdoc />
    public override bool HandleNull => true;

    /// <inheritdoc />
    public override LinkedChild[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return Array.Empty<LinkedChild>();
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return Array.Empty<LinkedChild>();
        }

        var items = new List<LinkedChild>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                break;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                // Legacy format: plain string path
                var path = reader.GetString();
                if (!string.IsNullOrEmpty(path))
                {
                    items.Add(new LinkedChild { Path = path });
                }
            }
            else if (reader.TokenType == JsonTokenType.StartObject)
            {
                // New format: LinkedChild object
                var child = JsonSerializer.Deserialize<LinkedChild>(ref reader, options);
                if (child is not null)
                {
                    items.Add(child);
                }
            }
            else
            {
                reader.Skip();
            }
        }

        return items.ToArray();
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, LinkedChild[] value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var child in value)
        {
            JsonSerializer.Serialize(writer, child, options);
        }

        writer.WriteEndArray();
    }
}
