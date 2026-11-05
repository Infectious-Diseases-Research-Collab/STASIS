using System.Net;
using Xunit;

namespace STASIS.Tests.Integration;

/// <summary>
/// STASIS has no working email, so passwords are reset only by a System Admin from
/// Administration → Edit User. The default Identity UI's self-service reset pages are overridden
/// to return 404 (like Register and ResendEmailConfirmation). Before this, Forgot Password
/// returned a 500 for existing accounts (the SMTP send failed) and a success page for unknown
/// ones, which revealed which emails had accounts. Requires STASIS_TEST_CONNECTION to be set; see
/// <see cref="TestWebApplicationFactory"/>.
/// </summary>
public class SelfServicePasswordResetDisabledTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public SelfServicePasswordResetDisabledTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/Identity/Account/ForgotPassword")]
    [InlineData("/Identity/Account/ForgotPasswordConfirmation")]
    [InlineData("/Identity/Account/ResetPassword?code=abc")]
    [InlineData("/Identity/Account/ResetPasswordConfirmation")]
    public async Task SelfServiceResetPage_Get_ReturnsNotFound(string url)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task LoginPage_HasNoForgotPasswordLink_AndTellsUsersToContactAnAdmin()
    {
        var client = _factory.CreateClient();

        var html = await client.GetStringAsync("/Identity/Account/Login");

        Assert.DoesNotContain("ForgotPassword", html);
        Assert.Contains("contact a STASIS administrator", html);
    }
}
