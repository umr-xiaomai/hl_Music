using KuGou.Net.Clients;
using Microsoft.Extensions.Logging;

namespace KugouAvaloniaPlayer.ViewModels;

public interface ISingerViewModelFactory
{
    SingerViewModel Create(string authorId, string singerName);
}

public sealed class SingerViewModelFactory(MusicClient musicClient, ILogger<SingerViewModel> logger)
    : ISingerViewModelFactory
{
    public SingerViewModel Create(string authorId, string singerName)
    {
        return new SingerViewModel(musicClient, logger, authorId, singerName);
    }
}

public interface IDesktopLyricViewModelFactory
{
    DesktopLyricViewModel Create();
}

public sealed class DesktopLyricViewModelFactory(PlayerViewModel playerViewModel) : IDesktopLyricViewModelFactory
{
    public DesktopLyricViewModel Create()
    {
        return new DesktopLyricViewModel(playerViewModel);
    }
}