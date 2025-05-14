using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DG2072_USB_Control.Services;

namespace DG2072_USB_Control.Continuous.Noise
{
    public class NoiseGen : INoiseEventHandler
    {
        // Device reference
        private readonly RigolDG2072 _device;

        // Active channel
        private int _activeChannel;

        // Event for logging
        public event EventHandler<string> LogEvent;

        // Constructor
        public NoiseGen(RigolDG2072 device, int channel, Window mainWindow)
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

        // Apply noise waveform
        public void ApplyNoiseParameters()
        {
            if (!IsDeviceConnected()) return;

            try
            {
                // Get current parameters
                double amplitude = _device.GetAmplitude(_activeChannel);
                double offset = _device.GetOffset(_activeChannel);

                // Apply noise waveform with current amplitude and offset
                _device.SendCommand($":SOURCE{_activeChannel}:APPLY:NOIS {amplitude},{offset}");
                Log($"Applied Noise waveform to CH{_activeChannel} with " +
                    $"Amp={UnitConversionUtility.FormatWithMinimumDecimals(amplitude)}Vpp, " +
                    $"Offset={UnitConversionUtility.FormatWithMinimumDecimals(offset)}V");
            }
            catch (Exception ex)
            {
                Log($"Error applying noise parameters: {ex.Message}");
            }
        }

        // Refresh noise settings from the device
        public void RefreshNoiseParameters()
        {
            if (!IsDeviceConnected()) return;

            try
            {
                // For Noise waveforms, we only have amplitude and offset parameters
                // which are already handled by MainWindow.
                // This method exists for consistency with other waveform generators.
                Log($"Refreshed Noise parameters for CH{_activeChannel}");
            }
            catch (Exception ex)
            {
                Log($"Error refreshing noise parameters: {ex.Message}");
            }
        }

        #endregion
    }
}