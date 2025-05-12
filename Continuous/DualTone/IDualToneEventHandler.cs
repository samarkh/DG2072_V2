using System.Windows;
using System.Windows.Controls;

namespace DG2072_USB_Control.Continuous.DualTone
{
    public interface IDualToneEventHandler
    {
        void OnSecondaryFrequencyTextChanged(object sender, TextChangedEventArgs e);
        void OnSecondaryFrequencyLostFocus(object sender, RoutedEventArgs e);
        void OnSecondaryFrequencyUnitChanged(object sender, SelectionChangedEventArgs e);
        void OnSynchronizeFrequenciesCheckChanged(object sender, RoutedEventArgs e);
        void OnFrequencyRatioSelectionChanged(object sender, SelectionChangedEventArgs e);
        void OnDualToneModeChanged(object sender, RoutedEventArgs e);
        void OnCenterFrequencyTextChanged(object sender, TextChangedEventArgs e);
        void OnCenterFrequencyLostFocus(object sender, RoutedEventArgs e);
        void OnCenterFrequencyUnitChanged(object sender, SelectionChangedEventArgs e);
        void OnOffsetFrequencyTextChanged(object sender, TextChangedEventArgs e);
        void OnOffsetFrequencyLostFocus(object sender, RoutedEventArgs e);
        void OnOffsetFrequencyUnitChanged(object sender, SelectionChangedEventArgs e);
    }
}