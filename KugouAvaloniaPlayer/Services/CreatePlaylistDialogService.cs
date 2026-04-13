using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using SukiUI.Dialogs;

namespace KugouAvaloniaPlayer.Services;

public interface ICreatePlaylistDialogService
{
    Task<string?> PromptPlaylistNameAsync();
}

public sealed class CreatePlaylistDialogService(ISukiDialogManager dialogManager) : ICreatePlaylistDialogService
{
    public Task<string?> PromptPlaylistNameAsync()
    {
        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        void ShowDialog()
        {
            var textBox = new TextBox
            {
                Watermark = "请输入歌单名称",
                Width = 300
            };

            dialogManager.CreateDialog()
                .WithTitle("新建歌单")
                .WithContent(textBox)
                .WithActionButton("取消", _ => { tcs.TrySetResult(null); }, true, "Standard")
                .WithActionButton("创建", _ =>
                {
                    var name = textBox.Text?.Trim();
                    tcs.TrySetResult(string.IsNullOrWhiteSpace(name) ? null : name);
                }, true, "Standard")
                .TryShow();
        }

        if (Dispatcher.UIThread.CheckAccess())
            ShowDialog();
        else
            Dispatcher.UIThread.Post(ShowDialog);

        return tcs.Task;
    }
}
