using System.Windows; using System.Windows.Controls;
namespace Sayra.Client.UI.Controls;
public partial class SessionTimer:UserControl{public SessionTimer(){InitializeComponent();} public static readonly DependencyProperty TimeTextProperty=DependencyProperty.Register(nameof(TimeText),typeof(string),typeof(SessionTimer),new PropertyMetadata("00:58:16")); public string TimeText{get=>(string)GetValue(TimeTextProperty);set=>SetValue(TimeTextProperty,value);} }
