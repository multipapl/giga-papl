using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BlenderToolbox.Tools.RenderManager.ViewModels;

namespace BlenderToolbox.Tools.RenderManager.Views;

public partial class RenderManagerView : UserControl
{
    private Point _dragStartPoint;

    public RenderManagerView()
    {
        InitializeComponent();
    }

    private void LogTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            textBox.ScrollToEnd();
        }
    }

    private void QueueGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void QueueGrid_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        if (e.OriginalSource is DependencyObject source && FindAncestor<CheckBox>(source) is not null)
        {
            return;
        }

        var diff = _dragStartPoint - e.GetPosition(null);
        if (Math.Abs(diff.X) <= SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) <= SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var row = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject);
        if (row?.Item is not RenderQueueItemViewModel)
        {
            return;
        }

        DragDrop.DoDragDrop(row, new DataObject(typeof(RenderQueueItemViewModel), row.Item), DragDropEffects.Move);
    }

    private void QueueGrid_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(RenderQueueItemViewModel)) is not RenderQueueItemViewModel droppedItem)
        {
            return;
        }

        var targetRow = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject);
        if (targetRow?.Item is not RenderQueueItemViewModel targetItem || droppedItem == targetItem)
        {
            return;
        }

        if (DataContext is not RenderManagerViewModel vm)
        {
            return;
        }

        var oldIndex = vm.Jobs.IndexOf(droppedItem);
        var newIndex = vm.Jobs.IndexOf(targetItem);
        if (oldIndex >= 0 && newIndex >= 0)
        {
            vm.Jobs.Move(oldIndex, newIndex);
        }
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
