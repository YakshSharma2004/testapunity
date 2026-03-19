using System;
using System.Collections.Generic;

namespace testapi1.Services.Embeddings
{
    public sealed class EmbeddingsOptions
    {
        public string Model { get; set; } = "mpnet";
        public Dictionary<string, EmbeddingModelOptions> Models { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class EmbeddingModelOptions
    {
        public string ModelPath { get; set; } = string.Empty;
        public string TokenizerPath { get; set; } = string.Empty;
        public int MaxLen { get; set; } = 384;
    }
}
