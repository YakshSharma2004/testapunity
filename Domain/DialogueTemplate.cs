namespace testapi1.Domain
{
    public class DialogueTemplate
    {
        public int TemplateId { get; set; }
        public int NpcId { get; set; }
        public int ActionId { get; set; }
        public string ToneTag { get; set; }
        public string TemplateText { get; set; }
        public bool IsActive { get; set; }

        public Npc Npc { get; set; }
        public ActionCatalog Action { get; set; }
    }
}
