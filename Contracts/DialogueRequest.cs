namespace testapi1.Contracts
{
    public class DialogueRequest
    {
        public string playerId { get; set; } = "";
        public string npcId { get; set; } = "";
        public string inGameTime { get; set; } = "";
        public string playerText { get; set; } = "";
    }

}