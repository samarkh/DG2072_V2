using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DG2072_USB_Control.Services;

namespace DG2072_USB_Control.Continuous.Ramp
{
    public class RampGen : IRampEventHandler
    {
        // Device reference
        private readonly RigolDG2072 _device;

        // Active channel
        private int _activeChannel;

        // UI elements
        private readonly TextBox _symmetryTextBox;

        // Update timer for debouncing
        private DispatcherTimer _symmetryUpdateTimer;

        // Event for logging
        public event EventHandler<string> LogEvent;

        // Constructor
        public RampGen(RigolDG2072 device, int channel, Window mainWindow)
        {
            _device = device;
            _activeChannel = channel;

            // Initialize UI references
            _symmetryTextBox = mainWindow.FindName("Symm") as TextBox;
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

        #region IRampEventHandler Implementation

        public void OnSymmetryTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsDeviceConnected()) return;
            if (!double.TryParse(_symmetryTextBox.Text, out double symmetry)) return;

            // Use a timer to debounce rapid changes
            if (_symmetryUpdateTimer == null)
            {
                _symmetryUpdateTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                _symmetryUpdateTimer.Tick += (s, args) =>
                {
                    _symmetryUpdateTimer.Stop();
                    if (double.TryParse(_symmetryTextBox.Text, out double sym))
                    {
                        ApplySymmetry(sym);
                    }
                };
            }

            _symmetryUpdateTimer.Stop();
            _symmetryUpdateTimer.Start();
        }

        public void OnSymmetryLostFocus(object sender, RoutedEventArgs e)
        {
            if (!IsDeviceConnected()) return;
            if (double.TryParse(_symmetryTextBox.Text, out double symmetry))
            {
                // Ensure value is in valid range
                symmetry = Math.Max(0, Math.Min(100, symmetry));
                _symmetryTextBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(symmetry);

                // Apply the value
                ApplySymmetry(symmetry);
            }
        }

        #endregion

        #region Core Functionality

        // Check if the device is connected
        private bool IsDeviceConnected()
        {
            return _device != null && _device.IsConnected;
        }

        // Apply symmetry value to the device
        private void ApplySymmetry(double symmetry)
        {
            if (!IsDeviceConnected()) return;

            try
            {
                // Ensure symmetry is within valid range (0-100%)
                symmetry = Math.Max(0, Math.Min(100, symmetry));

                // Apply to the device
                _device.SetSymmetry(_activeChannel, symmetry);
                Log($"Set CH{_activeChannel} symmetry to {symmetry}%");
            }
            catch (Exception ex)
            {
                Log($"Error applying symmetry: {ex.Message}");
            }
        }

        #endregion

        #region Public Methods

        // Update symmetry value in the UI from device
        public void UpdateSymmetryValue()
        {
            if (!IsDeviceConnected()) return;

            try
            {
                // Only update if the waveform is Ramp
                string currentWaveform = _device.SendQuery($":SOUR{_activeChannel}:FUNC?").Trim().ToUpper();
                if (currentWaveform.Contains("RAMP"))
                {
                    double symmetry = _device.GetSymmetry(_activeChannel);
                    _symmetryTextBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(symmetry);
                }
            }
            catch (Exception ex)
            {
                Log($"Error updating symmetry value: {ex.Message}");
            }
        }

        // Apply all ramp parameters
        public void ApplyRampParameters()
        {
            if (!IsDeviceConnected()) return;

            try
            {
                if (double.TryParse(_symmetryTextBox.Text, out double symmetry))
                {
                    ApplySymmetry(symmetry);
                }
            }
            catch (Exception ex)
            {
                Log($"Error applying ramp parameters: {ex.Message}");
            }
        }

        #endregion
    }
}