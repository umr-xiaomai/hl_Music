using System.Text.Json;
using System.Text.Json.Nodes;
using KuGou.Net.Infrastructure.Http;
using KuGou.Net.Protocol.Transport;
using KuGou.Net.util;

namespace KuGou.Net.Protocol.Raw;

public class RawDiscoveryApi(IKgTransport transport)
{
    /// <summary>
    ///     获取推荐歌单
    /// </summary>
    /// <param name="categoryId">0: 推荐, 11292: HI-RES</param>
    public async Task<JsonElement> GetRecommendedPlaylistsAsync(
        string userid,
        string dfid,
        int categoryId = 0,
        int page = 1,
        int pageSize = 30)
    {
        var clientTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var key = KgSigner.CalcLoginKey(clientTime);

        // 2. 计算 mid 
        var mid = KgUtils.Md5(string.IsNullOrEmpty(dfid) ? "-" : dfid);

        // 3. 构建内部对象 special_recommend
        var specialRecommend = new JsonObject
        {
            ["withtag"] = 1,
            ["withsong"] = 0,
            ["sort"] = 1,
            ["ugc"] = 1,
            ["is_selected"] = 0,
            ["withrecommend"] = 1,
            ["area_code"] = 1,
            ["categoryid"] = categoryId
        };

        // 4. 构建主 Body
        var body = new JsonObject
        {
            ["appid"] = KuGouConfig.AppId,
            ["mid"] = mid,
            ["clientver"] = KuGouConfig.ClientVer,
            ["platform"] = "android",
            ["clienttime"] = clientTime,
            ["userid"] = userid,
            ["module_id"] = 1,
            ["page"] = page,
            ["pagesize"] = pageSize,
            ["key"] = key,
            ["special_recommend"] = specialRecommend,
            ["req_multi"] = 1,
            ["retrun_min"] = 5,
            ["return_special_falg"] = 1
        };

        var request = new KgRequest
        {
            Method = HttpMethod.Post,
            Path = "/v2/special_recommend",
            Body = body,
            SpecificRouter = "specialrec.service.kugou.com",
            SignatureType = SignatureType.Default
        };

        return await transport.SendAsync(request);
    }

    /// <summary>
    ///     新歌速递
    /// </summary>
    /// <param name="rankId">默认 21608 (华语新歌?)</param>
    public async Task<JsonElement> GetNewSongsAsync(
        string userid,
        int rankId = 21608,
        int page = 1,
        int pageSize = 30)
    {
        var body = new JsonObject
        {
            ["rank_id"] = rankId,
            ["userid"] = userid,
            ["page"] = page,
            ["pagesize"] = pageSize,
            ["tags"] = new JsonArray()
        };

        var request = new KgRequest
        {
            Method = HttpMethod.Post,
            Path = "/musicadservice/container/v1/newsong_publish",
            Body = body,
            SignatureType = SignatureType.Default
        };

        return await transport.SendAsync(request);
    }


    /// <summary>
    ///     获取每日推荐
    /// </summary>
    public async Task<JsonElement> GetRecommendSongAsync(string? userid)
    {
        var body = new JsonObject
        {
            ["platform"] = "android",
            ["userid"] = userid ?? "0"
        };

        var request = new KgRequest
        {
            Method = HttpMethod.Post,
            Path = "/everyday_song_recommend",
            Body = body,
            SignatureType = SignatureType.Default,
            SpecificRouter = "everydayrec.service.kugou.com"
        };
        return await transport.SendAsync(request);
    }


    /// <summary>
    ///     获取每日风格推荐
    /// </summary>
    public async Task<JsonElement> GetRecommendStyleSongAsync()
    {
        var body = new JsonObject
        {
            ["platform"] = "android"
        };
        var request = new KgRequest
        {
            Method = HttpMethod.Post,
            Path = "/everydayrec.service/everyday_style_recommend",
            Body = body,
            SignatureType = SignatureType.Default,
            Params = new Dictionary<string, string>
            {
                { "tagids", "" }
            }
        };
        return await transport.SendAsync(request);
    }
    
    /// <summary>
    /// 获取私人推荐 (私人FM / 电台) 及 行为上报
    /// </summary>
    public async Task<JsonElement> GetPersonalRecommendAsync(
        string userid, 
        string token, 
        string vipType, 
        string mid,
        string? hash = null, 
        string? songid = null, 
        int? playtime = null,
        string action = "play",
        int songPoolId = 0,
        int remainSongCount = 0,
        bool isOverplay = false,
        string mode = "normal")
    {
        var clientTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); 
        var key = KgSigner.CalcLoginKey(clientTimeMs);

        var body = new JsonObject
        {
            ["appid"] = KuGouConfig.AppId,
            ["clienttime"] = clientTimeMs,
            ["mid"] = string.IsNullOrEmpty(mid) ? "-" : mid,
            ["action"] = action, 
            ["recommend_source_locked"] = 0,
            ["song_pool_id"] = songPoolId, 
            ["callerid"] = 0,
            ["m_type"] = 1,
            ["platform"] = "android", 
            ["area_code"] = 1,
            ["remain_songcnt"] = remainSongCount, 
            ["clientver"] = KuGouConfig.ClientVer,["is_overplay"] = isOverplay ? 1 : 0,
            ["mode"] = mode, 
            ["fakem"] = "ca981cfc583a4c37f28d2d49000013c16a0a",
            ["key"] = key
        };

        if (!string.IsNullOrEmpty(userid) && userid != "0")
        {
            body["userid"] = userid;
            body["kguid"] = userid;
        }

        if (!string.IsNullOrEmpty(token)) body["token"] = token;
        if (!string.IsNullOrEmpty(vipType)) body["vip_type"] = vipType;

        if (!string.IsNullOrEmpty(hash)) body["hash"] = hash;
        if (!string.IsNullOrEmpty(songid)) body["songid"] = songid;
        if (playtime.HasValue) body["playtime"] = playtime.Value;

        var request = new KgRequest
        {
            Method = HttpMethod.Post,
            Path = "/v2/personal_recommend",
            Body = body,
            SpecificRouter = "persnfm.service.kugou.com",
            SignatureType = SignatureType.Default
        };

        return await transport.SendAsync(request);
    }
}