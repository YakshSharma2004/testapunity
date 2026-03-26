using testapi1.Domain.Progression;

namespace testapi1.Tests
{
    public sealed class ProgressionSessionIdTests
    {
        [Fact]
        public void NewId_Uses_Expected_Format()
        {
            var sessionId = ProgressionSessionId.NewId();

            Assert.Matches("^ps_[a-f0-9]{32}$", sessionId);
            Assert.True(ProgressionSessionId.IsValid(sessionId));
        }

        [Theory]
        [InlineData("")]
        [InlineData("abc")]
        [InlineData("PS_123")]
        [InlineData("ps_123")]
        [InlineData("ps_zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
        public void IsValid_Rejects_Invalid_Values(string value)
        {
            Assert.False(ProgressionSessionId.IsValid(value));
        }
    }
}
