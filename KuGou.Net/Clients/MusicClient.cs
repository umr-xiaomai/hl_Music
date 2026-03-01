using System.Text.Json;
using KuGou.Net.Abstractions.Models;
using KuGou.Net.Adapters.Common;
using KuGou.Net.Protocol.Raw;
using KuGou.Net.Protocol.Session;
using KuGou.Net.util;

namespace KuGou.Net.Clients;

public class MusicClient(RawSearchApi rawApi, KgSessionManager sessionManager)
{
    public async Task<List<SongInfo>> SearchAsync(string keyword, int page = 1, string type = "song")
    {
        var json = await rawApi.SearchAsync(keyword, page, 30, type);

        var data = KgApiResponseParser.Parse<SearchResultData>(json, AppJsonContext.Default.SearchResultData);

        if (data?.Songs == null) return new List<SongInfo>();

        return data.Songs;
    }

    public async Task<PlayUrlData?> GetPlayInfoAsync(string hash, string? quality = null)
    {
        var json = await rawApi.GetPlayUrlAsync(hash, quality);

        var result = json.Deserialize(AppJsonContext.Default.PlayUrlData);

        return result ?? new PlayUrlData { Status = 0 };
    }


    public async Task<SearchHotResponse?> GetSearchHotAsync()
    {
        var json = await rawApi.SearchHotAsync();

        var data = KgApiResponseParser.Parse<SearchHotResponse>(
            json,
            AppJsonContext.Default.SearchHotResponse
        );

        return data;
    }


    public async Task<SingerAudioResponse?> GetSingerSongsAsync(
        string authorId,
        int page = 1,
        int pageSize = 30,
        string sort = "new")
    {
        var dfid = sessionManager.Session.Dfid;

        var json = await rawApi.GetSingerSongsAsync(dfid, authorId, page, pageSize, sort);

        var result = json.Deserialize(AppJsonContext.Default.SingerAudioResponse);
        return result;
    }

    public async Task<SingerDetailResponse?> GetSingerDetailAsync(string authorId)
    {
        var json = await rawApi.GetSingerDetailAsync(authorId);

        return KgApiResponseParser.Parse<SingerDetailResponse>(
            json,
            AppJsonContext.Default.SingerDetailResponse
        );
        ;
    }

    public async Task<List<SearchPlaylistItem>?> SearchSpecialAsync(string keyword, int page = 1,
        string type = "special")
    {
        var json = await rawApi.SearchAsync(keyword, page, 30, type);

        var data = KgApiResponseParser.Parse<SearchPlaylistResponse>(
            json,
            AppJsonContext.Default.SearchPlaylistResponse
        );
        return data?.Playlists;
    }

    public async Task<List<SearchAlbumItem>?> SearchAlbumAsync(string keyword, int page = 1, string type = "album")
    {
        var json = await rawApi.SearchAsync(keyword, page, 30, type);

        var data = KgApiResponseParser.Parse<SearchAlbumResponse>(
            json,
            AppJsonContext.Default.SearchAlbumResponse
        );

        return data?.Albums;
    }
}