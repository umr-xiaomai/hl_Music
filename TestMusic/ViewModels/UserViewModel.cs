using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KuGou.Net.Clients;
using TestMusic.Services;

namespace TestMusic.ViewModels;

public partial class UserViewModel : PageViewModelBase
{
    private  UserClient _userClient;
    private  AuthClient _authClient;
    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private string? _userAvatar;
    [ObservableProperty] private string _userId = "";
    [ObservableProperty] private string _userName = "加载中...";
    [ObservableProperty] private string _vipStatus = "未开通";
    
    private PlayerViewModel Player { get; } 
    
    public UserViewModel(PlayerViewModel player,UserClient userClient, AuthClient authClient)
    {
        _userClient = userClient;
        _authClient = authClient;
        Player = player;
        SelectedCloseBehavior = SettingsManager.Settings.CloseBehavior;
        SelectedQuality = SettingsManager.Settings.MusicQuality;
    }


    public override string DisplayName => "用户中心";
    public override string Icon => "/Assets/user-svgrepo-com.svg";

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
        LogoutRequested?.Invoke();
        await Task.CompletedTask;
    }

    public event Action? LogoutRequested;
    
    
    public CloseBehavior[] AvailableCloseBehaviors { get; } = Enum.GetValues<CloseBehavior>();
    
    [ObservableProperty] private CloseBehavior _selectedCloseBehavior;
    partial void OnSelectedCloseBehaviorChanged(CloseBehavior value)
    {
        SettingsManager.Settings.CloseBehavior = value;
        SettingsManager.Save();
    }

    
    public string[] QualityOptions { get; } = { "128", "320", "flac", "high" };

    [ObservableProperty] private string _selectedQuality;
    partial void OnSelectedQualityChanged(string value)
    {
        SettingsManager.Settings.MusicQuality = value;
        Player.MusicQuality = value;
        SettingsManager.Save();
    }
}