using System.Windows;
using System.Windows.Controls;

namespace DG2072_USB_Control.Continuous.PulseGenerator
{
    public interface IPulseEventHandler
    {
        void OnPulsePeriodTextChanged(object sender, TextChangedEventArgs e);

        void OnPulsePeriodLostFocus(object sender, RoutedEventArgs e);

        void OnPulsePeriodUnitChanged(object sender, SelectionChangedEventArgs e);

        void OnPulseRateModeToggleClicked(object sender, RoutedEventArgs e);

        void OnPulseRiseTimeTextChanged(object sender, TextChangedEventArgs e);

        void OnPulseRiseTimeLostFocus(object sender, RoutedEventArgs e);

        void OnPulseRiseTimeUnitChanged(object sender, SelectionChangedEventArgs e);

        void OnPulseWidthTextChanged(object sender, TextChangedEventArgs e);

        void OnPulseWidthLostFocus(object sender, RoutedEventArgs e);

        void OnPulseWidthUnitChanged(object sender, SelectionChangedEventArgs e);

        void OnPulseFallTimeTextChanged(object sender, TextChangedEventArgs e);

        void OnPulseFallTimeLostFocus(object sender, RoutedEventArgs e);

        void OnPulseFallTimeUnitChanged(object sender, SelectionChangedEventArgs e);
    }
}