using System;

namespace testapi1.Services
{
    public sealed class OnnxModelOptions
    {
        public string ModelPath { get; set; } = string.Empty;
        public string[] InputNames { get; set; } = Array.Empty<string>();
        public string[] OutputNames { get; set; } = Array.Empty<string>();
    }
}
