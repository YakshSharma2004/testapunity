namespace testapi1.Domain
{
    public class ProgressionStateAllowedAction
    {
        public string State { get; set; } = string.Empty;
        public int ActionId { get; set; }

        public ActionCatalog Action { get; set; } = default!;
    }
}
