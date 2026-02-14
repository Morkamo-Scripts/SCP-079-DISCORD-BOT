using Npgsql;
using NpgsqlTypes;
using SCP_079_DISCORD_BOT.Components;
using SCP_079_DISCORD_BOT.Components.Enums;
using SCP_079_DISCORD_BOT.Components.Records;

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
        cmd.Parameters.Add(new NpgsqlParameter("resolution_comment", NpgsqlDbType.Text)
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
        cmd.Parameters.Add(new NpgsqlParameter("resolution_comment", NpgsqlDbType.Text)
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
        cmd.Parameters.Add(new NpgsqlParameter("resolution_comment", NpgsqlDbType.Text)
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
    
    public async Task EnsureUserRowAsync(ulong discordId, CancellationToken ct = default)
    {
        const string sql = @"
            insert into users (discord_id, linked_steam)
            values (@discord_id, null)
            on conflict (discord_id) do nothing;";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("discord_id", (long)discordId);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<long?> GetLinkedSteamAsync(ulong discordId, CancellationToken ct = default)
    {
        const string sql = @"
            select linked_steam
            from users
            where discord_id = @discord_id
            limit 1;";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("discord_id", (long)discordId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct))
            return null;

        var ord = reader.GetOrdinal("linked_steam");
        return reader.IsDBNull(ord) ? null : reader.GetInt64(ord);
    }

    public async Task<ulong?> GetDiscordIdByLinkedSteamAsync(long steamId64, CancellationToken ct = default)
    {
        const string sql = @"
            select discord_id
            from users
            where linked_steam = @steamid64
            limit 1;";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("steamid64", steamId64);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is null ? null : (ulong)Convert.ToInt64(result);
    }

    public async Task<SteamLinkRequestItem?> GetLatestSteamLinkRequestAsync(ulong discordId, CancellationToken ct = default)
    {
        const string sql = @"
            select discord_id, steamid64, code, created_at
            from steam_link_requests
            where discord_id = @discord_id
            order by created_at desc
            limit 1;";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("discord_id", (long)discordId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct))
            return null;

        var dId = (ulong)reader.GetInt64(reader.GetOrdinal("discord_id"));
        var sId = reader.GetInt64(reader.GetOrdinal("steamid64"));
        var code = reader.GetString(reader.GetOrdinal("code")).Trim();
        var createdAt = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at"));

        return new SteamLinkRequestItem(dId, sId, code, createdAt);
    }

    public async Task DeleteSteamLinkRequestsAsync(ulong discordId, CancellationToken ct = default)
    {
        const string sql = @"
            delete from steam_link_requests
            where discord_id = @discord_id;";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("discord_id", (long)discordId);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task CreateSteamLinkRequestAsync(ulong discordId, long steamId64, string code, CancellationToken ct = default)
    {
        const string sql = @"
            insert into steam_link_requests (discord_id, steamid64, code, created_at)
            values (@discord_id, @steamid64, @code, now());";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("discord_id", (long)discordId);
        cmd.Parameters.AddWithValue("steamid64", steamId64);
        cmd.Parameters.AddWithValue("code", code);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<SteamLinkRequestResult> GetOrCreateSteamLinkRequestAsync(
        ulong discordId,
        long steamId64,
        Func<string> codeFactory,
        TimeSpan reuseWindow,
        CancellationToken ct = default)
    {
        await EnsureUserRowAsync(discordId, ct);

        var linkedSteam = await GetLinkedSteamAsync(discordId, ct);
        if (linkedSteam is not null)
            return new SteamLinkRequestResult(SteamLinkRequestResultType.AlreadyLinked, null);

        var ownerDiscordId = await GetDiscordIdByLinkedSteamAsync(steamId64, ct);
        if (ownerDiscordId is not null && ownerDiscordId.Value != discordId)
            return new SteamLinkRequestResult(SteamLinkRequestResultType.SteamAlreadyLinkedToAnotherUser, null);

        var latest = await GetLatestSteamLinkRequestAsync(discordId, ct);
        if (latest is not null)
        {
            var age = DateTimeOffset.UtcNow - latest.CreatedAt;
            if (age < reuseWindow)
            {
                return new SteamLinkRequestResult(
                    SteamLinkRequestResultType.OkExisting,
                    latest
                );
            }
        }

        await DeleteSteamLinkRequestsAsync(discordId, ct);

        var newCode = codeFactory();
        await CreateSteamLinkRequestAsync(discordId, steamId64, newCode, ct);

        var created = await GetLatestSteamLinkRequestAsync(discordId, ct);

        return new SteamLinkRequestResult(
            SteamLinkRequestResultType.OkNew,
            created
        );
    }
    
    public async Task<ConfirmResult> ConfirmSteamLinkAsync(string code, string steamId, TimeSpan ttl, CancellationToken ct = default)
    {
        code = (code ?? string.Empty).Trim().ToUpperInvariant();
        steamId = (steamId ?? string.Empty).Trim();

        if (code.Length != 6)
            return ConfirmResult.NotFound;

        if (!long.TryParse(steamId, out var steamId64))
            return ConfirmResult.Mismatch;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        long discordId;
        long requestedSteamId64;
        DateTimeOffset createdAt;

        const string selectSql = @"
            select discord_id, steamid64, created_at
            from steam_link_requests
            where code = @code
            limit 1;";

        await using (var cmd = new NpgsqlCommand(selectSql, conn))
        {
            cmd.Parameters.AddWithValue("code", code);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                return ConfirmResult.NotFound;

            discordId = reader.GetInt64(reader.GetOrdinal("discord_id"));
            requestedSteamId64 = reader.GetInt64(reader.GetOrdinal("steamid64"));
            createdAt = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at"));
        }

        var age = DateTimeOffset.UtcNow - createdAt;
        if (age > ttl)
        {
            const string deleteExpired = @"delete from steam_link_requests where discord_id = @discord_id;";
            await using var cmd = new NpgsqlCommand(deleteExpired, conn);
            cmd.Parameters.AddWithValue("discord_id", discordId);
            await cmd.ExecuteNonQueryAsync(ct);
            return ConfirmResult.Expired;
        }

        if (requestedSteamId64 != steamId64)
            return ConfirmResult.Mismatch;

        await using var tx = await conn.BeginTransactionAsync(ct);

        const string upsertUser = @"
            insert into users (discord_id, linked_steam)
            values (@discord_id, @steamid64)
            on conflict (discord_id) do update
            set linked_steam = excluded.linked_steam;";

        const string deleteReq = @"delete from steam_link_requests where discord_id = @discord_id;";

        await using (var cmd = new NpgsqlCommand(upsertUser, conn, tx))
        {
            cmd.Parameters.AddWithValue("discord_id", discordId);
            cmd.Parameters.AddWithValue("steamid64", steamId64);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using (var cmd = new NpgsqlCommand(deleteReq, conn, tx))
        {
            cmd.Parameters.AddWithValue("discord_id", discordId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        return ConfirmResult.Success;
    }

    public async Task<bool> UnlinkSteamAsync(ulong discordId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var tx = await conn.BeginTransactionAsync();

        const string updateSql = @"
update users
set linked_steam = null
where discord_id = @discord_id
  and linked_steam is not null;";

        const string deleteReqSql = @"
delete from steam_link_requests
where discord_id = @discord_id;";

        int updated;
        await using (var cmd = new NpgsqlCommand(updateSql, conn, tx))
        {
            cmd.Parameters.AddWithValue("discord_id", (long)discordId);
            updated = await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = new NpgsqlCommand(deleteReqSql, conn, tx))
        {
            cmd.Parameters.AddWithValue("discord_id", (long)discordId);
            await cmd.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();

        return updated > 0;
    }
    
    public sealed record GameTimeDay(DateOnly Day, int Minutes);

    public async Task AddGameTimeTickAsync(string serverCode, long[] steamIds, int addMinutes, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(serverCode))
            return;

        if (steamIds == null || steamIds.Length == 0)
            return;

        if (addMinutes <= 0)
            return;

        var day = DateOnly.FromDateTime(utcNow);

        const string sql = @"
    insert into game_time_daily (server_code, steamid64, day_date, minutes)
    select @server_code, x.steamid64, @day_date, @add_minutes
    from unnest(@steam_ids) as x(steamid64)
    on conflict (server_code, steamid64, day_date)
    do update set minutes = game_time_daily.minutes + excluded.minutes;";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("server_code", serverCode);
        cmd.Parameters.AddWithValue("day_date", day);
        cmd.Parameters.AddWithValue("add_minutes", addMinutes);
        cmd.Parameters.AddWithValue("steam_ids", steamIds);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<long?> GetLinkedSteamAsync(ulong discordId)
    {
        const string sql = @"
    select linked_steam
    from users
    where discord_id = @discord_id
    limit 1;";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("discord_id", (long)discordId);

        var result = await cmd.ExecuteScalarAsync();
        if (result is null || result is DBNull)
            return null;

        return Convert.ToInt64(result);
    }

    public async Task<IReadOnlyList<GameTimeDay>> GetGameTimeDaysAsync(string serverCode, long steamId64, int days)
    {
        if (days < 1)
            days = 1;

        if (days > 90)
            days = 90;

        var anchor = DateOnly.FromDateTime(DateTime.Now);

        const string sql = @"
select day_date, minutes
from game_time_daily
where server_code = @server_code
  and steamid64 = @steamid64
  and day_date >= (@anchor_date - (@days::int - 1))
  and day_date <= @anchor_date
order by day_date desc;";

        var list = new List<GameTimeDay>(days);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("server_code", serverCode);
        cmd.Parameters.AddWithValue("steamid64", steamId64);
        cmd.Parameters.AddWithValue("anchor_date", anchor);
        cmd.Parameters.AddWithValue("days", days);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var day = reader.GetFieldValue<DateOnly>(0);
            var minutes = reader.GetInt32(1);
            list.Add(new GameTimeDay(day, minutes));
        }

        return list;
    }

    public async Task<int> GetGameTimeTotalMinutesAsync(string serverCode, long steamId64, int? days)
    {
        string sql;

        var anchor = DateOnly.FromDateTime(DateTime.Now);

        if (days.HasValue)
        {
            var d = days.Value;

            if (d < 1)
                d = 1;

            if (d > 90)
                d = 90;
            Utils.BotLog($"GT anchor_date = {anchor:yyyy-MM-dd}", LogType.Info);

            sql = @"
select coalesce(sum(minutes), 0)
from game_time_daily
where server_code = @server_code
  and steamid64 = @steamid64
  and day_date >= (@anchor_date - (@days::int - 1))
  and day_date <= @anchor_date;";
        }
        else
        {
            sql = @"
select coalesce(sum(minutes), 0)
from game_time_daily
where server_code = @server_code
  and steamid64 = @steamid64;";
        }

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("server_code", serverCode);
        cmd.Parameters.AddWithValue("steamid64", steamId64);

        if (days.HasValue)
        {
            cmd.Parameters.AddWithValue("anchor_date", anchor);
            cmd.Parameters.AddWithValue("days", Math.Clamp(days.Value, 1, 90));
        }

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result ?? 0);
    }
}