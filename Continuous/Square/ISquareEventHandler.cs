using System.Windows;
using System.Windows.Controls;

namespace DG2072_USB_Control.Continuous.Square
{
    public interface ISquareEventHandler
    {
        void OnDutyCycleTextChanged(object sender, TextChangedEventArgs e);
        void OnDutyCycleLostFocus(object sender, RoutedEventArgs e);
    }
}