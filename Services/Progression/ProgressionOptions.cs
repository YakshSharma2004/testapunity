namespace testapi1.Services.Progression
{
    public sealed class ProgressionOptions
    {
        public int SessionTtlMinutes { get; set; } = 120;
        public List<string> ConfessionRequiredClues { get; set; } = new()
        {
            "elsa_email_draft",
            "payroll_report",
            "meeting_note",
            "entry_log",
            "pin_note",
            "cleanup_item",
            "weapon_found",
            "flash_drive"
        };
    }
}
