using Npgsql;

namespace Omnom.Api.Services;

public static class HostedDatabase
{
    public static string ConnectionString(string value)
    {
        if (!value.StartsWith("postgres://") && !value.StartsWith("postgresql://")) return value;
        var uri = new Uri(value);
        var credentials = uri.UserInfo.Split(':', 2);
        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host, Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : "",
            SslMode = SslMode.VerifyFull, Pooling = true, MaxPoolSize = 5
        }.ConnectionString;
    }
}
