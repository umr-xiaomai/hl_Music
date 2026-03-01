using System.Text.Json;
using System.Text.Json.Nodes;
using KuGou.Net.Infrastructure.Http;
using KuGou.Net.Protocol.Transport;

namespace KuGou.Net.Protocol.Raw;

public class RawAlbumApi(IKgTransport transport)
{
    public async Task<JsonElement> GetAlbumInfoAsync(string albumId)
    {
        var body = new JsonObject
        {
            ["album_id"] = albumId,
            ["is_buy"] = 0,
            ["fields"] =
                "album_id,album_name,publish_date,sizable_cover,intro,language,is_publish,heat,type,quality,authors,exclusive,author_name,trans_param"
        };

        var request = new KgRequest
        {
            Method = HttpMethod.Post,
            Path = "/kmr/v2/albums",
            Body = body,
            SpecificRouter = "openapi.kugou.com",
            SignatureType = SignatureType.Default,
            CustomHeaders = new Dictionary<string, string>
            {
                { "kg-tid", "255" }
            }
        };

        return await transport.SendAsync(request);
    }

    public async Task<JsonElement> GetAlbumSongAsync(string albumId, int page, int pageSize)
    {
        var body = new JsonObject
        {
            ["album_id"] = albumId,
            ["is_buy"] = 0,
            ["page"] = page,
            ["pagesize"] = pageSize
        };

        var request = new KgRequest
        {
            Method = HttpMethod.Post,
            Path = "/v1/album_audio/lite",
            Body = body,
            SpecificRouter = "openapi.kugou.com",
            SignatureType = SignatureType.Default,
            CustomHeaders = new Dictionary<string, string>
            {
                { "kg-tid", "255" }
            }
        };

        return await transport.SendAsync(request);
    }
}