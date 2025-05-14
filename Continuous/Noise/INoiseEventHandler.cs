using System.Windows;
using System.Windows.Controls;

namespace DG2072_USB_Control.Continuous.Noise
{
    public interface INoiseEventHandler
    {
        // Noise waveform doesn't have specialized parameters beyond
        // amplitude and offset which are handled by MainWindow.
        // This interface is included for consistency with other waveform types
        // and to allow for future extension if needed.
    }
}