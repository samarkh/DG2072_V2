using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DG2072_USB_Control.Services;

namespace DG2072_USB_Control.Continuous.Square
{
    public class SquareGen : ISquareEventHandler
    {
        // Device reference
        private readonly RigolDG2072 _device;

        // Active channel
        private int _activeChannel;

        // UI elements
        private readonly TextBox _dutyCycleTextBox;

        // Update timer for debouncing
        private DispatcherTimer _dutyCycleUpdateTimer;

        // Event for logging
        public event EventHandler<string> LogEvent;

        // Constructor
        public SquareGen(RigolDG2072 device, int channel, Window mainWindow)
        {
            _device = device;
            _activeChannel = channel;

            // Initialize UI references
            _dutyCycleTextBox = mainWindow.FindName("DutyCycle") as TextBox;
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

        #region ISquareEventHandler Implementation

        public void OnDutyCycleTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsDeviceConnected()) return;
            if (!double.TryParse(_dutyCycleTextBox.Text, out double dutyCycle)) return;

            // Use a timer to debounce rapid changes
            if (_dutyCycleUpdateTimer == null)
            {
                _dutyCycleUpdateTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                _dutyCycleUpdateTimer.Tick += (s, args) =>
                {
                    _dutyCycleUpdateTimer.Stop();
                    if (double.TryParse(_dutyCycleTextBox.Text, out double duty))
                    {
                        ApplyDutyCycle(duty);
                    }
                };
            }

            _dutyCycleUpdateTimer.Stop();
            _dutyCycleUpdateTimer.Start();
        }

        public void OnDutyCycleLostFocus(object sender, RoutedEventArgs e)
        {
            if (!IsDeviceConnected()) return;
            if (double.TryParse(_dutyCycleTextBox.Text, out double dutyCycle))
            {
                // Ensure value is in valid range
                dutyCycle = Math.Max(0, Math.Min(100, dutyCycle));
                _dutyCycleTextBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(dutyCycle);

                // Apply the value
                ApplyDutyCycle(dutyCycle);
            }
        }

        #endregion

        #region Core Functionality

        // Check if the device is connected
        private bool IsDeviceConnected()
        {
            return _device != null && _device.IsConnected;
        }

        // Apply duty cycle value to the device
        private void ApplyDutyCycle(double dutyCycle)
        {
            if (!IsDeviceConnected()) return;

            try
            {
                // Ensure duty cycle is within valid range
                dutyCycle = Math.Max(0, Math.Min(100, dutyCycle));

                // Apply to the device
                _device.SetDutyCycle(_activeChannel, dutyCycle);
                Log($"Set CH{_activeChannel} duty cycle to {dutyCycle}%");
            }
            catch (Exception ex)
            {
                Log($"Error applying duty cycle: {ex.Message}");
            }
        }

        #endregion

        #region Public Methods

        // Update duty cycle value in the UI from device
        public void UpdateDutyCycleValue()
        {
            if (!IsDeviceConnected()) return;

            try
            {
                // Only update if the waveform is Square
                string currentWaveform = _device.SendQuery($":SOUR{_activeChannel}:FUNC?").Trim().ToUpper();
                if (currentWaveform.Contains("SQU"))
                {
                    double dutyCycle = _device.GetDutyCycle(_activeChannel);
                    _dutyCycleTextBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(dutyCycle);
                }
            }
            catch (Exception ex)
            {
                Log($"Error updating duty cycle value: {ex.Message}");
            }
        }

        // Apply all square parameters
        public void ApplySquareParameters()
        {
            if (!IsDeviceConnected()) return;

            try
            {
                if (double.TryParse(_dutyCycleTextBox.Text, out double dutyCycle))
                {
                    ApplyDutyCycle(dutyCycle);
                }
            }
            catch (Exception ex)
            {
                Log($"Error applying square parameters: {ex.Message}");
            }
        }

        #endregion
    }
}