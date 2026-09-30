using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using StudioCRM.Api.Controllers;
using StudioCRM.Application.DTOs.Branding;
using StudioCRM.Infrastructure.Services.Branding;

namespace StudioCRM.Tests.UnitTests;

public class BrandingTests
{
    [Theory]
    [InlineData("#112233", true)]
    [InlineData("red; background:url(x)", false)]
    [InlineData("#fff", false)]
    public void OnlyHexColorsAreAccepted(string color, bool valid)
    {
        var settings = new BrandingSettingsDto { PrimaryColor = color };
        if (valid) BrandingService.ValidateSettings(settings);
        else Assert.Throws<ValidationException>(() => BrandingService.ValidateSettings(settings));
    }
    [Fact]
    public void WhiteSpaceNameIsRejected() =>
        Assert.Throws<ValidationException>(() => BrandingService.ValidateSettings(new() { ApplicationName = "  " }));

    [Fact]
    public void AdminControllerRequiresDedicatedRole()
    {
        var attribute = Assert.Single(typeof(AdminBrandingController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal("SuperAdmin", attribute.Roles);
    }

    [Fact]
    public void RejectsSvgAndCorruptPng()
    {
        Assert.Throws<InvalidOperationException>(() => BrandingPng.Validate("<svg onload='alert(1)'/>"u8.ToArray()));
        var png = ValidPng();
        png[20] ^= 1;
        Assert.Throws<InvalidOperationException>(() => BrandingPng.Validate(png));
    }

    [Fact]
    public void AcceptsPngAndRejectsAppendedContent()
    {
        var png = ValidPng();
        Assert.NotEmpty(BrandingPng.Validate(png));
        Assert.Throws<InvalidOperationException>(() => BrandingPng.Validate(png.Concat("<script/>"u8.ToArray()).ToArray()));
    }

    internal static byte[] ValidPng() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP4z8DwHwAFAAH/iZk9HQAAAABJRU5ErkJggg==");
}
