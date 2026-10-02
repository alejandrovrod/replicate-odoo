using Erp.Application.Common;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Constitution Article VI.4 / decision D7: the four idempotency decisions of
/// <c>POST /api/v1/stockentries</c>, tested as pure Application logic (no HTTP, no database).
/// </summary>
public sealed class IdempotencyPolicyTests
{
    private const string Hash = "sha256-abc";

    // ---------------------------------------------------------------------- header usability

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HasUsableKey_MissingOrBlankHeader_IsFalse(string? header)
    {
        Assert.False(IdempotencyPolicy.HasUsableKey(header));
    }

    [Fact]
    public void HasUsableKey_PresentHeader_IsTrue()
    {
        Assert.True(IdempotencyPolicy.HasUsableKey("0f8d4c77-6b6e-4b7e-9d3f-1f7d8a4e2c11"));
    }

    // ---------------------------------------------------------------------------- decisions

    [Fact]
    public void Decide_UnknownKey_Proceeds()
    {
        var decision = IdempotencyPolicy.Decide(
            storedRecordExists: false,
            storedRequestHash: string.Empty,
            storedResponseStatus: null,
            requestHash: Hash);

        Assert.Equal(IdempotencyDecision.Proceed, decision);
    }

    [Fact]
    public void Decide_SameKeySameBodyCompleted_ReplaysStoredResponse()
    {
        var decision = IdempotencyPolicy.Decide(
            storedRecordExists: true,
            storedRequestHash: Hash,
            storedResponseStatus: 201,
            requestHash: Hash);

        Assert.Equal(IdempotencyDecision.ReplayStoredResponse, decision);
    }

    [Fact]
    public void Decide_SameKeySameBodyStillRunning_ReportsInProgress()
    {
        var decision = IdempotencyPolicy.Decide(
            storedRecordExists: true,
            storedRequestHash: Hash,
            storedResponseStatus: null,
            requestHash: Hash);

        Assert.Equal(IdempotencyDecision.RequestInProgress, decision);
    }

    [Fact]
    public void Decide_SameKeyDifferentBody_Conflicts()
    {
        var decision = IdempotencyPolicy.Decide(
            storedRecordExists: true,
            storedRequestHash: Hash,
            storedResponseStatus: 201,
            requestHash: "sha256-other");

        Assert.Equal(IdempotencyDecision.KeyReuseWithDifferentPayload, decision);
    }

    [Fact]
    public void Decide_HashComparison_IsOrdinal()
    {
        // "SHA256-ABC" != "sha256-abc": case must NOT be folded into an accidental match.
        var decision = IdempotencyPolicy.Decide(
            storedRecordExists: true,
            storedRequestHash: "SHA256-ABC",
            storedResponseStatus: 201,
            requestHash: "sha256-abc");

        Assert.Equal(IdempotencyDecision.KeyReuseWithDifferentPayload, decision);
    }
}
