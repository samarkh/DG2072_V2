using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DG2072_USB_Control.Services;

namespace DG2072_USB_Control.Continuous.Square
{
    public class SquareGen : WaveformGenerator, ISquareEventHandler
    {
        // UI elements
        private readonly TextBox _dutyCycleTextBox;

        // Update timer for debouncing
        private DispatcherTimer _dutyCycleUpdateTimer;

        // Constructor
        public SquareGen(RigolDG2072 device, int channel, Window mainWindow)
            : base(device, channel, mainWindow)
        {
            // Initialize UI references
            _dutyCycleTextBox = mainWindow.FindName("DutyCycle") as TextBox;
        }

        #region ISquareEventHandler Implementation

        public void OnDutyCycleTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsDeviceConnected()) return;
            if (!double.TryParse(_dutyCycleTextBox.Text, out double dutyCycle)) return;

            // Use base class timer method
            CreateOrResetTimer(ref _dutyCycleUpdateTimer, () => {
                if (double.TryParse(_dutyCycleTextBox.Text, out double duty))
                {
                    ApplyDutyCycle(duty);
                }
            });
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

        // Apply duty cycle value to the device
        private void ApplyDutyCycle(double dutyCycle)
        {
            if (!IsDeviceConnected()) return;

            try
            {
                // Ensure duty cycle is within valid range
                dutyCycle = Math.Max(0, Math.Min(100, dutyCycle));

                // Apply to the device using base class properties
                Device.SetDutyCycle(ActiveChannel, dutyCycle);
                Log($"Set CH{ActiveChannel} duty cycle to {dutyCycle}%");
            }
            catch (Exception ex)
            {
                Log($"Error applying duty cycle: {ex.Message}");
            }
        }

        #endregion

        #region Base Class Overrides

        // Override from base class
        public override void ApplyParameters()
        {
            if (!IsDeviceConnected()) return;

            try
            {
                // Get common parameters using base class methods
                double frequency = GetFrequencyFromUI();
                double amplitude = GetAmplitudeFromUI();
                double offset = GetOffsetFromUI();
                double phase = GetPhaseFromUI();

                // Get square-specific parameters
                double dutyCycle = 50.0; // Default
                if (_dutyCycleTextBox != null && double.TryParse(_dutyCycleTextBox.Text, out double duty))
                {
                    dutyCycle = duty;
                }

                // Apply square waveform
                Device.ApplyWaveform(ActiveChannel, "SQUARE", frequency, amplitude, offset, phase);

                // Apply duty cycle
                Device.SetDutyCycle(ActiveChannel, dutyCycle);

                Log($"Applied Square waveform to CH{ActiveChannel} with Freq={UnitConversionUtility.FormatWithMinimumDecimals(frequency)}Hz, " +
                    $"Amp={UnitConversionUtility.FormatWithMinimumDecimals(amplitude)}Vpp, " +
                    $"Offset={UnitConversionUtility.FormatWithMinimumDecimals(offset)}V, " +
                    $"Phase={UnitConversionUtility.FormatWithMinimumDecimals(phase)}°, " +
                    $"Duty={dutyCycle}%");
            }
            catch (Exception ex)
            {
                Log($"Error applying square parameters: {ex.Message}");
            }
        }

        // Override from base class
        public override void RefreshParameters()
        {
            if (!IsDeviceConnected()) return;

            try
            {
                // Update duty cycle value
                UpdateDutyCycleValue();

                Log($"Refreshed Square parameters for CH{ActiveChannel}");
            }
            catch (Exception ex)
            {
                Log($"Error refreshing square parameters: {ex.Message}");
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
                string currentWaveform = Device.SendQuery($":SOUR{ActiveChannel}:FUNC?").Trim().ToUpper();
                if (currentWaveform.Contains("SQU"))
                {
                    double dutyCycle = Device.GetDutyCycle(ActiveChannel);
                    _dutyCycleTextBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(dutyCycle);
                }
            }
            catch (Exception ex)
            {
                Log($"Error updating duty cycle value: {ex.Message}");
            }
        }

        // Backward compatibility method
        public void ApplySquareParameters() => ApplyParameters();

        #endregion
    }
}