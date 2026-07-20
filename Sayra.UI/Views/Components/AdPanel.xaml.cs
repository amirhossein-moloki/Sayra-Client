using System.Windows.Controls;

namespace Sayra.UI.Views.Components
{
    public partial class AdPanel : UserControl
    {
        public AdPanel()
        {
            InitializeComponent();
            this.DataContext = App.ServiceProvider?.GetService<Sayra.UI.ViewModels.AdPanelViewModel>() ?? new Sayra.UI.ViewModels.AdPanelViewModel();
        }
    }
}
