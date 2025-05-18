using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DG2072_USB_Control.Services;

namespace DG2072_USB_Control.Continuous.Ramp
{
    public class RampGen : WaveformGenerator, IRampEventHandler
    {
        // UI elements specific to Ramp
        private readonly TextBox _symmetryTextBox;

        // Timer for debouncing
        private DispatcherTimer _symmetryUpdateTimer;

        // Constructor
        public RampGen(RigolDG2072 device, int channel, Window mainWindow)
            : base(device, channel, mainWindow)
        {
            // Find UI elements
            _symmetryTextBox = mainWindow.FindName("Symm") as TextBox;
        }

        #region WaveformGenerator Overrides

        // Implement required abstract methods from WaveformGenerator
        public override void ApplyParameters()
        {
            if (!IsDeviceConnected()) return;

            try
            {
                // Apply symmetry
                if (double.TryParse(_symmetryTextBox.Text, out double symmetry))
                {
                    Device.SetSymmetry(ActiveChannel, symmetry);
                    Log($"Applied ramp symmetry for CH{ActiveChannel}: {symmetry}%");
                }

                // Apply standard waveform parameters using base class helper methods
                double frequency = GetFrequencyFromUI();
                double amplitude = GetAmplitudeFromUI();
                double offset = GetOffsetFromUI();
                double phase = GetPhaseFromUI();

                // Apply waveform with parameters
                Device.ApplyWaveform(ActiveChannel, "RAMP", frequency, amplitude, offset, phase);
                Log($"Applied ramp waveform to CH{ActiveChannel} with Freq={frequency}Hz, Amp={amplitude}Vpp, Offset={offset}V, Phase={phase}°");
            }
            catch (Exception ex)
            {
                Log($"Error applying ramp parameters: {ex.Message}");
            }
        }

        public override void RefreshParameters()
        {
            if (!IsDeviceConnected()) return;

            try
            {
                // Update symmetry value from device
                UpdateSymmetryValue();
                Log($"Refreshed ramp parameters for CH{ActiveChannel}");
            }
            catch (Exception ex)
            {
                Log($"Error refreshing ramp parameters: {ex.Message}");
            }
        }

        #endregion

        #region Event Handlers

        public void OnSymmetryTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsDeviceConnected()) return;
            if (!double.TryParse(_symmetryTextBox.Text, out double symmetry)) return;

            // Use a timer to debounce rapid changes
            CreateOrResetTimer(ref _symmetryUpdateTimer, () => {
                if (double.TryParse(_symmetryTextBox.Text, out double symm))
                {
                    ApplySymmetry(symm);
                }
            });
        }

        public void OnSymmetryLostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(_symmetryTextBox.Text, out double symmetry))
            {
                // Format the value
                _symmetryTextBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(symmetry);

                // Apply the formatted value
                ApplySymmetry(symmetry);
            }
        }

        #endregion

        #region Helper Methods

        public void UpdateSymmetryValue()
        {
            if (!IsDeviceConnected() || _symmetryTextBox == null) return;

            try
            {
                double symmetry = Device.GetSymmetry(ActiveChannel);
                _symmetryTextBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(symmetry);
            }
            catch (Exception ex)
            {
                Log($"Error updating symmetry value: {ex.Message}");
            }
        }

        private void ApplySymmetry(double symmetry)
        {
            if (!IsDeviceConnected()) return;

            try
            {
                // Ensure symmetry is within valid range (0-100%)
                symmetry = Math.Max(0, Math.Min(100, symmetry));

                // Set the symmetry
                Device.SetSymmetry(ActiveChannel, symmetry);
                Log($"Set ramp symmetry to {symmetry}%");
            }
            catch (Exception ex)
            {
                Log($"Error applying symmetry: {ex.Message}");
            }
        }

        #endregion
    }
}