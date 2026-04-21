using KuGou.Net.Clients;
using Microsoft.AspNetCore.Mvc;

namespace KgWebApi.Net.Controllers;

[ApiController]
[Route("[controller]")]
public class DiscoveryController(DiscoveryClient discoveryClient) : ControllerBase
{
    /// <summary>
    ///     歌单推荐
    /// </summary>
    [HttpGet("playlist/recommend")]
    public async Task<IActionResult> GetRecommendPlaylist(
        [FromQuery] int category_id = 0,
        [FromQuery] int page = 1)
    {
        var res = await discoveryClient.GetRecommendedPlaylistsAsync(category_id, page);
        return Ok(res);
    }

    /// <summary>
    ///     新歌速递
    /// </summary>
    [HttpGet("newsong")]
    public async Task<IActionResult> GetNewSong(
        [FromQuery] int type = 21608,
        [FromQuery] int page = 1)
    {
        var res = await discoveryClient.GetNewSongsAsync(type, page);
        return Ok(res);
    }


    [HttpGet("RecommendSong")]
    public async Task<IActionResult> GetRecommendSong()
    {
        var res = await discoveryClient.GetRecommendedSongsAsync();
        return Ok(res);
    }


    [HttpGet("RecommendStyleSong")]
    public async Task<IActionResult> GetRecommendStyleSong()
    {
        var res = await discoveryClient.GetRecommendedStyleSongsAsync();
        return Ok(res);
    }
    
    /// <summary>
    /// 获取私人FM推荐 / 上报电台播放行为
    /// </summary>
    /// <param name="hash">音乐 hash</param>
    /// <param name="songid">音乐 songid</param>
    /// <param name="playtime">已播放时间 (秒)</param>
    /// <param name="action">行为: play (获取推荐), garbage (标记为不喜欢)</param>
    /// <param name="mode">模式: normal (发现), small (小众), peak (30s 高潮)</param>
    /// <param name="songPoolId">AI 策略池: 0 (口味Alpha), 1 (风格Beta), 2 (Gamma)</param>
    /// <param name="isOverplay">该歌曲是否自然播放到结束</param>
    /// <param name="remainSongCnt">列表剩余歌曲数 (如果填5，只会告诉服务器你的行为，不会返回新歌节省带宽)</param>
    [HttpGet("personal_fm")]
    public async Task<IActionResult> GetPersonalFm([FromQuery] string? hash = null,
        [FromQuery] string? songid = null,
        [FromQuery] int? playtime = null,
        [FromQuery] string action = "play",
        [FromQuery] string mode = "normal",
        [FromQuery] int songPoolId = 0,
        [FromQuery] bool isOverplay = false,
        [FromQuery] int remainSongCnt = 0)
    {
        var res = await discoveryClient.GetPersonalRecommendFMAsync(
            hash, songid, playtime, action, mode, songPoolId, isOverplay, remainSongCnt);
            
        return Ok(res);
    }
}