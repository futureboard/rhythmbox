using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Rythmbox.SampleCreator.ViewModels;

namespace Rythmbox.SampleCreator.Views.LayerEngine;

/// <summary>
/// Renders the 1–127 velocity range as horizontal segmented blocks — one block per
/// velocity layer. The selected layer is highlighted, gaps and overlaps in the current
/// data are shown visually, and clicking a segment raises <see cref="SelectionCommand"/>.
/// This is a lightweight custom-drawn control (same approach as WaveformCanvas): it only
/// invalidates on the handful of properties that affect the picture, never a full retemplate.
/// </summary>
public sealed class VelocityMapControl : Control
{
    private const int MinVelocity = 1;
    private const int MaxVelocity = 127;

    private static readonly IBrush TrackBrush = new SolidColorBrush(Color.Parse("#0D100E"));
    private static readonly IBrush GapBrush = new SolidColorBrush(Color.Parse("#191E1A"));
    private static readonly IPen GapBorderPen = new Pen(new SolidColorBrush(Color.Parse("#2B302C")), 1) { DashStyle = new DashStyle([2, 2], 0) };
    private static readonly IBrush LayerBrush = new SolidColorBrush(Color.Parse("#26302A"));
    private static readonly IPen LayerBorderPen = new Pen(new SolidColorBrush(Color.Parse("#404640")), 1);
    private static readonly IBrush SelectedBrush = new SolidColorBrush(Color.Parse("#3D3018"));
    private static readonly IPen SelectedBorderPen = new Pen(new SolidColorBrush(Color.Parse("#F5A000")), 1.5);
    private static readonly IBrush OverlapBrush = new SolidColorBrush(Color.FromArgb(70, 229, 69, 59));
    private static readonly IBrush LabelBrush = new SolidColorBrush(Color.Parse("#F1F3EF"));
    private static readonly IBrush DimLabelBrush = new SolidColorBrush(Color.Parse("#8D978E"));

    public static readonly StyledProperty<IEnumerable?> LayersProperty =
        AvaloniaProperty.Register<VelocityMapControl, IEnumerable?>(nameof(Layers));

    public static readonly StyledProperty<VelocityLayerViewModel?> SelectedLayerProperty =
        AvaloniaProperty.Register<VelocityMapControl, VelocityLayerViewModel?>(nameof(SelectedLayer));

    public static readonly StyledProperty<ICommand?> SelectionCommandProperty =
        AvaloniaProperty.Register<VelocityMapControl, ICommand?>(nameof(SelectionCommand));

    private INotifyCollectionChanged? _observedCollection;
    private readonly List<INotifyPropertyChanged> _subscribedItems = [];

    static VelocityMapControl()
    {
        AffectsRender<VelocityMapControl>(LayersProperty, SelectedLayerProperty);
    }

    public VelocityMapControl()
    {
        MinHeight = 44;
    }

    public IEnumerable? Layers
    {
        get => GetValue(LayersProperty);
        set => SetValue(LayersProperty, value);
    }

    public VelocityLayerViewModel? SelectedLayer
    {
        get => GetValue(SelectedLayerProperty);
        set => SetValue(SelectedLayerProperty, value);
    }

    public ICommand? SelectionCommand
    {
        get => GetValue(SelectionCommandProperty);
        set => SetValue(SelectionCommandProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LayersProperty)
        {
            DetachCollection();
            AttachCollection(change.GetNewValue<IEnumerable?>());
            InvalidateVisual();
        }
    }

    private void AttachCollection(IEnumerable? layers)
    {
        if (layers is INotifyCollectionChanged incc)
        {
            _observedCollection = incc;
            incc.CollectionChanged += OnCollectionChanged;
        }

        if (layers is not null)
        {
            foreach (var item in layers)
            {
                SubscribeItem(item);
            }
        }
    }

    private void DetachCollection()
    {
        if (_observedCollection is not null)
        {
            _observedCollection.CollectionChanged -= OnCollectionChanged;
            _observedCollection = null;
        }

        // Track subscriptions explicitly: a Reset (ObservableCollection.Clear) carries no
        // OldItems, so this is the only reliable way to avoid leaking layer subscriptions.
        foreach (var item in _subscribedItems)
        {
            item.PropertyChanged -= OnLayerPropertyChanged;
        }

        _subscribedItems.Clear();
    }

    private void SubscribeItem(object? item)
    {
        if (item is INotifyPropertyChanged inpc)
        {
            inpc.PropertyChanged += OnLayerPropertyChanged;
            _subscribedItems.Add(inpc);
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var item in _subscribedItems)
            {
                item.PropertyChanged -= OnLayerPropertyChanged;
            }

            _subscribedItems.Clear();
        }
        else if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems)
            {
                if (item is INotifyPropertyChanged inpc)
                {
                    inpc.PropertyChanged -= OnLayerPropertyChanged;
                    _subscribedItems.Remove(inpc);
                }
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems)
            {
                SubscribeItem(item);
            }
        }

        if (sender is IEnumerable current && e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var item in current)
            {
                SubscribeItem(item);
            }
        }

        InvalidateVisual();
    }

    private void OnLayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(VelocityLayerViewModel.VelocityLow)
            or nameof(VelocityLayerViewModel.VelocityHigh)
            or nameof(VelocityLayerViewModel.IsSelected)
            or nameof(VelocityLayerViewModel.RoundRobinCountLabel))
        {
            InvalidateVisual();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Layers is null || Bounds.Width <= 0)
        {
            return;
        }

        var x = e.GetPosition(this).X;
        var velocity = (int)Math.Clamp(MinVelocity + x / Bounds.Width * (MaxVelocity - MinVelocity + 1), MinVelocity, MaxVelocity);

        foreach (var item in Layers)
        {
            if (item is VelocityLayerViewModel layer
                && velocity >= (int)layer.VelocityLow
                && velocity <= (int)layer.VelocityHigh)
            {
                if (SelectionCommand is { } command && command.CanExecute(layer))
                {
                    command.Execute(layer);
                }

                e.Handled = true;
                return;
            }
        }
    }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        var width = bounds.Width;
        var height = bounds.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        // Track + axis labels.
        context.FillRectangle(TrackBrush, new Rect(0, 0, width, height));

        var span = MaxVelocity - MinVelocity + 1;
        double XForVelocity(int velocity) => (double)(velocity - MinVelocity) / span * width;

        var coverage = new int[MaxVelocity + 1];
        if (Layers is not null)
        {
            foreach (var item in Layers)
            {
                if (item is not VelocityLayerViewModel layer)
                {
                    continue;
                }

                var low = Math.Clamp((int)layer.VelocityLow, MinVelocity, MaxVelocity);
                var high = Math.Clamp((int)layer.VelocityHigh, low, MaxVelocity);
                for (var v = low; v <= high; v++)
                {
                    coverage[v]++;
                }
            }
        }

        // Gaps: uncovered velocity ranges get a dashed placeholder so missing zones are obvious.
        DrawRunsWhere(coverage, static c => c == 0, (startV, endV) =>
        {
            var rect = new Rect(XForVelocity(startV), 0, XForVelocity(endV + 1) - XForVelocity(startV), height);
            context.FillRectangle(GapBrush, rect);
            context.DrawRectangle(null, GapBorderPen, rect.Deflate(0.5));
        });

        // Layer blocks.
        if (Layers is not null)
        {
            foreach (var item in Layers)
            {
                if (item is not VelocityLayerViewModel layer)
                {
                    continue;
                }

                var low = Math.Clamp((int)layer.VelocityLow, MinVelocity, MaxVelocity);
                var high = Math.Clamp((int)layer.VelocityHigh, low, MaxVelocity);
                var left = XForVelocity(low);
                var right = XForVelocity(high + 1);
                var rect = new Rect(left + 1, 4, Math.Max(2, right - left - 2), height - 8);

                context.DrawRectangle(
                    layer.IsSelected ? SelectedBrush : LayerBrush,
                    layer.IsSelected ? SelectedBorderPen : LayerBorderPen,
                    rect,
                    3,
                    3);

                if (rect.Width > 26)
                {
                    var text = new FormattedText(
                        (layer.Index + 1).ToString(),
                        System.Globalization.CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        Typeface.Default,
                        11,
                        layer.IsSelected ? LabelBrush : DimLabelBrush);
                    context.DrawText(text, new Point(rect.X + 5, rect.Center.Y - text.Height / 2));
                }
            }
        }

        // Overlaps: velocities covered by more than one layer are flagged in red.
        DrawRunsWhere(coverage, static c => c >= 2, (startV, endV) =>
        {
            var rect = new Rect(XForVelocity(startV), 2, XForVelocity(endV + 1) - XForVelocity(startV), height - 4);
            context.FillRectangle(OverlapBrush, rect);
        });

        // 1 / 127 axis ticks.
        var low1 = new FormattedText("1", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 8, DimLabelBrush);
        context.DrawText(low1, new Point(2, height - low1.Height - 1));
        var high127 = new FormattedText("127", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 8, DimLabelBrush);
        context.DrawText(high127, new Point(width - high127.Width - 2, height - high127.Height - 1));
    }

    private static void DrawRunsWhere(int[] coverage, Func<int, bool> predicate, Action<int, int> draw)
    {
        var start = -1;
        for (var v = MinVelocity; v <= MaxVelocity; v++)
        {
            if (predicate(coverage[v]))
            {
                if (start < 0)
                {
                    start = v;
                }
            }
            else if (start >= 0)
            {
                draw(start, v - 1);
                start = -1;
            }
        }

        if (start >= 0)
        {
            draw(start, MaxVelocity);
        }
    }
}
