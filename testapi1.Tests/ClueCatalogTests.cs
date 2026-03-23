using testapi1.Domain.Progression;

namespace testapi1.Tests
{
    public sealed class ClueCatalogTests
    {
        [Fact]
        public void Every_ClueId_Has_A_Definition()
        {
            foreach (var clueId in Enum.GetValues<ClueId>())
            {
                Assert.True(ClueCatalog.TryGetDefinition(clueId, out var definition));
                Assert.Equal(clueId, definition.Id);
                Assert.False(string.IsNullOrWhiteSpace(definition.Key));
                Assert.False(string.IsNullOrWhiteSpace(definition.UnlockTopic));
            }
        }

        [Fact]
        public void Strict_Parser_Accepts_Canonical_Key_And_Rejects_Alias()
        {
            Assert.True(ClueCatalog.TryParseKey("elsa_email_draft", out var canonical));
            Assert.Equal(ClueId.ElsaEmailDraft, canonical);

            Assert.False(ClueCatalog.TryParseKey("email", out _));
            Assert.False(ClueCatalog.TryParseKey("Elsa's unsent email draft", out _));
        }
    }
}
