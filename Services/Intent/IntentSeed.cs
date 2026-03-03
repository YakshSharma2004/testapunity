namespace testapi1.Services.Intent
{
    public sealed record IntentExample(string Intent, string Text);

    public static class IntentSeed
    {
        public static readonly IntentExample[] Examples =
        {
            new("GREET", "hello"),
            //new("GREET", "hey there"),
            //new("ASK_WHERE", "where is john"),
            //new("ASK_WHERE", "do you know where the lighthouse is"),
            //new("ACCUSE", "i think you killed him"),
            //new("ACCUSE", "you are lying, you did it"),
            //new("BUY", "show me what you sell"),
            //new("BUY", "i want to buy something"),
            //new("GOODBYE", "bye"),
            //new("GOODBYE", "see you later")
        };
    }
}
