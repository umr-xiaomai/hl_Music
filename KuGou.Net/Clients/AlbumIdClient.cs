using KuGou.Net.Abstractions.Models;
using KuGou.Net.Adapters.Common;
using KuGou.Net.Protocol.Raw;
using KuGou.Net.util;

namespace KuGou.Net.Clients;

public class AlbumClient(RawAlbumApi rawApi)
{
    public async Task<List<AlbumSongItem>?> GetSongsAsync(string albumId, int page = 1, int pageSize = 30)
    {
        var json = await rawApi.GetAlbumSongAsync(albumId, page, pageSize);

        var response = KgApiResponseParser.Parse<AlbumSongResponse>(json, AppJsonContext.Default.AlbumSongResponse);
        return response?.Songs;
    }
}