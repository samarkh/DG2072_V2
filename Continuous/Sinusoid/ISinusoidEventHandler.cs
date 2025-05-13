using System.Windows;
using System.Windows.Controls;

namespace DG2072_USB_Control.Continuous.Sinusoid
{
    public interface ISinusoidEventHandler
    {
        // Unlike other waveforms, sine doesn't have special parameters beyond 
        // the basic frequency, amplitude, offset, and phase.
        // This interface exists for consistency and potential future expansion.
    }
}