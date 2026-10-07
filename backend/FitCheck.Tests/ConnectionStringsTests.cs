using FitCheck.Api.Infrastructure;
using Npgsql;

namespace FitCheck.Tests;

public class ConnectionStringsTests
{
    [Fact]
    public void Converts_postgres_urls_to_npgsql_format_with_tls()
    {
        var result = ConnectionStrings.NormalizePostgres(
            "postgresql://app_user:p%40ss@ep-cool-1234.eu-central-1.aws.neon.tech/fitcheck?sslmode=require&channel_binding=require");

        var builder = new NpgsqlConnectionStringBuilder(result);
        Assert.Equal("ep-cool-1234.eu-central-1.aws.neon.tech", builder.Host);
        Assert.Equal(5432, builder.Port);
        Assert.Equal("fitcheck", builder.Database);
        Assert.Equal("app_user", builder.Username);
        Assert.Equal("p@ss", builder.Password);
        Assert.Equal(SslMode.Require, builder.SslMode);
        Assert.Equal(ChannelBinding.Require, builder.ChannelBinding);
    }

    [Fact]
    public void Keeps_explicit_ports()
    {
        var builder = new NpgsqlConnectionStringBuilder(
            ConnectionStrings.NormalizePostgres("postgres://u:p@db.internal:6543/app"));

        Assert.Equal(6543, builder.Port);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Host=localhost;Database=fitcheck;Username=postgres;Password=postgres")]
    public void Leaves_key_value_strings_alone(string? value)
    {
        Assert.Equal(value, ConnectionStrings.NormalizePostgres(value));
    }
}
