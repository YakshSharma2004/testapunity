namespace testapi1.ApiContracts
{
    public class IntentRequest
    {

        //this needs more things added to it later like the npc id and the context reference etc.
        public string Text { get; set; } = "";
        public string NpcId { get; set; } = "";
        public string ContextKey { get; set; } = "";

    }
}
