namespace testapi1.Domain
{
    public class Interaction
    {
        public long InteractionId { get; set; }
        public int PlayerId { get; set; }
        public int NpcId { get; set; }
        public DateTime OccurredAt { get; set; }
        public string Location { get; set; }
        public string PlayerAction { get; set; }
        public string PlayerText { get; set; }
        public string NluTopIntent { get; set; }
        public decimal Sentiment { get; set; }  // -1..1
        public decimal Friendliness { get; set; }
        public string ToneTag { get; set; }
        public bool NsfwFlag { get; set; }
        public int? ChosenActionId { get; set; }
        public string ResponseText { get; set; }
        public string ResponseSource { get; set; }  // TEMPLATE | LOCAL_LLM | CLOUD_LLM
        public string ModelVersion { get; set; }
        public decimal RewardScore { get; set; }
        public string OutcomeFlags { get; set; }

        public Player Player { get; set; }
        public Npc Npc { get; set; }
        public ActionCatalog ChosenAction { get; set; }
    }
}
