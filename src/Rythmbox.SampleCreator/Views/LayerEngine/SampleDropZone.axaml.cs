using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Rythmbox.SampleCreator.ViewModels;

namespace Rythmbox.SampleCreator.Views.LayerEngine;

/// <summary>
/// Per-layer WAV drop target. Dropping files appends them as round robins on the owning
/// velocity layer through the existing import path; clicking Import opens the file picker.
/// </summary>
public partial class SampleDropZone : UserControl
{
    public SampleDropZone()
    {
        InitializeComponent();
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.Contains(DataFormat.File))
        {
            e.DragEffects = DragDropEffects.Copy;
            Zone.Classes.Set("dragover", true);
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void OnDragLeave(object? sender, DragEventArgs e) => Zone.Classes.Set("dragover", false);

    private void OnDrop(object? sender, DragEventArgs e)
    {
        Zone.Classes.Set("dragover", false);

        if (DataContext is not VelocityLayerViewModel layer || !e.DataTransfer.Contains(DataFormat.File))
        {
            return;
        }

        var paths = new List<string>();
        foreach (var item in e.DataTransfer.Items)
        {
            if (item.TryGetFile() is { } file && file.TryGetLocalPath() is { } path)
            {
                paths.Add(path);
            }
        }

        if (paths.Count == 0)
        {
            return;
        }

        layer.Pad.ImportFilesToLayer(layer, paths);
        e.Handled = true;
    }
}
