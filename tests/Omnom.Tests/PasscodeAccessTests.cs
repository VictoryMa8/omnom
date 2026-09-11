using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Omnom.Api.Data;
using Omnom.Api.Data.Entities;
using Omnom.Api.Services;
using Xunit;

namespace Omnom.Tests;

public class PasscodeAccessTests
{
    [Fact]
    public void ProtectedApiRejectsMissingAndForgedTokensAndAcceptsIssuedToken()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.UserSettings.Add(new UserSetting { Key = "AppPasscode", Value = "1234" });
        db.SaveChanges();
        var access = new PasscodeAccess(db, new ConfigurationBuilder().Build());
        var http = new DefaultHttpContext();
        var context = new AuthorizationFilterContext(new ActionContext(http, new RouteData(),
            new ActionDescriptor()), new List<IFilterMetadata>());
        new PasscodeAccessFilter(access, new ConfigurationBuilder().Build()).OnAuthorization(context);
        Assert.IsType<UnauthorizedObjectResult>(context.Result);
        http.Request.Headers.Authorization = "Bearer forged";
        Assert.False(access.HasAccess(http.Request));
        http.Request.Headers.Authorization = "Bearer " + access.GenerateToken();
        Assert.True(access.HasAccess(http.Request));
        db.UserSettings.Single().Value = "5678";
        db.SaveChanges();
        Assert.False(access.HasAccess(http.Request));
    }

    [Fact]
    public void ExplicitlyClearedPasscodeOverridesConfiguredFallback()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.UserSettings.Add(new UserSetting { Key = "AppPasscode", Value = "" });
        db.SaveChanges();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["AppPasscode"] = "1234" }).Build();
        Assert.True(new PasscodeAccess(db, config).HasAccess(new DefaultHttpContext().Request));
    }
}
