using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Api.Controllers;
using StudioCRM.Application.Interfaces.Calendar;
using StudioCRM.Infrastructure.Services.Calendar;

namespace StudioCRM.Tests.UnitTests;

public class OutlookOAuthErrorTests
{
    [Theory]
    [InlineData("access_denied", "AADSTS65004: User declined to consent to access the app.", "AccessDenied")]
    [InlineData("access_denied", null, "AccessDenied")]
    [InlineData("access_denied", "AADSTS65001: The user or administrator has not consented to use the application.", "AdminConsentRequired")]
    [InlineData("access_denied", "AADSTS90094: An administrator of Contoso has set a policy that prevents you from granting permissions.", "AdminConsentRequired")]
    [InlineData("consent_required", null, "AdminConsentRequired")]
    [InlineData("invalid_request", "AADSTS50020: User account from identity provider does not exist in tenant.", "AccountNotSupported")]
    [InlineData("invalid_request", "AADSTS50011: The redirect URI does not match.", "AccountNotSupported")]
    [InlineData("login_required", null, "SignInRequired")]
    [InlineData("temporarily_unavailable", null, "MicrosoftUnavailable")]
    [InlineData("something_new", "AADSTS99999: Unknown.", "MicrosoftError")]
    public void ClassifiesMicrosoftErrors(string error, string? description, string expectedCode) =>
        Assert.Equal(expectedCode, OutlookOAuthError.Describe(error, description).Code);

    [Fact]
    public void ExtractsAadstsCodeAndTrimsUntrustedDescription()
    {
        var result = OutlookOAuthError.Describe("access_denied\r\nInjected: x",
            "AADSTS65001: consent missing\r\nTrace ID: 1\u0007" + new string('x', 500));
        Assert.Equal(("AdminConsentRequired", "AADSTS65001", "access_denied"), (result.Code, result.MicrosoftCode, result.MicrosoftError));
        Assert.Equal("AADSTS65001: consent missing", result.Detail);
        Assert.Null(OutlookOAuthError.Describe("x", new string('y', 1000)).MicrosoftCode);
        Assert.Equal(OutlookOAuthError.MaxDetailLength, OutlookOAuthError.Describe("x", new string('y', 1000)).Detail!.Length);
    }

    [Fact]
    public async Task CallbackReturnsReadableErrorWithoutCallingTheAuthService()
    {
        var calls = 0;
        var auth = OwnerPanelContractTests.Fake<IOutlookCalendarAuthService>.Create((_, _) => { calls++; return Task.CompletedTask; });
        var controller = new OutlookController(auth, null!, null!, null!, null!, null!)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = Assert.IsType<BadRequestObjectResult>(await controller.Callback(null, "state", "access_denied",
            "AADSTS65001: The user or administrator has not consented to use the application."));
        var body = result.Value!;
        string? Read(string name) => body.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)!.GetValue(body) as string;
        Assert.Equal(("AdminConsentRequired", "AADSTS65001"), (Read("code"), Read("microsoftCode")));
        Assert.Contains("administrator", Read("message"));
        Assert.Equal(0, calls);

        await controller.Callback("code", "state");
        Assert.Equal(1, calls);
    }
}
