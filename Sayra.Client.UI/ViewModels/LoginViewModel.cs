using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sayra.Client.UI.Services;
using System.Threading.Tasks;

namespace Sayra.Client.UI.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly IClientBridge _clientBridge;

        [ObservableProperty]
        private string _pin = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        public LoginViewModel(IClientBridge clientBridge)
        {
            _clientBridge = clientBridge;
        }

        [RelayCommand]
        private async Task Login()
        {
            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                // In a real app, we'd send the PIN to the core
                await _clientBridge.SendCommand("LOGIN", new { Pin = Pin });
            }
            catch (System.Exception ex)
            {
                ErrorMessage = "Login failed: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
