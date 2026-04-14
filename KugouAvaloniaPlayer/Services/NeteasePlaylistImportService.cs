using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using KuGou.Net.Clients;
using Microsoft.Extensions.Logging;

namespace KugouAvaloniaPlayer.Services;

public sealed class NeteasePlaylistParseResult
{
    public bool Success { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
    public string SourcePlaylistName { get; init; } = string.Empty;
    public List<string> SongNames { get; init; } = new();
}

public sealed class NeteasePlaylistImportResult
{
    public bool Success => string.IsNullOrWhiteSpace(ErrorMessage);
    public string ErrorMessage { get; init; } = string.Empty;
    public int Total { get; init; }
    public int Matched { get; init; }
    public int Imported { get; init; }
    public List<string> FailedNames { get; init; } = new();
}

public interface INeteasePlaylistImportService
{
    Task<NeteasePlaylistParseResult> ParseAndLoadAsync(string sourceText, CancellationToken cancellationToken = default);

    Task<NeteasePlaylistImportResult> ImportToKugouAsync(
        NeteasePlaylistParseResult parseResult,
        string targetPlaylistName,
        CancellationToken cancellationToken = default);
}

public sealed class NeteasePlaylistImportService(
    IHttpClientFactory httpClientFactory,
    MusicClient musicClient,
    PlaylistClient playlistClient,
    UserClient userClient,
    ILogger<NeteasePlaylistImportService> logger) : INeteasePlaylistImportService
{
    private const int SongBatchSize = 100;
    private const int NeteaseSongDetailChunkSize = 400;
    private const string NeteasePlaylistDetailApi = "https://music.163.com/api/v6/playlist/detail";
    private const string NeteaseSongDetailApi = "https://music.163.com/api/v3/song/detail";
    private static readonly Regex UrlRegex = new(@"https?://[^\s]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<NeteasePlaylistParseResult> ParseAndLoadAsync(
        string sourceText,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceText))
            return new NeteasePlaylistParseResult { ErrorMessage = "链接不能为空。" };

        var url = ExtractUrl(sourceText);
        if (string.IsNullOrWhiteSpace(url))
            return new NeteasePlaylistParseResult { ErrorMessage = "未识别到有效链接。" };

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return new NeteasePlaylistParseResult { ErrorMessage = "链接格式不正确。" };

        var resolvedUri = await ResolveIfShortLinkAsync(uri, cancellationToken);
        if (resolvedUri == null)
            return new NeteasePlaylistParseResult { ErrorMessage = "短链解析失败，请稍后重试。" };

        if (!IsSupportedNeteaseHost(resolvedUri.Host))
            return new NeteasePlaylistParseResult { ErrorMessage = "暂只支持导入网易云歌单链接。" };

        var playlistId = ExtractPlaylistId(resolvedUri);
        if (string.IsNullOrWhiteSpace(playlistId))
            return new NeteasePlaylistParseResult { ErrorMessage = "未在链接中解析到歌单ID。" };

        return await LoadPlaylistByIdAsync(playlistId, cancellationToken);
    }

    public async Task<NeteasePlaylistImportResult> ImportToKugouAsync(
        NeteasePlaylistParseResult parseResult,
        string targetPlaylistName,
        CancellationToken cancellationToken = default)
    {
        if (!parseResult.Success)
            return new NeteasePlaylistImportResult { ErrorMessage = parseResult.ErrorMessage };

        if (parseResult.SongNames.Count == 0)
            return new NeteasePlaylistImportResult
            {
                ErrorMessage = "来源歌单没有可导入歌曲。"
            };

        if (string.IsNullOrWhiteSpace(targetPlaylistName))
            return new NeteasePlaylistImportResult { ErrorMessage = "目标歌单名称不能为空。" };

        try
        {
            await playlistClient.CreatePlaylistAsync(targetPlaylistName);
            var target = await WaitForCreatedPlaylistAsync(targetPlaylistName, cancellationToken);

            if (target == null)
                return new NeteasePlaylistImportResult { ErrorMessage = "创建成功，但未找到目标歌单，请刷新后重试。" };

            var sourceSongNames = parseResult.SongNames.AsEnumerable().Reverse().ToList();
            var total = sourceSongNames.Count;
            var matchedSongs = new List<(string SourceName, string Name, string Hash, string AlbumId, string MixSongId)>();
            var failedNames = new List<string>();

            foreach (var songName in sourceSongNames)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var searchResult = await musicClient.SearchAsync(songName);
                    var first = searchResult.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Hash));
                    if (first == null)
                    {
                        failedNames.Add(songName);
                        continue;
                    }

                    matchedSongs.Add((
                        songName,
                        string.IsNullOrWhiteSpace(first.Name) ? songName : first.Name,
                        first.Hash,
                        string.IsNullOrWhiteSpace(first.AlbumId) ? "0" : first.AlbumId,
                        "0"));
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "搜索歌曲失败，已跳过。 song={SongName}", songName);
                    failedNames.Add(songName);
                }
            }

            var imported = 0;
            foreach (var chunk in matchedSongs.Chunk(SongBatchSize))
            {
                try
                {
                    var payload = chunk
                        .Select(x => (x.Name, x.Hash, x.AlbumId, x.MixSongId))
                        .ToList();

                    var addResult = await playlistClient.AddSongsAsync(target.ListId.ToString(), payload);
                    imported += addResult?.AddedSongs.Count ?? 0;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "批量添加歌曲失败。 targetListId={ListId}", target.ListId);
                    failedNames.AddRange(chunk.Select(x => x.SourceName));
                }
            }

            return new NeteasePlaylistImportResult
            {
                Total = total,
                Matched = matchedSongs.Count,
                Imported = imported,
                FailedNames = failedNames.Distinct(StringComparer.Ordinal).ToList()
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "导入网易云歌单失败。 targetPlaylist={TargetPlaylist}", targetPlaylistName);
            return new NeteasePlaylistImportResult { ErrorMessage = $"导入失败：{ex.Message}" };
        }
    }

    private static string? ExtractUrl(string text)
    {
        var match = UrlRegex.Match(text);
        return match.Success ? match.Value.Trim() : text.Trim();
    }

    private async Task<Uri?> ResolveIfShortLinkAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (!string.Equals(uri.Host, "163cn.tv", StringComparison.OrdinalIgnoreCase))
            return uri;

        try
        {
            using var client = httpClientFactory.CreateClient(nameof(NeteasePlaylistImportService));
            using var req = new HttpRequestMessage(HttpMethod.Get, uri);
            using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return resp.RequestMessage?.RequestUri ?? uri;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "解析网易云短链失败。 url={Url}", uri.ToString());
            return null;
        }
    }

    private static bool IsSupportedNeteaseHost(string host)
    {
        return host.EndsWith("music.163.com", StringComparison.OrdinalIgnoreCase)
               || host.Equals("y.music.163.com", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractPlaylistId(Uri uri)
    {
        var queryId = ExtractQueryValue(uri.Query, "id");
        if (!string.IsNullOrWhiteSpace(queryId))
            return queryId;

        var fragment = uri.Fragment.TrimStart('#');
        if (fragment.StartsWith('/'))
            fragment = fragment[1..];

        var fragmentQueryIndex = fragment.IndexOf('?', StringComparison.Ordinal);
        if (fragmentQueryIndex >= 0)
        {
            var fragmentQuery = fragment[(fragmentQueryIndex + 1)..];
            var fragmentId = ExtractQueryValue(fragmentQuery, "id");
            if (!string.IsNullOrWhiteSpace(fragmentId))
                return fragmentId;
        }

        var full = Uri.UnescapeDataString(uri.ToString());
        var regex = Regex.Match(full, @"(?:playlist|songlist)\?id=(\d+)", RegexOptions.IgnoreCase);
        return regex.Success ? regex.Groups[1].Value : null;
    }

    private static string? ExtractQueryValue(string query, string key)
    {
        var q = query.TrimStart('?');
        if (string.IsNullOrWhiteSpace(q))
            return null;

        foreach (var segment in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var index = segment.IndexOf('=', StringComparison.Ordinal);
            if (index <= 0)
                continue;

            var currentKey = segment[..index];
            if (!string.Equals(currentKey, key, StringComparison.OrdinalIgnoreCase))
                continue;

            var value = segment[(index + 1)..];
            return string.IsNullOrWhiteSpace(value) ? null : Uri.UnescapeDataString(value);
        }

        return null;
    }

    private async Task<NeteasePlaylistParseResult> LoadPlaylistByIdAsync(
        string playlistId,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = httpClientFactory.CreateClient(nameof(NeteasePlaylistImportService));
            client.DefaultRequestHeaders.Referrer = new Uri("https://music.163.com/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0 Safari/537.36");

            using var requestContent = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["id"] = playlistId
            });
            using var response = await client.PostAsync(NeteasePlaylistDetailApi, requestContent, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(content);

            if (!doc.RootElement.TryGetProperty("playlist", out var playlist))
                return new NeteasePlaylistParseResult { ErrorMessage = "网易云响应格式异常，未找到歌单信息。" };

            var sourcePlaylistName = playlist.TryGetProperty("name", out var playlistNameEl)
                ? playlistNameEl.GetString() ?? "导入歌单"
                : "导入歌单";

            var trackIds = new List<long>();
            if (playlist.TryGetProperty("trackIds", out var trackIdsEl) && trackIdsEl.ValueKind == JsonValueKind.Array)
                foreach (var track in trackIdsEl.EnumerateArray())
                    if (track.TryGetProperty("id", out var idEl) && idEl.TryGetInt64(out var id))
                        trackIds.Add(id);

            var songs = trackIds.Count > 0
                ? await LoadSongNamesByTrackIdsAsync(client, trackIds, cancellationToken)
                : LoadSongNamesFromTracksFallback(playlist);

            songs = songs.Distinct(StringComparer.Ordinal).ToList();
            if (songs.Count == 0)
                return new NeteasePlaylistParseResult { ErrorMessage = "该歌单未解析到歌曲名称，可能是私密歌单或接口受限。" };

            return new NeteasePlaylistParseResult
            {
                Success = true,
                SourcePlaylistName = sourcePlaylistName,
                SongNames = songs
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "加载网易云歌单失败。 playlistId={PlaylistId}", playlistId);
            return new NeteasePlaylistParseResult { ErrorMessage = $"解析失败：{ex.Message}" };
        }
    }

    private static List<string> LoadSongNamesFromTracksFallback(JsonElement playlist)
    {
        var songs = new List<string>();
        if (playlist.TryGetProperty("tracks", out var tracksEl) && tracksEl.ValueKind == JsonValueKind.Array)
            foreach (var track in tracksEl.EnumerateArray())
                if (track.TryGetProperty("name", out var songNameEl))
                {
                    var name = songNameEl.GetString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(name))
                        songs.Add(name);
                }

        return songs;
    }

    private async Task<List<string>> LoadSongNamesByTrackIdsAsync(
        HttpClient client,
        List<long> trackIds,
        CancellationToken cancellationToken)
    {
        var songs = new List<string>(trackIds.Count);

        foreach (var chunk in trackIds.Chunk(NeteaseSongDetailChunkSize))
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["c"] = BuildSongDetailPayload(chunk)
            });
            using var resp = await client.PostAsync(NeteaseSongDetailApi, form, cancellationToken);
            resp.EnsureSuccessStatusCode();

            var body = await resp.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(body);

            if (!doc.RootElement.TryGetProperty("songs", out var songsEl) || songsEl.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var song in songsEl.EnumerateArray())
                if (song.TryGetProperty("name", out var nameEl))
                {
                    var name = nameEl.GetString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(name))
                        songs.Add(name);
                }
        }

        return songs;
    }

    private static string BuildSongDetailPayload(long[] ids)
    {
        var sb = new StringBuilder();
        sb.Append('[');
        for (var i = 0; i < ids.Length; i++)
        {
            if (i > 0)
                sb.Append(',');
            sb.Append("{\"id\":");
            sb.Append(ids[i]);
            sb.Append('}');
        }

        sb.Append(']');
        return sb.ToString();
    }

    private async Task<KuGou.Net.Abstractions.Models.UserPlaylistItem?> WaitForCreatedPlaylistAsync(
        string targetPlaylistName,
        CancellationToken cancellationToken)
    {
        const int maxRetries = 8;
        var delay = TimeSpan.FromMilliseconds(500);

        for (var i = 0; i < maxRetries; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var playlists = await userClient.GetPlaylistsAsync();
            var target = playlists?.Playlists?
                .Where(x => !string.IsNullOrWhiteSpace(x.ListCreateId) &&
                            string.Equals(x.Name, targetPlaylistName, StringComparison.Ordinal))
                .OrderByDescending(x => x.ListId)
                .FirstOrDefault();

            if (target != null)
                return target;

            await Task.Delay(delay, cancellationToken);
        }

        return null;
    }
}
