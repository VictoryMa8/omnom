using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Npgsql;
using Omnom.Api.Data;
using Omnom.Api.Data.Entities;
using Omnom.Api.Services;
using Xunit;

namespace Omnom.Tests;

public class HostingSecurityTests
{
    [Fact]
    public async Task RequestBudgetPersistsAcrossContextsAndResetsAfterWindow()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        using var first = new AppDbContext(options);
        DbInitializer.Initialize(first);
        Assert.True(await new RequestBudget(first).Take("ai", 2, 3600));
        using var second = new AppDbContext(options);
        Assert.True(await new RequestBudget(second).Take("ai", 2, 3600));
        Assert.False(await new RequestBudget(first).Take("ai", 2, 3600));
        await first.Database.ExecuteSqlRawAsync("UPDATE \"RequestBudgets\" SET \"Window\" = -1");
        Assert.True(await new RequestBudget(second).Take("ai", 2, 3600));
    }

    [Fact]
    public void HostedPinCannotBeDisabledByStoredSettings()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.UserSettings.Add(new UserSetting { Key = "AppPasscode", Value = "" });
        db.SaveChanges();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["REQUIRE_PASSCODE"] = "true", ["APP_PASSCODE"] = "12345678"
        }).Build();
        var access = new PasscodeAccess(db, config);
        Assert.Equal("12345678", access.ConfiguredPasscode);
        Assert.False(access.HasAccess(new DefaultHttpContext().Request));
        config["APP_PASSCODE"] = "";
        Assert.False(access.HasAccess(new DefaultHttpContext().Request));
    }

    [Fact]
    public void DatabaseUrlDecodesCredentialsAndRequiresVerifiedTls()
    {
        var connection = new NpgsqlConnectionStringBuilder(HostedDatabase.ConnectionString(
            "postgresql://test%40user:p%3Ass%2Fword@db.example.com/app?sslmode=require"));
        Assert.Equal("test@user", connection.Username);
        Assert.Equal("p:ss/word", connection.Password);
        Assert.Equal(5432, connection.Port);
        Assert.Equal(SslMode.VerifyFull, connection.SslMode);
    }
}
