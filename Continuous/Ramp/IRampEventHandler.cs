using System.Windows;
using System.Windows.Controls;

namespace DG2072_USB_Control.Continuous.Ramp
{
    public interface IRampEventHandler
    {
        void OnSymmetryTextChanged(object sender, TextChangedEventArgs e);
        void OnSymmetryLostFocus(object sender, RoutedEventArgs e);
    }
}