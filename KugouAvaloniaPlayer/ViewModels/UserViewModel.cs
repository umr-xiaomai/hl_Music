using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Styling;
using KugouAvaloniaPlayer.Behaviors;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using KuGou.Net.Clients;
using KuGou.Net.Protocol.Session;
using KugouAvaloniaPlayer.Controls;
using KugouAvaloniaPlayer.Models;
using KugouAvaloniaPlayer.Services;
using SukiUI;
using SukiUI.Dialogs;

namespace KugouAvaloniaPlayer.ViewModels;

public partial class UserViewModel : PageViewModelBase
{
    private const string SettingsSectionGeneral = "常规";
    private const string SettingsSectionPlayback = "播放与音效";
    private const string SettingsSectionShortcuts = "快捷键";
    private const string SettingsSectionLyrics = "歌词悬浮窗";
    private const string SettingsSectionUpdate = "更新与关于";
    private const string SettingsSectionAccount = "账户";
    private const string LyricTargetMain = "歌词";
    private const string LyricTargetTranslation = "歌词翻译";
    private const string LyricColorModeDefault = "默认";
    private const string LyricColorModeCustom = "自定义";

    private readonly AuthClient _authClient;
    private readonly ISukiDialogManager _dialogManager;
    private readonly EqSettingsViewModel _eqSettingsViewModel;
    private readonly IGlobalShortcutService _globalShortcutService;
    private readonly KgSessionManager _sessionManager;
    private readonly UserClient _userClient;
    private bool _isInitializingLyricColorEditor;
    private bool _isInitializingLyricFontEditor;
    private readonly HashSet<string> _availableLyricFonts;

    [ObservableProperty] private bool _autoCheckUpdate;
    [ObservableProperty] private bool _enableSeamlessTransition;
    [ObservableProperty] private bool _enableSurround;
    [ObservableProperty] private bool _isCheckingUpdate;
    [ObservableProperty] private bool _isLoading = true;

    [ObservableProperty] private string _lyricColorHexInput = "#FFFFFFFF";
    [ObservableProperty] private string _selectedLyricColorMode = LyricColorModeDefault;
    [ObservableProperty] private string _selectedLyricColorTarget = LyricTargetMain;
    [ObservableProperty] private string _selectedLyricFontMode = LyricColorModeDefault;
    [ObservableProperty] private string? _selectedLyricFontFamily;
    [ObservableProperty] private bool _enableGlobalShortcuts;

    [ObservableProperty] private CloseBehavior _selectedCloseBehavior;
    [ObservableProperty] private string _selectedEQPreset;
    [ObservableProperty] private string _selectedSettingsSection = SettingsSectionGeneral;
    [ObservableProperty] private string? _userAvatar;
    [ObservableProperty] private string _userId = "";
    [ObservableProperty] private string _userName = "加载中...";
    [ObservableProperty] private string _vipStatus = "未开通";

    public UserViewModel(PlayerViewModel player, UserClient userClient, AuthClient authClient,
        ISukiDialogManager dialogManager, EqSettingsViewModel eqSettingsViewModel, KgSessionManager sessionManager,
        IGlobalShortcutService globalShortcutService)
    {
        _userClient = userClient;
        _authClient = authClient;
        _dialogManager = dialogManager;
        _eqSettingsViewModel = eqSettingsViewModel;
        _sessionManager = sessionManager;
        _globalShortcutService = globalShortcutService;

        Player = player;
        SelectedCloseBehavior = SettingsManager.Settings.CloseBehavior;
        AutoCheckUpdate = SettingsManager.Settings.AutoCheckUpdate;
        EnableGlobalShortcuts = SettingsManager.Settings.GlobalShortcuts.EnableGlobalShortcuts;
        EQPresetOptions = ["原声", "流行", "摇滚", "爵士", "古典", "嘻哈", "布鲁斯", "电子音乐", "金属", "自定义"];

        var preset = SettingsManager.Settings.EQPreset;
        SelectedEQPreset = Array.Exists(EQPresetOptions, x => x == preset) ? preset : "原声";

        EnableSurround = SettingsManager.Settings.EnableSurround;
        EnableSeamlessTransition = SettingsManager.Settings.EnableSeamlessTransition;
        LyricFontFamilyOptions = LoadSystemFontFamilies();
        _availableLyricFonts = new HashSet<string>(LyricFontFamilyOptions, StringComparer.OrdinalIgnoreCase);
        UserId = _sessionManager.Session.UserId;
        LoadLyricColorEditorFromSettings();
        LoadLyricFontEditorFromSettings();
        ShortcutItems =
        [
            new GlobalShortcutItemViewModel(GlobalShortcutAction.PlayPause, "播放/暂停"),
            new GlobalShortcutItemViewModel(GlobalShortcutAction.PreviousTrack, "上一首"),
            new GlobalShortcutItemViewModel(GlobalShortcutAction.NextTrack, "下一首"),
            new GlobalShortcutItemViewModel(GlobalShortcutAction.ShowMainWindow, "显示窗口"),
            new GlobalShortcutItemViewModel(GlobalShortcutAction.ToggleDesktopLyric, "显示桌面歌词")
        ];
        RefreshShortcutTexts();
        ApplyRegistrationResults(_globalShortcutService.CurrentResults);
        _globalShortcutService.RegistrationChanged += ApplyRegistrationResults;
    }

    public string[] EQPresetOptions { get; }
    public string[] SettingsSections { get; } =
    [
        SettingsSectionGeneral, SettingsSectionPlayback, SettingsSectionShortcuts, SettingsSectionLyrics, SettingsSectionUpdate,
        SettingsSectionAccount
    ];

    public string[] LyricColorTargetOptions { get; } = [LyricTargetMain, LyricTargetTranslation];

    public string[] LyricColorModeOptions { get; } = [LyricColorModeDefault, LyricColorModeCustom];
    public string[] LyricFontModeOptions { get; } = [LyricColorModeDefault, LyricColorModeCustom];

    public string[] LyricColorPalette { get; } =
    [
        "#FFFFFFFF",
        "#FFCCFFFFFF",
        "#FFFFE082",
        "#FFFFAB91",
        "#FFA5D6A7",
        "#FF80DEEA",
        "#FF90CAF9",
        "#FFB39DDB",
        "#FFF48FB1",
        "#FFFFF59D",
        "#FFB0BEC5",
        "#FFFFCDD2"
    ];
    public string[] LyricFontFamilyOptions { get; }
    public GlobalShortcutItemViewModel[] ShortcutItems { get; }

    public PlayerViewModel Player { get; }

    public override string DisplayName => "设置";
    public override string Icon => "/Assets/gear-svgrepo-com.svg";

    public CloseBehavior[] AvailableCloseBehaviors { get; } = Enum.GetValues<CloseBehavior>();

    public bool IsLyricColorCustomMode => SelectedLyricColorMode == LyricColorModeCustom;
    public bool IsLyricFontCustomMode => SelectedLyricFontMode == LyricColorModeCustom;
    public bool IsGeneralSection => SelectedSettingsSection == SettingsSectionGeneral;
    public bool IsPlaybackSection => SelectedSettingsSection == SettingsSectionPlayback;
    public bool IsShortcutsSection => SelectedSettingsSection == SettingsSectionShortcuts;
    public bool IsLyricsSection => SelectedSettingsSection == SettingsSectionLyrics;
    public bool IsUpdateSection => SelectedSettingsSection == SettingsSectionUpdate;
    public bool IsAccountSection => SelectedSettingsSection == SettingsSectionAccount;

    public IBrush LyricColorPreviewBrush => new SolidColorBrush(ParseColorOrDefault(LyricColorHexInput, Colors.Transparent));

    public bool IsDarkMode
    {
        get => SukiTheme.GetInstance().ActiveBaseTheme == ThemeVariant.Dark;
        set
        {
            SukiTheme.GetInstance().ChangeBaseTheme(value ? ThemeVariant.Dark : ThemeVariant.Light);
            OnPropertyChanged();
        }
    }

    public async Task LoadUserInfoAsync()
    {
        IsLoading = true;
        try
        {
            var userInfo = await _userClient.GetUserInfoAsync();
            if (userInfo != null)
            {
                UserName = userInfo.Name;
                UserAvatar = string.IsNullOrWhiteSpace(userInfo.Pic) ? null : userInfo.Pic;
                UserId = _sessionManager.Session.UserId;
            }

            var vipInfo = await _userClient.GetVipInfoAsync();
            if (vipInfo != null) VipStatus = vipInfo.IsVip is 1 ? "VIP会员" : "普通用户";
        }
        catch
        {
            UserName = "加载失败";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task Logout()
    {
        _authClient.LogOutAsync();
        WeakReferenceMessenger.Default.Send(new AuthStateChangedMessage(false));
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task CheckForUpdate()
    {
        if (IsCheckingUpdate) return;
        IsCheckingUpdate = true;
        CheckForUpdateRequested?.Invoke();
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void PickLyricPaletteColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return;
        LyricColorHexInput = hex;
        ApplyLyricColorHex();
    }

    [RelayCommand]
    private void ApplyLyricColorHex()
    {
        var normalized = NormalizeColorHex(LyricColorHexInput);
        if (normalized == null) return;

        // 输入颜色时自动进入自定义模式
        if (!IsLyricColorCustomMode)
        {
            _isInitializingLyricColorEditor = true;
            SelectedLyricColorMode = LyricColorModeCustom;
            _isInitializingLyricColorEditor = false;
            SetCurrentTargetCustomEnabled(true);
            OnPropertyChanged(nameof(IsLyricColorCustomMode));
        }

        SetCurrentTargetCustomColor(normalized);
        LyricColorHexInput = normalized;
        OnPropertyChanged(nameof(LyricColorPreviewBrush));
        SettingsManager.Save();
        NotifyDesktopLyricStyleChanged();
    }

    public event Action? CheckForUpdateRequested;

    [RelayCommand]
    private void SwitchSettingsSection(string? section)
    {
        if (string.IsNullOrWhiteSpace(section)) return;
        if (SettingsSections.Contains(section)) SelectedSettingsSection = section;
    }

    partial void OnSelectedCloseBehaviorChanged(CloseBehavior value)
    {
        SettingsManager.Settings.CloseBehavior = value;
        SettingsManager.Save();
    }

    partial void OnAutoCheckUpdateChanged(bool value)
    {
        SettingsManager.Settings.AutoCheckUpdate = value;
        SettingsManager.Save();
    }

    partial void OnEnableGlobalShortcutsChanged(bool value)
    {
        if (ShortcutItems == null)
            return;

        var candidate = BuildShortcutSettingsSnapshot();
        candidate.EnableGlobalShortcuts = value;
        var applyResult = _globalShortcutService.TryApplySettings(candidate);
        if (!applyResult.Success)
        {
            EnableGlobalShortcuts = !value;
            return;
        }

        SettingsManager.Settings.GlobalShortcuts = candidate;
        SettingsManager.Save();
        ApplyRegistrationResults(applyResult.Results);
    }

    partial void OnSelectedEQPresetChanged(string value)
    {
        SettingsManager.Settings.EQPreset = value;
        SettingsManager.Save();
        Player.UpdateAudioEffects(value, EnableSurround);
    }

    partial void OnEnableSurroundChanged(bool value)
    {
        SettingsManager.Settings.EnableSurround = value;
        SettingsManager.Save();
        Player.UpdateAudioEffects(SelectedEQPreset, value);
    }

    partial void OnEnableSeamlessTransitionChanged(bool value)
    {
        SettingsManager.Settings.EnableSeamlessTransition = value;
        SettingsManager.Save();
        Player.SetSeamlessTransitionEnabled(value);
    }

    partial void OnSelectedLyricColorTargetChanged(string value)
    {
        LoadLyricColorEditorFromSettings();
    }

    partial void OnSelectedLyricColorModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsLyricColorCustomMode));
        if (_isInitializingLyricColorEditor) return;

        SetCurrentTargetCustomEnabled(value == LyricColorModeCustom);
        SettingsManager.Save();
        NotifyDesktopLyricStyleChanged();
    }

    partial void OnLyricColorHexInputChanged(string value)
    {
        OnPropertyChanged(nameof(LyricColorPreviewBrush));
    }

    partial void OnSelectedLyricFontModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsLyricFontCustomMode));
        if (_isInitializingLyricFontEditor) return;

        SettingsManager.Settings.DesktopLyricUseCustomFont = value == LyricColorModeCustom;
        if (SettingsManager.Settings.DesktopLyricUseCustomFont && !string.IsNullOrWhiteSpace(SelectedLyricFontFamily))
            SettingsManager.Settings.DesktopLyricCustomFontFamily = SelectedLyricFontFamily;
        SettingsManager.Save();
        NotifyDesktopLyricStyleChanged();
    }

    partial void OnSelectedLyricFontFamilyChanged(string? value)
    {
        if (_isInitializingLyricFontEditor) return;

        var normalized = NormalizeFontName(value);
        if (normalized == null)
            return;

        if (!string.Equals(value, normalized, StringComparison.Ordinal))
        {
            _isInitializingLyricFontEditor = true;
            SelectedLyricFontFamily = normalized;
            _isInitializingLyricFontEditor = false;
        }

        SettingsManager.Settings.DesktopLyricCustomFontFamily = normalized;
        SettingsManager.Settings.DesktopLyricUseCustomFont = SelectedLyricFontMode == LyricColorModeCustom;
        SettingsManager.Save();
        NotifyDesktopLyricStyleChanged();
    }

    public void SetCheckingUpdateState(bool isChecking)
    {
        IsCheckingUpdate = isChecking;
    }

    [RelayCommand]
    private void OpenEqSettings()
    {
        var eqSettings = new EqSettingsControl
        {
            DataContext = _eqSettingsViewModel
        };

        _dialogManager.CreateDialog()
            .WithContent(eqSettings)
            .WithActionButton("确定", _ => { }, true)
            .TryShow();
    }

    private void LoadLyricColorEditorFromSettings()
    {
        _isInitializingLyricColorEditor = true;

        if (IsEditingMainLyricColor())
        {
            SelectedLyricColorMode = SettingsManager.Settings.DesktopLyricUseCustomMainColor
                ? LyricColorModeCustom
                : LyricColorModeDefault;
            LyricColorHexInput = SettingsManager.Settings.DesktopLyricCustomMainColor;
        }
        else
        {
            SelectedLyricColorMode = SettingsManager.Settings.DesktopLyricUseCustomTranslationColor
                ? LyricColorModeCustom
                : LyricColorModeDefault;
            LyricColorHexInput = SettingsManager.Settings.DesktopLyricCustomTranslationColor;
        }

        _isInitializingLyricColorEditor = false;
        OnPropertyChanged(nameof(IsLyricColorCustomMode));
        OnPropertyChanged(nameof(LyricColorPreviewBrush));
    }

    private void LoadLyricFontEditorFromSettings()
    {
        _isInitializingLyricFontEditor = true;

        SelectedLyricFontMode = SettingsManager.Settings.DesktopLyricUseCustomFont
            ? LyricColorModeCustom
            : LyricColorModeDefault;
        SelectedLyricFontFamily = NormalizeFontName(SettingsManager.Settings.DesktopLyricCustomFontFamily)
                                  ?? LyricFontFamilyOptions.FirstOrDefault();

        _isInitializingLyricFontEditor = false;
        OnPropertyChanged(nameof(IsLyricFontCustomMode));
    }

    private bool IsEditingMainLyricColor()
    {
        return SelectedLyricColorTarget == LyricTargetMain;
    }

    private void SetCurrentTargetCustomEnabled(bool enabled)
    {
        if (IsEditingMainLyricColor())
            SettingsManager.Settings.DesktopLyricUseCustomMainColor = enabled;
        else
            SettingsManager.Settings.DesktopLyricUseCustomTranslationColor = enabled;
    }

    private void SetCurrentTargetCustomColor(string normalizedHex)
    {
        if (IsEditingMainLyricColor())
            SettingsManager.Settings.DesktopLyricCustomMainColor = normalizedHex;
        else
            SettingsManager.Settings.DesktopLyricCustomTranslationColor = normalizedHex;
    }

    private static string? NormalizeColorHex(string? colorText)
    {
        if (string.IsNullOrWhiteSpace(colorText)) return null;
        return Color.TryParse(colorText.Trim(), out var parsed) ? parsed.ToString() : null;
    }

    private string? NormalizeFontName(string? fontName)
    {
        if (string.IsNullOrWhiteSpace(fontName)) return null;
        var trimmed = fontName.Trim();

        if (!_availableLyricFonts.Contains(trimmed))
            return null;

        return LyricFontFamilyOptions.FirstOrDefault(x =>
            string.Equals(x, trimmed, StringComparison.OrdinalIgnoreCase));
    }

    private static Color ParseColorOrDefault(string? colorText, Color fallback)
    {
        return Color.TryParse(colorText, out var parsed) ? parsed : fallback;
    }

    private static void NotifyDesktopLyricStyleChanged()
    {
        WeakReferenceMessenger.Default.Send(new DesktopLyricColorSettingsChangedMessage(
            SettingsManager.Settings.DesktopLyricUseCustomMainColor,
            SettingsManager.Settings.DesktopLyricCustomMainColor,
            SettingsManager.Settings.DesktopLyricUseCustomTranslationColor,
            SettingsManager.Settings.DesktopLyricCustomTranslationColor,
            SettingsManager.Settings.DesktopLyricUseCustomFont,
            SettingsManager.Settings.DesktopLyricCustomFontFamily));
    }

    private static string[] LoadSystemFontFamilies()
    {
        return FontManager.Current.SystemFonts
            .Select(f => f.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    partial void OnSelectedSettingsSectionChanged(string value)
    {
        OnPropertyChanged(nameof(IsGeneralSection));
        OnPropertyChanged(nameof(IsPlaybackSection));
        OnPropertyChanged(nameof(IsShortcutsSection));
        OnPropertyChanged(nameof(IsLyricsSection));
        OnPropertyChanged(nameof(IsUpdateSection));
        OnPropertyChanged(nameof(IsAccountSection));
    }

    [RelayCommand]
    private void BeginShortcutRecording(GlobalShortcutItemViewModel? item)
    {
        if (item == null)
            return;

        StopRecording(clearStatus: false);
        item.IsRecording = true;
        item.SetInfo("按下新的快捷键，Esc 取消。");
    }

    [RelayCommand]
    private void CaptureShortcutKey(InteractionBehaviors.KeyDownCommandContext? context)
    {
        if (context?.Parameter is not GlobalShortcutItemViewModel item || !item.IsRecording)
            return;

        var args = context.EventArgs;
        args.Handled = true;
        if (args.Key == Avalonia.Input.Key.Escape)
        {
            item.IsRecording = false;
            item.ClearStatus();
            RefreshShortcutTexts();
            return;
        }

        if (GlobalShortcutParser.IsModifierOnlyKey(args.Key))
        {
            var modifierText = GlobalShortcutParser.FormatModifiers(args.KeyModifiers);
            item.SetInfo(string.IsNullOrWhiteSpace(modifierText)
                ? "按下修饰键后继续输入主键。"
                : $"继续按下主键... 当前: {modifierText}");
            item.ShortcutText = string.IsNullOrWhiteSpace(modifierText)
                ? "按下快捷键..."
                : $"{modifierText}+...";
            return;
        }

        if (!GlobalShortcutParser.TryCreateFromKeyEvent(args, out var gesture, out var error))
        {
            item.IsRecording = false;
            item.SetError(error);
            RefreshShortcutTexts();
            return;
        }

        var gestureText = GlobalShortcutParser.Format(gesture);
        var conflict = ShortcutItems.FirstOrDefault(x =>
            x != item &&
            string.Equals(GlobalShortcutParser.NormalizeText(SettingsManager.Settings.GlobalShortcuts.GetShortcut(x.Action)),
                gestureText, StringComparison.Ordinal));
        if (conflict != null)
        {
            item.IsRecording = false;
            item.SetError($"与“{conflict.DisplayName}”冲突。");
            RefreshShortcutTexts();
            return;
        }

        var candidate = BuildShortcutSettingsSnapshot();
        candidate.SetShortcut(item.Action, gestureText);
        var applyResult = _globalShortcutService.TryApplySettings(candidate);
        item.IsRecording = false;
        if (!applyResult.Success)
        {
            item.SetError(applyResult.Results.TryGetValue(item.Action, out var result)
                ? result.ErrorMessage
                : "保存失败。");
            RefreshShortcutTexts();
            ApplyRegistrationResults(_globalShortcutService.CurrentResults);
            return;
        }

        SettingsManager.Settings.GlobalShortcuts = candidate;
        SettingsManager.Save();
        RefreshShortcutTexts();
        ApplyRegistrationResults(applyResult.Results);
    }

    [RelayCommand]
    private void ClearShortcut(GlobalShortcutItemViewModel? item)
    {
        if (item == null)
            return;

        var candidate = BuildShortcutSettingsSnapshot();
        candidate.SetShortcut(item.Action, null);
        var applyResult = _globalShortcutService.TryApplySettings(candidate);
        if (!applyResult.Success)
        {
            item.SetError(applyResult.Results.TryGetValue(item.Action, out var result)
                ? result.ErrorMessage
                : "清空失败。");
            return;
        }

        SettingsManager.Settings.GlobalShortcuts = candidate;
        SettingsManager.Save();
        RefreshShortcutTexts();
        ApplyRegistrationResults(applyResult.Results);
    }

    private void RefreshShortcutTexts()
    {
        foreach (var item in ShortcutItems)
        {
            if (item.IsRecording)
                continue;

            item.ApplyShortcutText(GlobalShortcutParser.NormalizeText(
                SettingsManager.Settings.GlobalShortcuts.GetShortcut(item.Action)));
        }
    }

    private void ApplyRegistrationResults(IReadOnlyDictionary<GlobalShortcutAction, GlobalShortcutRegistrationResult> results)
    {
        foreach (var item in ShortcutItems)
        {
            if (item.IsRecording)
                continue;

            if (!results.TryGetValue(item.Action, out var result))
            {
                item.ClearStatus();
                continue;
            }

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                item.SetError(result.ErrorMessage);
                continue;
            }

            item.ClearStatus();
        }
    }

    private void StopRecording(bool clearStatus)
    {
        foreach (var shortcutItem in ShortcutItems)
        {
            shortcutItem.IsRecording = false;
            if (clearStatus)
                shortcutItem.ClearStatus();
        }
    }

    private GlobalShortcutSettings BuildShortcutSettingsSnapshot()
    {
        var snapshot = SettingsManager.Settings.GlobalShortcuts.Clone();
        snapshot.EnableGlobalShortcuts = EnableGlobalShortcuts;
        return snapshot;
    }
}
