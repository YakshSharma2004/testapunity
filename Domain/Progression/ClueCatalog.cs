namespace testapi1.Domain.Progression
{
    public sealed record ClueDefinition(
        ClueId Id,
        string Key,
        string DisplayName,
        string UnlockTopic,
        IReadOnlyCollection<string> Aliases);

    public static class ClueCatalog
    {
        private static readonly IReadOnlyList<ClueDefinition> Definitions = new List<ClueDefinition>
        {
            new(
                ClueId.ElsaEmailDraft,
                "elsa_email_draft",
                "Elsa's unsent email draft",
                "topic_email",
                new[] { "email", "elsa email", "email draft" }),
            new(
                ClueId.PayrollReport,
                "payroll_report",
                "Payroll discrepancy report",
                "topic_money",
                new[] { "payroll", "fraud report", "money report" }),
            new(
                ClueId.MeetingNote,
                "meeting_note",
                "Meeting room warning note",
                "topic_argument",
                new[] { "meeting", "warning note", "agenda note" }),
            new(
                ClueId.EntryLog,
                "entry_log",
                "Entry/access log",
                "topic_alibi",
                new[] { "entry", "access log", "security log" }),
            new(
                ClueId.PinNote,
                "pin_note",
                "Torn sticky note with suitcase PIN start",
                "topic_suitcase",
                new[] { "pin", "sticky note", "suitcase pin" }),
            new(
                ClueId.CleanupItem,
                "cleanup_item",
                "Cleaning rag/gloves",
                "topic_cleanup",
                new[] { "rag", "gloves", "cleaning" }),
            new(
                ClueId.LockedSuitcaseOpened,
                "locked_suitcase_opened",
                "Locked suitcase opened",
                "topic_suitcase",
                new[] { "suitcase opened", "opened suitcase", "storage case opened" }),
            new(
                ClueId.WeaponFound,
                "weapon_found",
                "Murder weapon found",
                "topic_weapon",
                new[] { "weapon", "murder weapon" }),
            new(
                ClueId.FlashDrive,
                "flash_drive",
                "Elsa's flash drive",
                "topic_coverup",
                new[] { "flash drive", "usb", "backup evidence" })
        };

        private static readonly IReadOnlyDictionary<ClueId, ClueDefinition> ById =
            Definitions.ToDictionary(item => item.Id);

        private static readonly IReadOnlyDictionary<string, ClueId> StrictByKey =
            BuildStrictKeyMap();

        private static readonly IReadOnlyDictionary<string, ClueId> ByAnyToken =
            BuildKeyMap();

        public static IReadOnlyList<ClueDefinition> All => Definitions;

        public static bool TryGetDefinition(ClueId clueId, out ClueDefinition definition)
        {
            return ById.TryGetValue(clueId, out definition!);
        }

        public static bool TryParseKey(string? value, out ClueId clueId)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                clueId = default;
                return false;
            }

            var token = Normalize(value);
            return StrictByKey.TryGetValue(token, out clueId);
        }

        public static bool TryParse(string? value, out ClueId clueId)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                clueId = default;
                return false;
            }

            var token = Normalize(value);
            return ByAnyToken.TryGetValue(token, out clueId);
        }

        public static string ToKey(ClueId clueId)
        {
            return ById[clueId].Key;
        }

        public static string ToUnlockTopic(ClueId clueId)
        {
            return ById[clueId].UnlockTopic;
        }

        public static string ToDisplayName(ClueId clueId)
        {
            return ById[clueId].DisplayName;
        }

        private static IReadOnlyDictionary<string, ClueId> BuildStrictKeyMap()
        {
            var map = new Dictionary<string, ClueId>(StringComparer.OrdinalIgnoreCase);
            foreach (var clue in Definitions)
            {
                map[Normalize(clue.Key)] = clue.Id;
                map[Normalize(clue.Id.ToString())] = clue.Id;
            }

            return map;
        }

        private static IReadOnlyDictionary<string, ClueId> BuildKeyMap()
        {
            var map = new Dictionary<string, ClueId>(StringComparer.OrdinalIgnoreCase);

            foreach (var clue in Definitions)
            {
                map[Normalize(clue.Key)] = clue.Id;
                map[Normalize(clue.DisplayName)] = clue.Id;
                map[Normalize(clue.Id.ToString())] = clue.Id;

                foreach (var alias in clue.Aliases)
                {
                    map[Normalize(alias)] = clue.Id;
                }
            }

            return map;
        }

        private static string Normalize(string input)
        {
            return input.Trim().ToLowerInvariant().Replace("-", "_").Replace(" ", "_");
        }
    }
}
