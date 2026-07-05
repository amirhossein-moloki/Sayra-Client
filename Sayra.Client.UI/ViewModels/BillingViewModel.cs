using CommunityToolkit.Mvvm.ComponentModel;
using Sayra.Client.UI.Services;
using System;

namespace Sayra.Client.UI.ViewModels;

public partial class BillingViewModel : ObservableObject, IDisposable
{
    private readonly IClientBridge _clientBridge;
    private readonly System.Reactive.Disposables.CompositeDisposable _disposables = new();

    [ObservableProperty]
    private string _hourlyRate = "$0.00";

    [ObservableProperty]
    private string _currentCost = "$0.00";

    [ObservableProperty]
    private string _estimatedTotal = "$0.00";

    public BillingViewModel(IClientBridge clientBridge)
    {
        _clientBridge = clientBridge;

        _clientBridge.SubscribeToStateChanged().Subscribe(state =>
        {
            HourlyRate = state.RatePerHour.ToString("C2");
            CurrentCost = state.CurrentCost.ToString("C2");

            double estimated = (state.TotalDurationMinutes / 60.0) * state.RatePerHour;
            EstimatedTotal = estimated.ToString("C2");
        }).DisposeWith(_disposables);
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }
}
