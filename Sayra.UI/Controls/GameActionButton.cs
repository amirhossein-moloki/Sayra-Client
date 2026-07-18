using System.Windows;
using System.Windows.Controls;

namespace Sayra.UI.Controls
{
    public class GameActionButton : Button
    {
        static GameActionButton()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(GameActionButton), new FrameworkPropertyMetadata(typeof(GameActionButton)));
        }
    }
}
