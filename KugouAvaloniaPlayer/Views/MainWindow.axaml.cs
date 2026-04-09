using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using KugouAvaloniaPlayer.Services;
using KugouAvaloniaPlayer.ViewModels;
using SukiUI.Controls;

namespace KugouAvaloniaPlayer.Views;

public partial class MainWindow : SukiWindow
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public bool CanClose { get; set; }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        var behavior = SettingsManager.Settings.CloseBehavior;
        if (behavior == CloseBehavior.MinimizeToTray && !CanClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        if (DataContext is MainWindowViewModel vm) vm.ForceCloseDesktopLyric();

        base.OnClosing(e);
    }

    private void OnBackdropPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm) vm.IsQueuePaneOpen = false;
    }

    private void TextBox_KeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            if (DataContext is MainWindowViewModel vm && vm.SearchCommand.CanExecute(null))
                vm.SearchCommand.Execute(null);
    }

    private void OnSidebarAddPlaylistClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control || DataContext is not MainWindowViewModel vm) return;

        var contextMenu = new ContextMenu();

        var addLocalItem = new MenuItem { Header = "导入本地文件夹..." };
        addLocalItem.Click += AddLocalFolder_Click;

        var addOnlineItem = new MenuItem { Header = "新建歌单..." };
        addOnlineItem.Click += (_, _) => vm.PlaylistsViewModel.ShowCreatePlaylistDialog();

        contextMenu.Items.Add(addLocalItem);
        contextMenu.Items.Add(addOnlineItem);
        contextMenu.Open(control);
    }

    private async void AddLocalFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择本地音乐文件夹",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            var path = folders[0].Path.LocalPath;
            vm.PlaylistsViewModel.AddLocalPlaylist(path);
        }
    }

    private void OnDeleteLocalPlaylistClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is PlaylistItem item &&
            DataContext is MainWindowViewModel vm)
            vm.PlaylistsViewModel.DeleteLocalPlaylistCommand.Execute(item);
    }

    private void OnDeleteOnlinePlaylistClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is PlaylistItem item &&
            DataContext is MainWindowViewModel vm)
            vm.PlaylistsViewModel.DeleteOnlinePlaylistCommand.Execute(item);
    }
}