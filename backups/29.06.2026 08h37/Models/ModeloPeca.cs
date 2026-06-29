using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CapitalAco.DrawingMacro.App.Models
{
    public class ModeloPeca
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("nome")]
        public string Nome { get; set; } = string.Empty;

        [JsonPropertyName("descricao")]
        public string Descricao { get; set; } = string.Empty;

        [JsonPropertyName("chapa")]
        public string Chapa { get; set; } = string.Empty;

        [JsonPropertyName("comprimento")]
        public double? Comprimento { get; set; }

        [JsonPropertyName("segmentos")]
        public List<Segmento> Segmentos { get; set; } = new();

        [JsonPropertyName("criado_em")]
        public DateTime CriadoEm { get; set; }

        [JsonPropertyName("atualizado_em")]
        public DateTime AtualizadoEm { get; set; }
    }
}
