using KuGou.Net.Clients;
using KuGou.Net.Infrastructure.Http;
using KuGou.Net.Protocol.Raw;
using KuGou.Net.Protocol.Session;
using Microsoft.Extensions.Logging.Abstractions;

namespace KgTest.Services;

internal sealed class TerminalKugouClients
{
    public required KgSessionManager SessionManager { get; init; }
    public required AuthClient Auth { get; init; }
    public required DiscoveryClient Discovery { get; init; }
    public required MusicClient Music { get; init; }
    public required PlaylistClient Playlist { get; init; }
    public required UserClient User { get; init; }
    public required LyricClient Lyric { get; init; }
    public required RankClient Rank { get; init; }
    public required AlbumClient Album { get; init; }
}

internal static class TerminalKugouClientFactory
{
    public static TerminalKugouClients Create()
    {
        var (transport, sessionManager) = KgHttpClientFactory.CreateWithSession(new TerminalSessionPersistence());

        var rawLogin = new RawLoginApi(transport, sessionManager, NullLogger<RawLoginApi>.Instance);
        var rawSearch = new RawSearchApi(transport);
        var rawUser = new RawUserApi(transport);
        var rawPlaylist = new RawPlaylistApi(transport, NullLogger<RawPlaylistApi>.Instance);
        var rawLyric = new RawLyricApi(transport);
        var rawDiscovery = new RawDiscoveryApi(transport);
        var rawRank = new RawRankApi(transport);
        var rawAlbum = new RawAlbumApi(transport);

        return new TerminalKugouClients
        {
            SessionManager = sessionManager,
            Auth = new AuthClient(rawLogin, sessionManager, NullLogger<AuthClient>.Instance),
            Discovery = new DiscoveryClient(rawDiscovery, sessionManager),
            Music = new MusicClient(rawSearch, sessionManager),
            Playlist = new PlaylistClient(rawPlaylist, sessionManager),
            User = new UserClient(rawUser, sessionManager),
            Lyric = new LyricClient(rawLyric),
            Rank = new RankClient(rawRank),
            Album = new AlbumClient(rawAlbum)
        };
    }
}
