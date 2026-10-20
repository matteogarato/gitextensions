using GitUI.Avatars;

namespace GitUITests.Avatars;
public sealed class InitialsAvatarGeneratorTests
{
    [Theory]
    [InlineData("albert.einstein@noreply.com", "albert einstein", "AE")]
    [InlineData("albert.einstein@noreply.com", "albert z middlename einstein (k)", "AE")]
    [InlineData("albert.einstein@noreply.com", "albert einstein (Nobel_price_winner)", "AE")]
    [InlineData("albert.einstein@noreply.com", "EINSTEIN, Albert middlename (Nobel_price_winner)", "EM")]
    [InlineData("albert.einstein@noreply.com", "albert", "Al")]
    [InlineData("albert.einstein@noreply.com", "", "AE")]
    [InlineData("albert.einstein@noreply.com", null, "AE")]
    [InlineData("albert.z.einstein@noreply.com", null, "AE")]
    [InlineData("albert_z_einstein@noreply.com", null, "AE")]
    [InlineData("albert.bose-einstein@noreply.com", null, "AE")]
    [InlineData("albert", "", "Al")]
    [InlineData("albert.einstein", "", "AE")]
    [InlineData("", "albert.einstein", "AE")]
    [InlineData("", "AlbertEinstein", "AE")]
    [InlineData("", "A-Einstein", "AE")]
    [InlineData("", "Alb-Einstein", "AE")]
    [InlineData("", "AEinstein", "AE")]
    [InlineData("1-pass@noreply.com", "1-pass", "1P")]
    [InlineData("", "1-pass", "1P")]
    [InlineData("1-pass@noreply.com", "", "1P")]
    [InlineData("/*+=/°¨", "'µ*", "'")] // Fallback when no letters or digit found
    [InlineData("/*+=/°¨", "'µ *", "'*")] // Fallback when no letters or digit found
    [InlineData("", "", "?")]
    [InlineData(null, null, "?")]
    public void GetInitialsAndHashCode_return_initials_of_a_user(string? email, string? name, string expected)
    {
        (string initials, int _) = new InitialsAvatarProvider().GetInitialsAndColorIndex(email!, name);

        initials.Should().Be(expected);
    }
}
