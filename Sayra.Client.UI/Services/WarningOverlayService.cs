using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace Sayra.Client.UI.Services;

public partial class WarningOverlayService : ObservableObject
{
    [ObservableProperty]
    private bool _isWarningVisible;

    [ObservableProperty]
    private bool _isSoftWarningVisible;

    [ObservableProperty]
    private string _warningMessage = string.Empty;

    [ObservableProperty]
    private bool _isCritical;

    private int _lastWarningMinutes = int.MaxValue;

    public void UpdateRemainingTime(TimeSpan remainingTime)
    {
        double minutes = remainingTime.TotalMinutes;

        if (minutes <= 0)
        {
            ShowWarning("Session Ended. Please top up to continue.", true, true);
        }
        else if (minutes <= 5)
        {
            if (_lastWarningMinutes > 5)
                ShowWarning("Critical: 5 minutes remaining!", true, true);
        }
        else if (minutes <= 10)
        {
            if (_lastWarningMinutes > 10)
                ShowWarning("Warning: 10 minutes remaining.", false, false);
        }
        else if (minutes <= 30)
        {
            if (_lastWarningMinutes > 30)
                ShowWarning("Notice: 30 minutes remaining.", false, false);
        }
        else
        {
            IsWarningVisible = false;
            IsSoftWarningVisible = false;
        }

        _lastWarningMinutes = (int)Math.Floor(minutes);
    }

    public void ShowSoftWarning(string message) => ShowWarning(message, false, false);

    private void ShowWarning(string message, bool isCritical, bool isModal)
    {
        WarningMessage = message;
        IsCritical = isCritical;
        if (isModal)
        {
            IsWarningVisible = true;
            IsSoftWarningVisible = false;
        }
        else
        {
            IsSoftWarningVisible = true;
            IsWarningVisible = false;
        }
    }

    [RelayCommand]
    public void HideWarning()
    {
        IsWarningVisible = false;
    }
}
