using System.Data;
using System.Data.Common;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Services;

// Lock distribuido por documento. A diferencia de SemaphoreSlim, sp_getapplock
// coordina las instancias separadas de la API y del worker usando SQL Server.
public sealed class SqlAppLock : IAsyncDisposable
{
    private readonly FelDbContext _db;
    private readonly DbConnection _connection;
    private readonly bool _openedHere;
    private readonly string _resource;
    private bool _released;

    private SqlAppLock(FelDbContext db, DbConnection connection, bool openedHere, string resource)
    {
        _db = db;
        _connection = connection;
        _openedHere = openedHere;
        _resource = resource;
    }

    public static async Task<SqlAppLock?> AcquireAsync(FelDbContext db, string resource, int timeoutMilliseconds = 5000)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
            await db.Database.OpenConnectionAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                DECLARE @result int;
                EXEC @result = sp_getapplock
                    @Resource = @resource,
                    @LockMode = 'Exclusive',
                    @LockOwner = 'Session',
                    @LockTimeout = @timeout;
                SELECT @result;
                """;
            command.CommandTimeout = Math.Max(1, timeoutMilliseconds / 1000 + 1);
            AddParameter(command, "@resource", resource);
            AddParameter(command, "@timeout", timeoutMilliseconds);

            var value = await command.ExecuteScalarAsync();
            var result = Convert.ToInt32(value);
            if (result < 0)
            {
                if (openedHere) await db.Database.CloseConnectionAsync();
                return null;
            }

            return new SqlAppLock(db, connection, openedHere, resource);
        }
        catch
        {
            if (openedHere) await db.Database.CloseConnectionAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_released) return;
        _released = true;

        try
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = "EXEC sp_releaseapplock @Resource = @resource, @LockOwner = 'Session';";
            AddParameter(command, "@resource", _resource);
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (_openedHere)
                await _db.Database.CloseConnectionAsync();
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
