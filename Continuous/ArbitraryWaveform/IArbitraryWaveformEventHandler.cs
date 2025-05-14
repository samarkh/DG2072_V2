using System.Windows;
using System.Windows.Controls;

namespace DG2072_USB_Control.Continuous.ArbitraryWaveform
{
    public interface IArbitraryWaveformEventHandler
    {
        void OnCategorySelectionChanged(object sender, SelectionChangedEventArgs e);
        void OnWaveformSelectionChanged(object sender, SelectionChangedEventArgs e);
        void OnParameterTextChanged(object sender, TextChangedEventArgs e);
        void OnParameterLostFocus(object sender, RoutedEventArgs e);
        void OnApplyButtonClick(object sender, RoutedEventArgs e);
    }
}