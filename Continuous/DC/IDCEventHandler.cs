using System.Windows;
using System.Windows.Controls;

namespace DG2072_USB_Control.Continuous.DC
{
    public interface IDCEventHandler
    {
        void OnDCVoltageTextChanged(object sender, TextChangedEventArgs e);
        void OnDCVoltageLostFocus(object sender, RoutedEventArgs e);
        void OnDCVoltageUnitChanged(object sender, SelectionChangedEventArgs e);
        void OnDCImpedanceChanged(object sender, SelectionChangedEventArgs e);
    }
}