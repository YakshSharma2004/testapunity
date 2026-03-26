namespace testapi1.Domain
{
    public class PlayerNpcState
    {
        public int PlayerId { get; set; }
        public int NpcId { get; set; }
        public decimal Trust { get; set; }
        public decimal Patience { get; set; }
        public decimal Curiosity { get; set; }
        public decimal Openness { get; set; }
        public string Memory { get; set; }  // short notes
        public DateTime LastInteractionAt { get; set; }

        public Player Player { get; set; }
        public Npc Npc { get; set; }
    }
}
