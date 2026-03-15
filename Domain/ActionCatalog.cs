namespace testapi1.Domain
{
    public class ActionCatalog
    {
        public int ActionId { get; set; }
        public string Code { get; set; }
        public string IntentTag { get; set; }
        public string Description { get; set; }

        public ICollection<Interaction> Interactions { get; set; }
        public ICollection<DialogueTemplate> DialogueTemplates { get; set; }
    }
}
