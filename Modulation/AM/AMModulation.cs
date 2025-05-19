using System;
using System.Windows;
using DG2072_USB_Control.Services;

namespace DG2072_USB_Control.Modulation.AM
{
    public class AMModulation : ModulationBase
    {
        public AMModulation(RigolDG2072 device, int channel, Window mainWindow)
            : base(device, channel, mainWindow)
        {
        }

        public override void ApplyModulation()
        {
            if (!IsDeviceConnected())
            {
                Log("Device not connected. Cannot apply AM modulation.");
                return;
            }

            try
            {
                // Get AM parameters from UI
                bool isEnabled = IsToggleButtonChecked("AMStateToggle", false);
                string source = GetComboBoxSelectedValue("AMSourceComboBox", "Internal");
                double depth = GetDoubleFromTextBox("AMDepthTextBox", 100.0);
                string waveform = GetComboBoxSelectedValue("AMWaveformComboBox", "Sine");
                double frequency = GetDoubleFromTextBox("AMFrequencyTextBox", 100.0);
                string freqUnit = GetComboBoxSelectedValue("AMFrequencyUnitComboBox", "Hz");

                // Convert frequency based on unit
                double freqMultiplier = UnitConversionUtility.GetFrequencyMultiplier(freqUnit);
                double modFrequency = frequency * freqMultiplier;

                // Apply AM modulation parameters
                Device.SendCommand($"SOURCE{ActiveChannelNumber}:AM:SOURCE {source.ToUpper()}");
                Device.SendCommand($"SOURCE{ActiveChannelNumber}:AM:DEPTH {depth}");

                if (source.ToUpper() == "INTERNAL")
                {
                    Device.SendCommand($"SOURCE{ActiveChannelNumber}:AM:INTERNAL:FUNCTION {waveform.ToUpper()}");
                    Device.SendCommand($"SOURCE{ActiveChannelNumber}:AM:INTERNAL:FREQUENCY {modFrequency}");
                }

                // Enable/disable AM modulation
                Device.SendCommand($"SOURCE{ActiveChannelNumber}:AM:STATE {(isEnabled ? "ON" : "OFF")}");

                Log($"Applied AM Modulation to CH{ActiveChannelNumber}: " +
                    $"State={isEnabled}, Source={source}, Depth={depth}%, " +
                    $"Waveform={waveform}, Frequency={modFrequency}Hz");
            }
            catch (Exception ex)
            {
                Log($"Error applying AM modulation: {ex.Message}");
            }
        }

        public override void SetModulationState(bool enabled)
        {
            if (!IsDeviceConnected()) return;

            try
            {
                Device.SendCommand($"SOURCE{ActiveChannelNumber}:AM:STATE {(enabled ? "ON" : "OFF")}");
                Log($"Set AM modulation state to {(enabled ? "ON" : "OFF")} for CH{ActiveChannelNumber}");
            }
            catch (Exception ex)
            {
                Log($"Error setting AM modulation state: {ex.Message}");
            }
        }

        public override void SetModulationSource(string source)
        {
            if (!IsDeviceConnected()) return;

            try
            {
                Device.SendCommand($"SOURCE{ActiveChannelNumber}:AM:SOURCE {source.ToUpper()}");
                Log($"Set AM modulation source to {source} for CH{ActiveChannelNumber}");
            }
            catch (Exception ex)
            {
                Log($"Error setting AM modulation source: {ex.Message}");
            }
        }

        public override void RefreshParameters()
        {
            if (!IsDeviceConnected()) return;

            try
            {
                // Query AM modulation parameters
                string state = Device.SendQuery($"SOURCE{ActiveChannelNumber}:AM:STATE?").Trim();
                string source = Device.SendQuery($"SOURCE{ActiveChannelNumber}:AM:SOURCE?").Trim();
                string depth = Device.SendQuery($"SOURCE{ActiveChannelNumber}:AM:DEPTH?").Trim();
                string waveform = Device.SendQuery($"SOURCE{ActiveChannelNumber}:AM:INTERNAL:FUNCTION?").Trim();
                string frequency = Device.SendQuery($"SOURCE{ActiveChannelNumber}:AM:INTERNAL:FREQUENCY?").Trim();

                // Update UI
                Log($"AM Modulation parameters for CH{ActiveChannelNumber}: " +
                    $"State={state}, Source={source}, Depth={depth}%, " +
                    $"Waveform={waveform}, Frequency={frequency}Hz");

                // TODO: Update UI controls if needed
            }
            catch (Exception ex)
            {
                Log($"Error refreshing AM modulation parameters: {ex.Message}");
            }
        }
    }
}