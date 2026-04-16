using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace KugouAvaloniaPlayer.Behaviors;

public sealed class InteractionBehaviors
{
    private static readonly Dictionary<Control, EventHandler<VisualTreeAttachmentEventArgs>> AttachedHandlers = new();
    private static readonly Dictionary<Control, EventHandler<VisualTreeAttachmentEventArgs>> DetachedHandlers = new();
    private static readonly Dictionary<InputElement, EventHandler<KeyEventArgs>> EnterKeyHandlers = new();
    private static readonly Dictionary<InputElement, EventHandler<PointerPressedEventArgs>> PointerPressedHandlers = new();
    private static readonly Dictionary<InputElement, EventHandler<TappedEventArgs>> DoubleTappedHandlers = new();
    private static readonly Dictionary<Control, EventHandler<ScrollChangedEventArgs>> NearBottomScrollHandlers = new();

    private static readonly AttachedProperty<bool> HasExecutedAttachedCommandProperty =
        AvaloniaProperty.RegisterAttached<InteractionBehaviors, Control, bool>("HasExecutedAttachedCommand");

    public static readonly AttachedProperty<ICommand?> AttachedToVisualTreeCommandProperty =
        AvaloniaProperty.RegisterAttached<InteractionBehaviors, Control, ICommand?>("AttachedToVisualTreeCommand");

    public static readonly AttachedProperty<object?> AttachedToVisualTreeCommandParameterProperty =
        AvaloniaProperty.RegisterAttached<InteractionBehaviors, Control, object?>("AttachedToVisualTreeCommandParameter");

    public static readonly AttachedProperty<bool> AttachedToVisualTreeCommandRunOnceProperty =
        AvaloniaProperty.RegisterAttached<InteractionBehaviors, Control, bool>("AttachedToVisualTreeCommandRunOnce", true);

    public static readonly AttachedProperty<ICommand?> DetachedFromVisualTreeCommandProperty =
        AvaloniaProperty.RegisterAttached<InteractionBehaviors, Control, ICommand?>("DetachedFromVisualTreeCommand");

    public static readonly AttachedProperty<object?> DetachedFromVisualTreeCommandParameterProperty =
        AvaloniaProperty.RegisterAttached<InteractionBehaviors, Control, object?>("DetachedFromVisualTreeCommandParameter");

    public static readonly AttachedProperty<ICommand?> EnterKeyCommandProperty =
        AvaloniaProperty.RegisterAttached<InteractionBehaviors, InputElement, ICommand?>("EnterKeyCommand");

    public static readonly AttachedProperty<object?> EnterKeyCommandParameterProperty =
        AvaloniaProperty.RegisterAttached<InteractionBehaviors, InputElement, object?>("EnterKeyCommandParameter");

    public static readonly AttachedProperty<ICommand?> PointerPressedCommandProperty =
        AvaloniaProperty.RegisterAttached<InteractionBehaviors, InputElement, ICommand?>("PointerPressedCommand");

    public static readonly AttachedProperty<object?> PointerPressedCommandParameterProperty =
        AvaloniaProperty.RegisterAttached<InteractionBehaviors, InputElement, object?>("PointerPressedCommandParameter");

    public static readonly AttachedProperty<ICommand?> DoubleTappedCommandProperty =
        AvaloniaProperty.RegisterAttached<InteractionBehaviors, InputElement, ICommand?>("DoubleTappedCommand");

    public static readonly AttachedProperty<object?> DoubleTappedCommandParameterProperty =
        AvaloniaProperty.RegisterAttached<InteractionBehaviors, InputElement, object?>("DoubleTappedCommandParameter");

    public static readonly AttachedProperty<ICommand?> ScrollNearBottomCommandProperty =
        AvaloniaProperty.RegisterAttached<InteractionBehaviors, Control, ICommand?>("ScrollNearBottomCommand");

    public static readonly AttachedProperty<object?> ScrollNearBottomCommandParameterProperty =
        AvaloniaProperty.RegisterAttached<InteractionBehaviors, Control, object?>("ScrollNearBottomCommandParameter");

    public static readonly AttachedProperty<double> ScrollNearBottomThresholdProperty =
        AvaloniaProperty.RegisterAttached<InteractionBehaviors, Control, double>("ScrollNearBottomThreshold", 50d);

    static InteractionBehaviors()
    {
        AttachedToVisualTreeCommandProperty.Changed.AddClassHandler<Control>(OnAttachedToVisualTreeCommandChanged);
        DetachedFromVisualTreeCommandProperty.Changed.AddClassHandler<Control>(OnDetachedFromVisualTreeCommandChanged);
        EnterKeyCommandProperty.Changed.AddClassHandler<InputElement>(OnEnterKeyCommandChanged);
        PointerPressedCommandProperty.Changed.AddClassHandler<InputElement>(OnPointerPressedCommandChanged);
        DoubleTappedCommandProperty.Changed.AddClassHandler<InputElement>(OnDoubleTappedCommandChanged);
        ScrollNearBottomCommandProperty.Changed.AddClassHandler<Control>(OnScrollNearBottomCommandChanged);
    }

    private InteractionBehaviors()
    {
    }

    public static void SetAttachedToVisualTreeCommand(AvaloniaObject element, ICommand? value)
        => element.SetValue(AttachedToVisualTreeCommandProperty, value);

    public static ICommand? GetAttachedToVisualTreeCommand(AvaloniaObject element)
        => element.GetValue(AttachedToVisualTreeCommandProperty);

    public static void SetAttachedToVisualTreeCommandParameter(AvaloniaObject element, object? value)
        => element.SetValue(AttachedToVisualTreeCommandParameterProperty, value);

    public static object? GetAttachedToVisualTreeCommandParameter(AvaloniaObject element)
        => element.GetValue(AttachedToVisualTreeCommandParameterProperty);

    public static void SetAttachedToVisualTreeCommandRunOnce(AvaloniaObject element, bool value)
        => element.SetValue(AttachedToVisualTreeCommandRunOnceProperty, value);

    public static bool GetAttachedToVisualTreeCommandRunOnce(AvaloniaObject element)
        => element.GetValue(AttachedToVisualTreeCommandRunOnceProperty);

    public static void SetDetachedFromVisualTreeCommand(AvaloniaObject element, ICommand? value)
        => element.SetValue(DetachedFromVisualTreeCommandProperty, value);

    public static ICommand? GetDetachedFromVisualTreeCommand(AvaloniaObject element)
        => element.GetValue(DetachedFromVisualTreeCommandProperty);

    public static void SetDetachedFromVisualTreeCommandParameter(AvaloniaObject element, object? value)
        => element.SetValue(DetachedFromVisualTreeCommandParameterProperty, value);

    public static object? GetDetachedFromVisualTreeCommandParameter(AvaloniaObject element)
        => element.GetValue(DetachedFromVisualTreeCommandParameterProperty);

    public static void SetEnterKeyCommand(AvaloniaObject element, ICommand? value)
        => element.SetValue(EnterKeyCommandProperty, value);

    public static ICommand? GetEnterKeyCommand(AvaloniaObject element)
        => element.GetValue(EnterKeyCommandProperty);

    public static void SetEnterKeyCommandParameter(AvaloniaObject element, object? value)
        => element.SetValue(EnterKeyCommandParameterProperty, value);

    public static object? GetEnterKeyCommandParameter(AvaloniaObject element)
        => element.GetValue(EnterKeyCommandParameterProperty);

    public static void SetPointerPressedCommand(AvaloniaObject element, ICommand? value)
        => element.SetValue(PointerPressedCommandProperty, value);

    public static ICommand? GetPointerPressedCommand(AvaloniaObject element)
        => element.GetValue(PointerPressedCommandProperty);

    public static void SetPointerPressedCommandParameter(AvaloniaObject element, object? value)
        => element.SetValue(PointerPressedCommandParameterProperty, value);

    public static object? GetPointerPressedCommandParameter(AvaloniaObject element)
        => element.GetValue(PointerPressedCommandParameterProperty);

    public static void SetDoubleTappedCommand(AvaloniaObject element, ICommand? value)
        => element.SetValue(DoubleTappedCommandProperty, value);

    public static ICommand? GetDoubleTappedCommand(AvaloniaObject element)
        => element.GetValue(DoubleTappedCommandProperty);

    public static void SetDoubleTappedCommandParameter(AvaloniaObject element, object? value)
        => element.SetValue(DoubleTappedCommandParameterProperty, value);

    public static object? GetDoubleTappedCommandParameter(AvaloniaObject element)
        => element.GetValue(DoubleTappedCommandParameterProperty);

    public static void SetScrollNearBottomCommand(AvaloniaObject element, ICommand? value)
        => element.SetValue(ScrollNearBottomCommandProperty, value);

    public static ICommand? GetScrollNearBottomCommand(AvaloniaObject element)
        => element.GetValue(ScrollNearBottomCommandProperty);

    public static void SetScrollNearBottomCommandParameter(AvaloniaObject element, object? value)
        => element.SetValue(ScrollNearBottomCommandParameterProperty, value);

    public static object? GetScrollNearBottomCommandParameter(AvaloniaObject element)
        => element.GetValue(ScrollNearBottomCommandParameterProperty);

    public static void SetScrollNearBottomThreshold(AvaloniaObject element, double value)
        => element.SetValue(ScrollNearBottomThresholdProperty, value);

    public static double GetScrollNearBottomThreshold(AvaloniaObject element)
        => element.GetValue(ScrollNearBottomThresholdProperty);

    private static void OnAttachedToVisualTreeCommandChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        if (AttachedHandlers.Remove(control, out var oldHandler))
            control.AttachedToVisualTree -= oldHandler;

        if (e.NewValue is not ICommand)
            return;

        EventHandler<VisualTreeAttachmentEventArgs> handler = (_, _) =>
        {
            if (GetAttachedToVisualTreeCommandRunOnce(control) && control.GetValue(HasExecutedAttachedCommandProperty))
                return;

            ExecuteCommand(control, GetAttachedToVisualTreeCommand(control),
                GetAttachedToVisualTreeCommandParameter(control));

            if (GetAttachedToVisualTreeCommandRunOnce(control))
                control.SetValue(HasExecutedAttachedCommandProperty, true);
        };

        AttachedHandlers[control] = handler;
        control.AttachedToVisualTree += handler;
    }

    private static void OnDetachedFromVisualTreeCommandChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        if (DetachedHandlers.Remove(control, out var oldHandler))
            control.DetachedFromVisualTree -= oldHandler;

        if (e.NewValue is not ICommand)
            return;

        EventHandler<VisualTreeAttachmentEventArgs> handler = (_, _) =>
            ExecuteCommand(control, GetDetachedFromVisualTreeCommand(control),
                GetDetachedFromVisualTreeCommandParameter(control));

        DetachedHandlers[control] = handler;
        control.DetachedFromVisualTree += handler;
    }

    private static void OnEnterKeyCommandChanged(InputElement element, AvaloniaPropertyChangedEventArgs e)
    {
        if (EnterKeyHandlers.Remove(element, out var oldHandler))
            element.KeyDown -= oldHandler;

        if (e.NewValue is not ICommand)
            return;

        EventHandler<KeyEventArgs> handler = (_, args) =>
        {
            if (args.Key != Key.Enter)
                return;

            var command = GetEnterKeyCommand(element);
            var parameter = GetEnterKeyCommandParameter(element);
            if (ExecuteCommand(element, command, parameter))
                args.Handled = true;
        };

        EnterKeyHandlers[element] = handler;
        element.KeyDown += handler;
    }

    private static void OnPointerPressedCommandChanged(InputElement element, AvaloniaPropertyChangedEventArgs e)
    {
        if (PointerPressedHandlers.Remove(element, out var oldHandler))
            element.PointerPressed -= oldHandler;

        if (e.NewValue is not ICommand)
            return;

        EventHandler<PointerPressedEventArgs> handler = (_, args) =>
        {
            var command = GetPointerPressedCommand(element);
            var parameter = GetPointerPressedCommandParameter(element);
            if (ExecuteCommand(element, command, parameter))
                args.Handled = true;
        };

        PointerPressedHandlers[element] = handler;
        element.PointerPressed += handler;
    }

    private static void OnDoubleTappedCommandChanged(InputElement element, AvaloniaPropertyChangedEventArgs e)
    {
        if (DoubleTappedHandlers.Remove(element, out var oldHandler))
            element.DoubleTapped -= oldHandler;

        if (e.NewValue is not ICommand)
            return;

        EventHandler<TappedEventArgs> handler = (_, args) =>
        {
            var command = GetDoubleTappedCommand(element);
            var parameter = GetDoubleTappedCommandParameter(element);
            if (ExecuteCommand(element, command, parameter))
                args.Handled = true;
        };

        DoubleTappedHandlers[element] = handler;
        element.DoubleTapped += handler;
    }

    private static void OnScrollNearBottomCommandChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        if (NearBottomScrollHandlers.Remove(control, out var oldHandler))
            control.RemoveHandler(ScrollViewer.ScrollChangedEvent, oldHandler);

        if (e.NewValue is not ICommand)
            return;

        EventHandler<ScrollChangedEventArgs> handler = (_, args) =>
        {
            var scrollViewer = ResolveScrollViewer(control, args);
            if (scrollViewer == null)
                return;

            var threshold = GetScrollNearBottomThreshold(control);
            var currentBottom = scrollViewer.Offset.Y + scrollViewer.Viewport.Height;
            if (currentBottom < scrollViewer.Extent.Height - threshold)
                return;

            ExecuteCommand(control, GetScrollNearBottomCommand(control),
                GetScrollNearBottomCommandParameter(control));
        };

        NearBottomScrollHandlers[control] = handler;
        control.AddHandler(ScrollViewer.ScrollChangedEvent, handler, RoutingStrategies.Bubble);
    }

    private static ScrollViewer? ResolveScrollViewer(Control host, ScrollChangedEventArgs args)
    {
        if (args.Source is ScrollViewer eventSource)
            return eventSource;

        if (host is ScrollViewer hostScrollViewer)
            return hostScrollViewer;

        return host.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
    }

    private static bool ExecuteCommand(AvaloniaObject source, ICommand? command, object? parameter)
    {
        if (command == null)
            return false;

        var actualParameter = parameter ?? (source as StyledElement)?.DataContext;
        if (!command.CanExecute(actualParameter))
            return false;

        command.Execute(actualParameter);
        return true;
    }
}
