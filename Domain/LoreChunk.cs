namespace testapi1.Domain
{
    public class LoreChunk
    {
        public int ChunkId { get; set; }
        public int DocId { get; set; }
        public string ChunkKey { get; set; }
        public int ChunkOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime UpdatedAt { get; set; }
        public string ChunkText { get; set; }
        public byte[] Embedding { get; set; }  // optional/external store

        public LoreDoc Doc { get; set; }
    }

}
