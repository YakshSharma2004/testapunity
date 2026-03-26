namespace testapi1.Domain
{
    public class LoreDoc
    {
        public int DocId { get; set; }
        public int? NpcId { get; set; }  // nullable - global lore allowed
        public string DocKey { get; set; }
        public int Version { get; set; } = 1;
        public bool IsActive { get; set; } = true;
        public string Title { get; set; }
        public string Body { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Npc Npc { get; set; }
        public ICollection<LoreChunk> Chunks { get; set; }
    }
}
