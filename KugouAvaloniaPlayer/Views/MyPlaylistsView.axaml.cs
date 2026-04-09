using System.Linq;
using Avalonia.Controls;
using Avalonia.VisualTree;
using KugouAvaloniaPlayer.ViewModels;

namespace KugouAvaloniaPlayer.Views;

public partial class MyPlaylistsView : UserControl
{
    public MyPlaylistsView()
    {
        InitializeComponent();
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        var scrollViewer = ResolveScrollViewer(sender, e);
        if (scrollViewer == null) return;
        if (DataContext is not MyPlaylistsViewModel vm) return;

        var currentBottom = scrollViewer.Offset.Y + scrollViewer.Viewport.Height;

        if (currentBottom >= scrollViewer.Extent.Height - 50)
            if (vm.LoadMoreCommand.CanExecute(null))
                vm.LoadMoreCommand.Execute(null);
    }

    private static ScrollViewer? ResolveScrollViewer(object? sender, ScrollChangedEventArgs e)
    {
        if (e.Source is ScrollViewer eventScrollViewer)
            return eventScrollViewer;
        if (sender is ScrollViewer senderScrollViewer)
            return senderScrollViewer;
        if (sender is Control control)
            return control.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();

        return null;
    }
}