namespace testapi1.Domain
{
    public class Npc
    {
        public int NpcId { get; set; }
        public string Name { get; set; }
        public string Archetype { get; set; }
        public decimal BaseFriendliness { get; set; }
        public decimal BasePatience { get; set; }
        public decimal BaseCuriosity { get; set; }
        public decimal BaseOpenness { get; set; }
        public decimal BaseConfidence { get; set; }
        public DateTime CreatedAt { get; set; }

        public ICollection<PlayerNpcState> PlayerStates { get; set; }
        public ICollection<Interaction> Interactions { get; set; }
        public ICollection<DialogueTemplate> DialogueTemplates { get; set; }
        public ICollection<LoreDoc> LoreDocs { get; set; }
    }
}
