using testapi1.Domain.Progression;

namespace testapi1.Services.Dialogue
{
    internal static class NpcRevealPolicy
    {
        private static readonly IReadOnlyDictionary<string, string> GuidanceByTopic =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["public_story"] = "Safe baseline cover story Dylan may repeat at any time.",
                ["topic_email"] = "Acknowledge Elsa was upset or writing something work-related, but avoid admitting the complaint email unless pressure is strong.",
                ["topic_money"] = "Deny fraud, minimize irregularities, and reframe findings as accounting confusion or incomplete paperwork.",
                ["topic_argument"] = "Admit to a tense workplace conversation only if cornered by evidence, but deny it escalated into violence before Confession.",
                ["topic_alibi"] = "Defend or adjust the timeline carefully without explicitly admitting he stayed for the murder.",
                ["topic_suitcase"] = "Downplay the storage suitcase, call it personal storage, and avoid discussing contents unless cornered by unlocked evidence.",
                ["topic_cleanup"] = "Deny coverup and treat cleanup-related evidence as normal office or janitorial items.",
                ["topic_weapon"] = "Never explain the weapon directly before Confession; deflect or challenge provenance.",
                ["topic_coverup"] = "Never admit hiding evidence before Confession; at most show panic or evasiveness."
            };

        public static IReadOnlyList<string> GetAllowedTopics(ProgressionSessionState state)
        {
            var topics = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "public_story"
            };

            var discoveredTopics = state.DiscoveredClues
                .Select(ClueCatalog.ToUnlockTopic)
                .Where(topic => !string.IsNullOrWhiteSpace(topic))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var discussedTopics = state.DiscussedClues
                .Select(ClueCatalog.ToUnlockTopic)
                .Where(topic => !string.IsNullOrWhiteSpace(topic))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            switch (state.State)
            {
                case ProgressionStateId.Intro:
                    break;

                case ProgressionStateId.InformationGathering:
                    topics.UnionWith(discussedTopics);
                    break;

                case ProgressionStateId.BuildingCase:
                case ProgressionStateId.ConfessionWindow:
                case ProgressionStateId.GuiltyNoConfession:
                case ProgressionStateId.ClosedNoResolution:
                    topics.UnionWith(discoveredTopics);
                    topics.UnionWith(discussedTopics);
                    break;

                case ProgressionStateId.Confession:
                    topics.UnionWith(discoveredTopics);
                    topics.UnionWith(discussedTopics);
                    topics.Add("truth");
                    break;
            }

            return topics
                .OrderBy(topic => topic, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static IReadOnlyDictionary<string, string> GetTopicGuidance(IReadOnlyCollection<string> allowedTopics)
        {
            var guidance = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var topic in allowedTopics)
            {
                if (GuidanceByTopic.TryGetValue(topic, out var rule))
                {
                    guidance[topic] = rule;
                }
            }

            if (allowedTopics.Contains("truth", StringComparer.OrdinalIgnoreCase))
            {
                guidance["truth"] = "Explicit admission is allowed only because the state is Confession.";
            }

            return guidance;
        }
    }
}
