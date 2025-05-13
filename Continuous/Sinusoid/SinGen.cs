using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DG2072_USB_Control.Services;

namespace DG2072_USB_Control.Continuous.Sinusoid
{
    public class SinGen : ISinusoidEventHandler
    {
        // Device reference
        private readonly RigolDG2072 _device;

        // Active channel
        private int _activeChannel;

        // Event for logging
        public event EventHandler<string> LogEvent;

        // Constructor
        public SinGen(RigolDG2072 device, int channel, Window mainWindow)
        {
            _device = device;
            _activeChannel = channel;
        }

        // Property for the active channel
        public int ActiveChannel
        {
            get => _activeChannel;
            set => _activeChannel = value;
        }

        // Log helper method
        private void Log(string message)
        {
            LogEvent?.Invoke(this, message);
        }

        #region Core Functionality

        // Check if the device is connected
        private bool IsDeviceConnected()
        {
            return _device != null && _device.IsConnected;
        }

        // Apply sine wave parameters
        public void ApplySineParameters()
        {
            if (!IsDeviceConnected()) return;

            try
            {
                // Get current parameters
                double frequency = _device.GetFrequency(_activeChannel);
                double amplitude = _device.GetAmplitude(_activeChannel);
                double offset = _device.GetOffset(_activeChannel);
                double phase = _device.GetPhase(_activeChannel);

                // Apply sine waveform with parameters
                _device.ApplyWaveform(_activeChannel, "SINE", frequency, amplitude, offset, phase);
                Log($"Applied Sine waveform to CH{_activeChannel} with Freq={UnitConversionUtility.FormatWithMinimumDecimals(frequency)}Hz, " +
                    $"Amp={UnitConversionUtility.FormatWithMinimumDecimals(amplitude)}Vpp, " +
                    $"Offset={UnitConversionUtility.FormatWithMinimumDecimals(offset)}V, " +
                    $"Phase={UnitConversionUtility.FormatWithMinimumDecimals(phase)}°");
            }
            catch (Exception ex)
            {
                Log($"Error applying sine parameters: {ex.Message}");
            }
        }

        // Apply sine wave directly with provided parameters
        public void ApplySineWaveform(double frequency, double amplitude, double offset, double phase)
        {
            if (!IsDeviceConnected()) return;

            try
            {
                // Apply sine waveform with specified parameters
                _device.ApplyWaveform(_activeChannel, "SINE", frequency, amplitude, offset, phase);
                Log($"Applied Sine waveform to CH{_activeChannel} with Freq={UnitConversionUtility.FormatWithMinimumDecimals(frequency)}Hz, " +
                    $"Amp={UnitConversionUtility.FormatWithMinimumDecimals(amplitude)}Vpp, " +
                    $"Offset={UnitConversionUtility.FormatWithMinimumDecimals(offset)}V, " +
                    $"Phase={UnitConversionUtility.FormatWithMinimumDecimals(phase)}°");
            }
            catch (Exception ex)
            {
                Log($"Error applying sine waveform: {ex.Message}");
            }
        }

        // Refresh the UI with current values from device
        public void RefreshSineParameters()
        {
            if (!IsDeviceConnected()) return;

            try
            {
                // Sine waves don't have specialized parameters beyond the basic ones,
                // so this is mainly for consistency with other waveform classes
                Log($"Refreshed Sine parameters for CH{_activeChannel}");
            }
            catch (Exception ex)
            {
                Log($"Error refreshing sine parameters: {ex.Message}");
            }
        }

        #endregion
    }
}