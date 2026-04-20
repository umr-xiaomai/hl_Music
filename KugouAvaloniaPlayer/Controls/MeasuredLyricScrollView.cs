using System;
using System.Collections;
using System.Collections.Specialized;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace KugouAvaloniaPlayer.Controls;

public class MeasuredLyricScrollView : ItemsControl
{
    private const int StaggerRange = 5;
    private const int StaggerStepMs = 35;
    private const int EntranceStepMs = 25;
    private static readonly TimeSpan EntranceDuration = TimeSpan.FromMilliseconds(520);
    private const double DefaultEstimatedLineHeight = 72;
    private const double EntranceRiseOffset = 110;

    public static readonly StyledProperty<object?> ActiveItemProperty =
        AvaloniaProperty.Register<MeasuredLyricScrollView, object?>(nameof(ActiveItem));

    public static readonly StyledProperty<double> LineSpacingProperty =
        AvaloniaProperty.Register<MeasuredLyricScrollView, double>(nameof(LineSpacing), 18);

    public static readonly StyledProperty<TimeSpan> ScrollDurationProperty =
        AvaloniaProperty.Register<MeasuredLyricScrollView, TimeSpan>(nameof(ScrollDuration), TimeSpan.FromMilliseconds(420));

    public static readonly StyledProperty<double> WheelStepProperty =
        AvaloniaProperty.Register<MeasuredLyricScrollView, double>(nameof(WheelStep), 80);

    public static readonly StyledProperty<double> ActiveAnchorRatioProperty =
        AvaloniaProperty.Register<MeasuredLyricScrollView, double>(nameof(ActiveAnchorRatio), 0.5);

    private readonly DispatcherTimer _userScrollResetTimer;
    private INotifyCollectionChanged? _collectionChangedSource;
    private bool _deferredActiveItemUpdate;
    private bool _isUserScrolling;
    private int? _lockedActiveIndex;
    private bool _layoutUpdateQueued;
    private double _manualOffset;
    private bool _isFirstLayoutPass = true;
    private readonly Dictionary<int, double> _knownHeights = new();

    public MeasuredLyricScrollView()
    {
        _userScrollResetTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1800) };
        _userScrollResetTimer.Tick += OnUserScrollTimeout;

        LayoutUpdated += OnLayoutUpdated;
    }

    protected override Type StyleKeyOverride => typeof(MeasuredLyricScrollView);

    public object? ActiveItem
    {
        get => GetValue(ActiveItemProperty);
        set => SetValue(ActiveItemProperty, value);
    }

    public double LineSpacing
    {
        get => GetValue(LineSpacingProperty);
        set => SetValue(LineSpacingProperty, value);
    }

    public TimeSpan ScrollDuration
    {
        get => GetValue(ScrollDurationProperty);
        set => SetValue(ScrollDurationProperty, value);
    }

    public double WheelStep
    {
        get => GetValue(WheelStepProperty);
        set => SetValue(WheelStepProperty, value);
    }

    public double ActiveAnchorRatio
    {
        get => GetValue(ActiveAnchorRatioProperty);
        set => SetValue(ActiveAnchorRatioProperty, value);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        UnhookCollectionChanged();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ActiveItemProperty)
        {
            if (_isUserScrolling)
            {
                _deferredActiveItemUpdate = true;
                return;
            }

            QueueLayoutUpdate();
            return;
        }

        if (change.Property == BoundsProperty ||
            change.Property == LineSpacingProperty ||
            change.Property == ScrollDurationProperty ||
            change.Property == ActiveAnchorRatioProperty)
        {
            QueueLayoutUpdate();
            return;
        }

        if (change.Property == ItemsSourceProperty)
        {
            HookCollectionChanged(change.NewValue);
            ResetFirstLayoutState();
            QueueLayoutUpdate();
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (ItemCount == 0) return;

        if (!_isUserScrolling)
            _lockedActiveIndex = GetActiveIndex();

        _isUserScrolling = true;
        _manualOffset += e.Delta.Y * WheelStep;

        _userScrollResetTimer.Stop();
        _userScrollResetTimer.Start();

        QueueLayoutUpdate();
        e.Handled = true;
    }

    private void OnUserScrollTimeout(object? sender, EventArgs e)
    {
        _userScrollResetTimer.Stop();
        _isUserScrolling = false;
        _lockedActiveIndex = null;
        _manualOffset = 0;

        if (_deferredActiveItemUpdate)
            _deferredActiveItemUpdate = false;

        QueueLayoutUpdate();
    }

    private void HookCollectionChanged(object? itemsSource)
    {
        UnhookCollectionChanged();

        if (itemsSource is INotifyCollectionChanged changed)
        {
            _collectionChangedSource = changed;
            _collectionChangedSource.CollectionChanged += OnItemsSourceCollectionChanged;
        }
    }

    private void UnhookCollectionChanged()
    {
        if (_collectionChangedSource == null) return;

        _collectionChangedSource.CollectionChanged -= OnItemsSourceCollectionChanged;
        _collectionChangedSource = null;
    }

    private void OnItemsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ResetFirstLayoutState();
        QueueLayoutUpdate();
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (_isUserScrolling) return;
        QueueLayoutUpdate();
    }

    private void QueueLayoutUpdate()
    {
        if (_layoutUpdateQueued) return;

        _layoutUpdateQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _layoutUpdateQueued = false;
            ApplyMeasuredLayout();
        }, DispatcherPriority.Render);
    }

    private void ApplyMeasuredLayout()
    {
        if (ItemCount == 0 || Bounds.Height <= 0 || Bounds.Width <= 0) return;

        var activeIndex = _isUserScrolling
            ? _lockedActiveIndex ?? GetActiveIndex()
            : GetActiveIndex();
        if (activeIndex < 0 || activeIndex >= ItemCount)
            activeIndex = 0;
        var staggerAnchorIndex = Math.Clamp(activeIndex + 1, 0, ItemCount - 1);

        var heights = new double[ItemCount];
        for (var i = 0; i < ItemCount; i++)
        {
            if (ContainerFromIndex(i) is not Control container) continue;

            var height = container.Bounds.Height;
            if (height <= 0)
                height = container.DesiredSize.Height;
            if (height <= 0)
                height = _knownHeights.GetValueOrDefault(i, DefaultEstimatedLineHeight);
            else
                _knownHeights[i] = height;

            heights[i] = height;
        }

        // Keep the active lyric line on a configurable visual anchor rather than always hard-centering it.
        var centerY = Bounds.Height * Math.Clamp(ActiveAnchorRatio, 0.0, 1.0) + _manualOffset;

        for (var i = 0; i < ItemCount; i++)
        {
            if (ContainerFromIndex(i) is not Control container) continue;

            var isEntrance = _isFirstLayoutPass && !_isUserScrolling;
            var topDelay = _isUserScrolling
                ? TimeSpan.Zero
                : isEntrance
                    ? GetEntranceDelay(i, activeIndex)
                    : GetTopTransitionDelay(i, staggerAnchorIndex);
            var topDuration = _isUserScrolling
                ? TimeSpan.Zero
                : isEntrance
                    ? EntranceDuration
                    : ScrollDuration;
            EnsureTransitions(container, topDelay, topDuration);

            var targetCenterY = centerY;

            if (i < activeIndex)
            {
                for (var j = i; j < activeIndex; j++)
                    targetCenterY -= heights[j] + LineSpacing;
            }
            else if (i > activeIndex)
            {
                for (var j = activeIndex; j < i; j++)
                    targetCenterY += heights[j] + LineSpacing;
            }

            var targetTop = targetCenterY - heights[i] / 2;

            if (isEntrance)
            {
                // 首次加载从下往上依次抬升，避免“先挤成一团再展开”
                SetTopImmediate(container, targetTop + EntranceRiseOffset + Math.Abs(i - activeIndex) * 8);
            }

            Canvas.SetTop(container, targetTop);
            Canvas.SetLeft(container, 0);
            container.Width = Bounds.Width;

            var distance = Math.Abs(i - activeIndex);
            container.Opacity = Math.Clamp(1 - distance * 0.16, 0.24, 1);
        }

        _isFirstLayoutPass = false;
    }

    private int GetActiveIndex()
    {
        if (ActiveItem == null) return -1;

        var index = ItemsView.IndexOf(ActiveItem);
        if (index >= 0) return index;

        if (ItemsSource is IList list) return list.IndexOf(ActiveItem);

        return -1;
    }

    private static TimeSpan GetTopTransitionDelay(int index, int activeIndex)
    {
        var delta = index - activeIndex;
        if (Math.Abs(delta) > StaggerRange)
            return TimeSpan.Zero;
        var delayMs = (StaggerRange + delta) * StaggerStepMs;
        return TimeSpan.FromMilliseconds(Math.Max(0, delayMs));
    }

    private static TimeSpan GetEntranceDelay(int index, int activeIndex)
    {
        var distance = Math.Abs(index - activeIndex);
        return TimeSpan.FromMilliseconds(Math.Min(220, distance * EntranceStepMs));
    }

    private static void SetTopImmediate(Control container, double top)
    {
        var transitions = container.Transitions;
        container.Transitions = null;
        Canvas.SetTop(container, top);
        container.Transitions = transitions;
    }

    private void ResetFirstLayoutState()
    {
        _isFirstLayoutPass = true;
        _knownHeights.Clear();
    }

    public void ForceSecondPassLayout()
    {
        InvalidateMeasure();
        InvalidateArrange();
        QueueLayoutUpdate();
        Dispatcher.UIThread.Post(() =>
        {
            InvalidateMeasure();
            InvalidateArrange();
            QueueLayoutUpdate();
        }, DispatcherPriority.Render);
    }

    private void EnsureTransitions(Control container, TimeSpan topDelay, TimeSpan topDuration)
    {
        if (container.Transitions is not Transitions transitions || transitions.Count < 2)
        {
            transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = Canvas.TopProperty,
                    Duration = topDuration,
                    Delay = topDelay,
                    Easing = new CubicEaseOut()
                },
                new DoubleTransition
                {
                    Property = OpacityProperty,
                    Duration = TimeSpan.FromMilliseconds(320),
                    Delay = TimeSpan.Zero,
                    Easing = new CubicEaseOut()
                }
            };
            container.Transitions = transitions;
            return;
        }

        if (transitions[0] is DoubleTransition topTransition)
        {
            topTransition.Property = Canvas.TopProperty;
            topTransition.Duration = topDuration;
            topTransition.Delay = topDelay;
            topTransition.Easing = new CubicEaseOut();
        }

        if (transitions[1] is DoubleTransition opacityTransition)
        {
            opacityTransition.Property = OpacityProperty;
            opacityTransition.Duration = TimeSpan.FromMilliseconds(320);
            opacityTransition.Delay = TimeSpan.Zero;
            opacityTransition.Easing = new CubicEaseOut();
        }
    }
}
