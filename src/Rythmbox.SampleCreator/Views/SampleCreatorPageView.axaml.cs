using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Rythmbox.SampleCreator.ViewModels;

namespace Rythmbox.SampleCreator.Views;

public partial class SampleCreatorPageView : UserControl
{
    public SampleCreatorPageView()
    {
        InitializeComponent();
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.Contains(DataFormat.File))
        {
            e.DragEffects = DragDropEffects.Copy;
        }
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not SampleCreatorViewModel viewModel || !e.DataTransfer.Contains(DataFormat.File))
        {
            return;
        }

        var paths = new List<string>();
        foreach (var item in e.DataTransfer.Items)
        {
            if (item.TryGetFile() is not { } file)
            {
                continue;
            }

            var path = file.TryGetLocalPath();
            if (path is not null)
            {
                paths.Add(path);
            }
        }

        if (paths.Count == 0)
        {
            return;
        }

        viewModel.ImportDroppedFiles(paths);
        e.Handled = true;
    }

    private void OnPageKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not SampleCreatorViewModel viewModel || e.Source is TextBox)
        {
            return;
        }

        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);

        switch (e.Key)
        {
            case Key.Space when viewModel.SelectedPad is not null:
                viewModel.SelectedPad.PreviewCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Escape:
                viewModel.StopPreviewCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.O when ctrl:
                viewModel.BrowseOpenPresetCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.S when ctrl:
                viewModel.BrowseSaveKitCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.I:
                viewModel.BrowseImportWavCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.N when ctrl:
                viewModel.NewKitCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}
