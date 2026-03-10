namespace testapi1.Services.Intent
{
    public sealed record IntentExample(string Intent, string Text);

    public static class IntentSeed
    {
        public static readonly IntentExample[] Examples =
        {
            // --- ASK_OPEN_QUESTION ---
            new("ASK_OPEN_QUESTION", "start from the beginning"),
            new("ASK_OPEN_QUESTION", "walk me through what happened"),
            new("ASK_OPEN_QUESTION", "explain it"),
            new("ASK_OPEN_QUESTION", "what was your plan"),
            new("ASK_OPEN_QUESTION", "what exactly was taken"),
            new("ASK_OPEN_QUESTION", "if i prove it, will you be honest"),

            // --- ASK_TIMELINE ---
            new("ASK_TIMELINE", "what time did you go to bed"),
            new("ASK_TIMELINE", "when did you wake up"),
            new("ASK_TIMELINE", "where were you at 2 am"),
            new("ASK_TIMELINE", "what happened before midnight"),
            new("ASK_TIMELINE", "give me the timeline from last night"),

            // --- EMPATHY ---
            new("EMPATHY", "that sounds stressful"),
            new("EMPATHY", "take your time, i am listening"),
            new("EMPATHY", "if you are in trouble, tell me now"),
            new("EMPATHY", "i am giving you a chance to control how this ends"),
            new("EMPATHY", "if you do not know, just say you do not know"),

            // --- PRESENT_EVIDENCE ---
            new("PRESENT_EVIDENCE", "the safe has no damage and no pry marks"),
            new("PRESENT_EVIDENCE", "the alarm was disarmed at 2 13 and rearmed at 2 20"),
            new("PRESENT_EVIDENCE", "most of the glass is inside the study"),
            new("PRESENT_EVIDENCE", "this pawn receipt is signed with your name"),
            new("PRESENT_EVIDENCE", "you received a final debt notice due february second"),

            // --- CONTRADICTION ---
            new("CONTRADICTION", "that does not match what you said earlier"),
            new("CONTRADICTION", "you said burglars forced the safe but there is no damage"),
            new("CONTRADICTION", "so they entered, disarmed the alarm, and left no trace"),
            new("CONTRADICTION", "your story is not consistent"),
            new("CONTRADICTION", "how do you explain this contradiction"),

            // --- SILENCE ---
            new("SILENCE", "..."),
            new("SILENCE", "i will wait"),
            new("SILENCE", "take a moment"),

            // --- INTIMIDATE ---
            new("INTIMIDATE", "stop wasting my time and tell the truth"),
            new("INTIMIDATE", "confess now before this gets worse"),
            new("INTIMIDATE", "you are lying to me"),
            new("INTIMIDATE", "do not make me prove every lie"),

            // --- CLOSE_INTERROGATION ---
            new("CLOSE_INTERROGATION", "we are done talking for now"),
            new("CLOSE_INTERROGATION", "interview ended"),
            new("CLOSE_INTERROGATION", "i am ending this session")
        };
    }
}
