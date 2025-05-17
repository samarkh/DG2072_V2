using System;
using System.Windows;
using System.Windows.Controls;
using DG2072_USB_Control.Services;

namespace DG2072_USB_Control.Continuous.Sinusoid
{
    public class SinGen : WaveformGenerator, ISinusoidEventHandler
    {
        public SinGen(RigolDG2072 device, int channel, Window mainWindow)
            : base(device, channel, mainWindow)
        {
        }

        #region Core Functionality

        /// <summary>
        /// Apply sine wave parameters
        /// </summary>
        public override void ApplyParameters()
        {
            if (!IsDeviceConnected()) return;

            try
            {
                // Get current parameters
                double frequency = GetFrequencyFromUI();
                double amplitude = GetAmplitudeFromUI();
                double offset = GetOffsetFromUI();
                double phase = GetPhaseFromUI();

                // Apply sine waveform with parameters
                Device.ApplyWaveform(ActiveChannel, "SINE", frequency, amplitude, offset, phase);
                Log($"Applied Sine waveform to CH{ActiveChannel} with Freq={UnitConversionUtility.FormatWithMinimumDecimals(frequency)}Hz, " +
                    $"Amp={UnitConversionUtility.FormatWithMinimumDecimals(amplitude)}Vpp, " +
                    $"Offset={UnitConversionUtility.FormatWithMinimumDecimals(offset)}V, " +
                    $"Phase={UnitConversionUtility.FormatWithMinimumDecimals(phase)}°");
            }
            catch (Exception ex)
            {
                Log($"Error applying sine parameters: {ex.Message}");
            }
        }

        /// <summary>
        /// Apply sine wave directly with provided parameters
        /// </summary>
        public void ApplySineWaveform(double frequency, double amplitude, double offset, double phase)
        {
            if (!IsDeviceConnected()) return;

            try
            {
                // Apply sine waveform with specified parameters
                Device.ApplyWaveform(ActiveChannel, "SINE", frequency, amplitude, offset, phase);
                Log($"Applied Sine waveform to CH{ActiveChannel} with Freq={UnitConversionUtility.FormatWithMinimumDecimals(frequency)}Hz, " +
                    $"Amp={UnitConversionUtility.FormatWithMinimumDecimals(amplitude)}Vpp, " +
                    $"Offset={UnitConversionUtility.FormatWithMinimumDecimals(offset)}V, " +
                    $"Phase={UnitConversionUtility.FormatWithMinimumDecimals(phase)}°");
            }
            catch (Exception ex)
            {
                Log($"Error applying sine waveform: {ex.Message}");
            }
        }

        /// <summary>
        /// Refresh the UI with current values from device
        /// </summary>
        public override void RefreshParameters()
        {
            if (!IsDeviceConnected()) return;

            try
            {
                // Sine waves don't have specialized parameters beyond the basic ones
                Log($"Refreshed Sine parameters for CH{ActiveChannel}");
            }
            catch (Exception ex)
            {
                Log($"Error refreshing sine parameters: {ex.Message}");
            }
        }

        #endregion

        // For backward compatibility with MainWindow.xaml.cs
        public void ApplySineParameters() => ApplyParameters();
        public void RefreshSineParameters() => RefreshParameters();
    }
}