using Npgsql;
using NpgsqlTypes;
using SCP_079_DISCORD_BOT.Components.Enums;

namespace SCP_079_DISCORD_BOT.Database;

public sealed class DbService
{
    private readonly string _connectionString;

    public DbService(string connectionString)
    {
        _connectionString = connectionString;
    }

    
    private static string? NormalizeResolutionComment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        value = value.Replace("**Комментарий модератора:** ", "**Комментарий модератора:**\n", StringComparison.Ordinal);

        if (value.StartsWith("Комментарий модератора:", StringComparison.OrdinalIgnoreCase))
        {
            var rest = value.Substring("Комментарий модератора:".Length).TrimStart('\r', '\n', ' ');
            value = $"**Комментарий модератора:**\n{rest}";
        }

        return value;
    }
public async Task TestConnectionAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand("select 1", conn);
        var result = await cmd.ExecuteScalarAsync(ct);

        if (result is null || Convert.ToInt32(result) != 1)
            throw new InvalidOperationException("Database ping failed (select 1 returned unexpected result).");
    }

    public async Task<(Guid WarnId, long WarnNo)> CreateWarnAsync(
        ulong guildId,
        ulong targetUserId,
        ulong authorUserId,
        string reason,
        string? resolutionComment,
        WarnCategory category,
        DateTimeOffset expiresAt,
        CancellationToken ct = default)
    {
        var id = Guid.NewGuid();

        const string sql = @"
            insert into warns
            (id, guild_id, target_user_id, author_user_id, reason, resolution_comment, category, status, created_at, expires_at)
            values
            (@id, @guild_id, @target_user_id, @author_user_id, @reason, @resolution_comment, @category, @status, now(), @expires_at)
            returning id, warn_no;";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("guild_id", (long)guildId);
        cmd.Parameters.AddWithValue("target_user_id", (long)targetUserId);
        cmd.Parameters.AddWithValue("author_user_id", (long)authorUserId);
        cmd.Parameters.AddWithValue("reason", reason);
        cmd.Parameters.Add(new NpgsqlParameter("resolution_comment", NpgsqlTypes.NpgsqlDbType.Text)
        {
            Value = (object?)resolutionComment ?? DBNull.Value
        });
        cmd.Parameters.AddWithValue("category", category);
        cmd.Parameters.AddWithValue("status", WarnStatus.Waiting);
        cmd.Parameters.AddWithValue("expires_at", expiresAt.UtcDateTime);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);

        var warnId = reader.GetGuid(0);
        var warnNo = reader.GetInt64(1);

        return (warnId, warnNo);
    }

    public async Task AddWarnMediaAsync(
        Guid warnId,
        string url,
        string filename,
        string mediaType,
        CancellationToken ct = default)
    {
        const string sql = @"
            insert into warn_media
            (warn_id, url, filename, media_type, created_at)
            values
            (@warn_id, @url, @filename, @media_type, now());";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("warn_id", warnId);
        cmd.Parameters.AddWithValue("url", url);
        cmd.Parameters.AddWithValue("filename", filename);
        cmd.Parameters.AddWithValue("media_type", mediaType);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<WarnItem>> GetActiveWarnsAsync(
        ulong guildId,
        ulong targetUserId,
        CancellationToken ct = default)
    {
        const string sql = @"
            select
                id,
                coalesce(warn_no, 0) as warn_no,
                guild_id,
                target_user_id,
                author_user_id,
                responsible_user_id,
                reason,
                category,
                status,
                created_at,
                expires_at,
                resolved_at,
                resolution_comment
            from warns
            where guild_id = @guild_id
              and target_user_id = @target_user_id
              and status = 'Active'
              and expires_at > now()
            order by created_at desc;";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("guild_id", (long)guildId);
        cmd.Parameters.AddWithValue("target_user_id", (long)targetUserId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var list = new List<WarnItem>();

        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetGuid(reader.GetOrdinal("id"));
            var warnNo = reader.GetInt64(reader.GetOrdinal("warn_no"));

            var gId = (ulong)reader.GetInt64(reader.GetOrdinal("guild_id"));
            var tId = (ulong)reader.GetInt64(reader.GetOrdinal("target_user_id"));
            var aId = (ulong)reader.GetInt64(reader.GetOrdinal("author_user_id"));

            ulong? rId;
            {
                var ord = reader.GetOrdinal("responsible_user_id");
                rId = reader.IsDBNull(ord) ? null : (ulong)reader.GetInt64(ord);
            }

            var reason = reader.GetString(reader.GetOrdinal("reason"));
            var category = Enum.Parse<WarnCategory>(reader.GetString(reader.GetOrdinal("category")));
            var status = Enum.Parse<WarnStatus>(reader.GetString(reader.GetOrdinal("status")));

            var createdAt = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at"));
            var expiresAt = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("expires_at"));

            DateTimeOffset? resolvedAt;
            {
                var ord = reader.GetOrdinal("resolved_at");
                resolvedAt = reader.IsDBNull(ord) ? null : reader.GetFieldValue<DateTimeOffset>(ord);
            }

            string? resolutionComment;
            {
                var ord = reader.GetOrdinal("resolution_comment");
                resolutionComment = reader.IsDBNull(ord) ? null : NormalizeResolutionComment(reader.GetString(ord));
            }

            list.Add(new WarnItem(
                id,
                warnNo,
                gId,
                tId,
                aId,
                rId,
                reason,
                category,
                status,
                createdAt,
                expiresAt,
                resolvedAt,
                resolutionComment
            ));
        }

        return list;
    }

    public async Task<bool> ResolveWarnAsync(
        Guid warnId,
        WarnStatus newStatus,
        ulong responsibleUserId,
        string? resolutionComment,
        CancellationToken ct = default)
    {
        if (newStatus != WarnStatus.Active && newStatus != WarnStatus.Aborted)
            throw new ArgumentOutOfRangeException(nameof(newStatus));

        const string sql = @"
            update warns
            set status = @status,
                responsible_user_id = @responsible_user_id,
                resolved_at = now(),
                resolution_comment = case
                    when @resolution_comment is null then resolution_comment
                    when resolution_comment is null or resolution_comment = '' then @resolution_comment
                    else resolution_comment || E'\n\n**Комментарий модератора:**\n' || @resolution_comment
                end
            where id = @id
              and status = 'Waiting';";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("status", newStatus);
        cmd.Parameters.AddWithValue("responsible_user_id", (long)responsibleUserId);
        cmd.Parameters.Add(new NpgsqlParameter("resolution_comment", NpgsqlTypes.NpgsqlDbType.Text)
        {
            Value = (object?)resolutionComment ?? DBNull.Value
        });
        cmd.Parameters.AddWithValue("id", warnId);

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }
    
    public async Task<bool> UnwarnAsync(Guid warnId, ulong responsibleUserId, string? resolutionComment, CancellationToken ct = default)
    {
        const string sql = @"
            update warns
            set status = @status,
                responsible_user_id = @responsible_user_id,
                resolved_at = now(),
                resolution_comment = case
                    when @resolution_comment is null then resolution_comment
                    when resolution_comment is null or resolution_comment = '' then @resolution_comment
                    else resolution_comment || E'\n\n**Комментарий модератора:**\n' || @resolution_comment
                end
            where id = @id
              and status in ('Active', 'Waiting');";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("status", WarnStatus.Aborted);
        cmd.Parameters.AddWithValue("responsible_user_id", (long)responsibleUserId);
        cmd.Parameters.Add(new NpgsqlParameter("resolution_comment", NpgsqlTypes.NpgsqlDbType.Text)
        {
            Value = (object?)resolutionComment ?? DBNull.Value
        });
        cmd.Parameters.AddWithValue("id", warnId);

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }
    
    public async Task<int> ExpireOutdatedWarnsAsync(CancellationToken ct = default)
    {
        const string sql = @"
            update warns
            set status = 'Expired'
            where status = 'Active'
              and expires_at <= now();";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(sql, conn);
        return await cmd.ExecuteNonQueryAsync(ct);
    }
    
    
    public async Task<WarnItem?> GetWarnByIdAsync(Guid warnId, CancellationToken ct = default)
    {
        const string sql = @"
select
    id,
    coalesce(warn_no, 0) as warn_no,
    guild_id,
    target_user_id,
    author_user_id,
    responsible_user_id,
    reason,
    category,
    status,
    created_at,
    expires_at,
    resolved_at,
    resolution_comment
from warns
where id = @id
limit 1;";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", warnId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        var id = reader.GetGuid(reader.GetOrdinal("id"));
        var warnNo = reader.GetInt64(reader.GetOrdinal("warn_no"));

        var gId = (ulong)reader.GetInt64(reader.GetOrdinal("guild_id"));
        var tId = (ulong)reader.GetInt64(reader.GetOrdinal("target_user_id"));
        var aId = (ulong)reader.GetInt64(reader.GetOrdinal("author_user_id"));

        ulong? rId;
        {
            var ord = reader.GetOrdinal("responsible_user_id");
            rId = reader.IsDBNull(ord) ? null : (ulong)reader.GetInt64(ord);
        }

        var reason = reader.GetString(reader.GetOrdinal("reason"));
        var category = Enum.Parse<WarnCategory>(reader.GetString(reader.GetOrdinal("category")));
        var status = Enum.Parse<WarnStatus>(reader.GetString(reader.GetOrdinal("status")));

        var createdAt = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at"));
        var expiresAt = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("expires_at"));

        DateTimeOffset? resolvedAt;
        {
            var ord = reader.GetOrdinal("resolved_at");
            resolvedAt = reader.IsDBNull(ord) ? null : reader.GetFieldValue<DateTimeOffset>(ord);
        }

        string? resolutionComment;
        {
            var ord = reader.GetOrdinal("resolution_comment");
            resolutionComment = reader.IsDBNull(ord) ? null : NormalizeResolutionComment(reader.GetString(ord));
        }

        return new WarnItem(
            id,
            warnNo,
            gId,
            tId,
            aId,
            rId,
            reason,
            category,
            status,
            createdAt,
            expiresAt,
            resolvedAt,
            resolutionComment
        );
    }

    public async Task<WarnItem?> GetWarnByNoAsync(ulong guildId, ulong warnNo, CancellationToken ct = default)
    {
        const string sql = @"
select
    id,
    coalesce(warn_no, 0) as warn_no,
    guild_id,
    target_user_id,
    author_user_id,
    responsible_user_id,
    reason,
    category,
    status,
    created_at,
    expires_at,
    resolved_at,
    resolution_comment
from warns
where guild_id = @guild_id
  and warn_no = @warn_no
limit 1;";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("guild_id", (long)guildId);
        cmd.Parameters.AddWithValue("warn_no", (long)warnNo);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        var id = reader.GetGuid(reader.GetOrdinal("id"));
        var warnNo1 = reader.GetInt64(reader.GetOrdinal("warn_no"));

        var gId = (ulong)reader.GetInt64(reader.GetOrdinal("guild_id"));
        var tId = (ulong)reader.GetInt64(reader.GetOrdinal("target_user_id"));
        var aId = (ulong)reader.GetInt64(reader.GetOrdinal("author_user_id"));

        ulong? rId;
        {
            var ord = reader.GetOrdinal("responsible_user_id");
            rId = reader.IsDBNull(ord) ? null : (ulong)reader.GetInt64(ord);
        }

        var reason = reader.GetString(reader.GetOrdinal("reason"));

        var category = Enum.Parse<WarnCategory>(
            reader.GetString(reader.GetOrdinal("category"))
        );

        var status = Enum.Parse<WarnStatus>(
            reader.GetString(reader.GetOrdinal("status"))
        );

        var createdAt = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at"));
        var expiresAt = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("expires_at"));

        DateTimeOffset? resolvedAt = null;
        {
            var ord = reader.GetOrdinal("resolved_at");
            resolvedAt = reader.IsDBNull(ord) ? null : reader.GetFieldValue<DateTimeOffset>(ord);
        }

        string? resolutionComment;
        {
            var ord = reader.GetOrdinal("resolution_comment");
            resolutionComment = reader.IsDBNull(ord) ? null : NormalizeResolutionComment(reader.GetString(ord));
        }

        return new WarnItem(
            id,
            warnNo1,
            gId,
            tId,
            aId,
            rId,
            reason,
            category,
            status,
            createdAt,
            expiresAt,
            resolvedAt,
            resolutionComment
        );
    }
}