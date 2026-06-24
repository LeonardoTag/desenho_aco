using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CapitalAco.DrawingMacro.App.Models
{
    public class SegmentoConverter : JsonConverter<Segmento>
    {
        public override Segmento? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException("Expected StartArray for Segmento");
            }

            var segmento = new Segmento();

            // Read index 0: Direcao (string)
            reader.Read();
            if (reader.TokenType != JsonTokenType.String) throw new JsonException("Expected string for Direcao at index 0");
            segmento.Direcao = reader.GetString() ?? "E";

            // Read index 1: Angulo (double)
            reader.Read();
            if (reader.TokenType != JsonTokenType.Number) throw new JsonException("Expected number for Angulo at index 1");
            segmento.Angulo = reader.GetDouble();

            // Read index 2: Medida (double)
            reader.Read();
            if (reader.TokenType != JsonTokenType.Number) throw new JsonException("Expected number for Medida at index 2");
            segmento.Medida = reader.GetDouble();

            // Read index 3: TipoMedida (string)
            reader.Read();
            if (reader.TokenType != JsonTokenType.String) throw new JsonException("Expected string for TipoMedida at index 3");
            segmento.TipoMedida = reader.GetString() ?? "e";

            // Check if there are more elements
            reader.Read();
            if (reader.TokenType != JsonTokenType.EndArray)
            {
                // Read index 4: EhCurvo (bool)
                if (reader.TokenType == JsonTokenType.True || reader.TokenType == JsonTokenType.False)
                {
                    segmento.EhCurvo = reader.GetBoolean();
                }
                else
                {
                    throw new JsonException("Expected boolean for EhCurvo at index 4");
                }

                reader.Read();
                if (reader.TokenType != JsonTokenType.EndArray)
                {
                    // Read index 5: CurvaInfo (object)
                    if (reader.TokenType == JsonTokenType.StartObject)
                    {
                        segmento.CurvaInfo = JsonSerializer.Deserialize<Segmento.InformacaoCurva>(ref reader, options);
                    }
                    else
                    {
                        // Skip if it's null or other
                        reader.Skip();
                    }

                    // Move to EndArray
                    reader.Read();
                }
            }

            if (reader.TokenType != JsonTokenType.EndArray)
            {
                throw new JsonException("Expected EndArray for Segmento");
            }

            return segmento;
        }

        public override void Write(Utf8JsonWriter writer, Segmento value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            writer.WriteStringValue(value.Direcao);
            writer.WriteNumberValue(value.Angulo);
            writer.WriteNumberValue(value.Medida);
            writer.WriteStringValue(value.TipoMedida);

            if (value.EhCurvo)
            {
                writer.WriteBooleanValue(value.EhCurvo);
                if (value.CurvaInfo != null)
                {
                    JsonSerializer.Serialize(writer, value.CurvaInfo, options);
                }
                else
                {
                    writer.WriteNullValue();
                }
            }
            writer.WriteEndArray();
        }
    }
}
