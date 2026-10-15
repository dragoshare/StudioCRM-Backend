using StudioCRM.Application.Common;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Tests.UnitTests;

[Collection("TokenProtector")]
public class TokenSecurityTests
{
    [Fact]
    public void SecretTokenHashIsStableAndNotTheToken()
    {
        var hash = SecretTokens.Hash("refresh-token");
        Assert.Equal(64, hash.Length);
        Assert.Equal(hash, SecretTokens.Hash("refresh-token"));
        Assert.NotEqual("refresh-token", hash);
    }

    [Fact]
    public void LeakedHashCannotBeReplayedAsLegacyPlaintext()
    {
        Assert.False(SecretTokens.CanBeLegacyPlaintext(SecretTokens.Hash("anything")));
        Assert.True(SecretTokens.CanBeLegacyPlaintext(Convert.ToBase64String(new byte[64])));
        Assert.False(SecretTokens.CanBeLegacyPlaintext(""));
    }

    [Fact]
    public void OutlookTokensRoundTripAndLegacyPlaintextStaysReadable()
    {
        try
        {
            TokenProtector.Configure(Convert.ToBase64String(new byte[32]));
            var stored = TokenProtector.Protect("graph-access-token");
            Assert.StartsWith("enc:v1:", stored);
            Assert.DoesNotContain("graph-access-token", stored);
            Assert.Equal("graph-access-token", TokenProtector.Unprotect(stored));
            Assert.Equal(stored, TokenProtector.Protect(stored));
            Assert.Equal("legacy-plain", TokenProtector.Unprotect("legacy-plain"));
        }
        finally
        {
            TokenProtector.Configure(null);
        }
    }

    [Fact]
    public void WithoutKeyTokensPassThroughButEncryptedValuesFailLoudly()
    {
        TokenProtector.Configure(null);
        Assert.Equal("plain", TokenProtector.Protect("plain"));
        Assert.Throws<InvalidOperationException>(() => TokenProtector.Unprotect("enc:v1:AAAA"));
        Assert.Throws<InvalidOperationException>(() => TokenProtector.Configure(Convert.ToBase64String(new byte[16])));
    }
}
