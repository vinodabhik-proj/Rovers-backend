using FluentAssertions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Options;
using Moq;
using Rovers_backend.Controllers;
using Rovers_backend.Models;
using Rovers_backend.Options;
using System.Security.Claims;
using Xunit;

namespace Rovers_backend.Tests.Unit;

public class AuthControllerTests
{
    private readonly Mock<UserManager<ApplicationUser>> _userManager;
    private readonly Mock<SignInManager<ApplicationUser>> _signInManager;
    private readonly IOptions<FrontendOptions> _frontendOptions;

    public AuthControllerTests()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();

        _userManager = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _signInManager = new Mock<SignInManager<ApplicationUser>>(
            _userManager.Object,
            new Mock<IHttpContextAccessor>().Object,
            new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>().Object,
            null!, null!, null!, null!);

        _frontendOptions = Microsoft.Extensions.Options.Options.Create(new FrontendOptions
        {
            BaseUrl = "http://localhost:5173"
        });
    }

    [Fact]
    public async Task Me_Returns_User_When_User_Exists()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "test@test.com",
            FirstName = "Test",
            LastName = "User",
            UserName = "test@test.com"
        };

        _userManager
            .Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(user);

        _userManager
            .Setup(x => x.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "Admin" });

        var controller = CreateControllerWithHttpContext();

        var result = await controller.Me();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
        ok.Value.Should().BeEquivalentTo(new
        {
            Id = userId,
            Email = "test@test.com",
            FirstName = "Test",
            LastName = "User",
            UserName = "test@test.com",
            Roles = new List<string> { "Admin" }
        });
    }

    [Fact]
    public async Task Me_Returns_Unauthorized_When_User_Does_Not_Exist()
    {
        _userManager
            .Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var controller = CreateControllerWithHttpContext();

        var result = await controller.Me();

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public void EntraLogin_Returns_Challenge_To_Frontend_BaseUrl_When_ReturnUrl_Is_Null()
    {
        var controller = CreateController();

        var result = controller.EntraLogin();

        var challenge = Assert.IsType<ChallengeResult>(result);
        Assert.Contains(OpenIdConnectDefaults.AuthenticationScheme, challenge.AuthenticationSchemes);
        Assert.Equal("http://localhost:5173", challenge.Properties!.RedirectUri);
        Assert.True(challenge.Properties.IsPersistent);
    }

    [Fact]
    public void EntraLogin_Appends_ReturnUrl_To_Frontend_BaseUrl_When_ReturnUrl_Is_Provided()
    {
        var controller = CreateController();

        var result = controller.EntraLogin("/test-return-url");

        var challenge = Assert.IsType<ChallengeResult>(result);
        Assert.Contains(OpenIdConnectDefaults.AuthenticationScheme, challenge.AuthenticationSchemes);
        Assert.Equal("http://localhost:5173/test-return-url", challenge.Properties!.RedirectUri);
        Assert.True(challenge.Properties.IsPersistent);
    }

    [Fact]
    public async Task Logout_Calls_SignOut_And_Returns_SignOut_For_Cookie_And_OpenIdConnect()
    {
        var controller = CreateControllerWithHttpContext();

        var result = await controller.Logout();

        _signInManager.Verify(x => x.SignOutAsync(), Times.Once);

        var signOut = Assert.IsType<SignOutResult>(result);
        Assert.Contains(CookieAuthenticationDefaults.AuthenticationScheme, signOut.AuthenticationSchemes);
        Assert.Contains(OpenIdConnectDefaults.AuthenticationScheme, signOut.AuthenticationSchemes);
        Assert.Contains("/auth/signout-callback", signOut.Properties!.RedirectUri);
    }

    [Fact]
    public void SignOutCallback_Redirects_To_Frontend()
    {
        var controller = CreateController();

        var result = controller.SignOutCallback();

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("http://localhost:5173", redirect.Url);
    }

    private AuthController CreateController()
    {
        return new AuthController(
            _userManager.Object,
            _signInManager.Object,
            _frontendOptions
        );
    }

    private AuthController CreateControllerWithHttpContext()
    {
        var controller = CreateController();

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity())
            }
        };

        var urlHelper = new Mock<IUrlHelper>();
        urlHelper
            .Setup(x => x.Action(It.IsAny<UrlActionContext>()))
            .Returns("http://localhost/auth/signout-callback");
        controller.Url = urlHelper.Object;

        return controller;
    }
}
