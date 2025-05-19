using System;
using System.Windows;
using DG2072_USB_Control.Services;

namespace DG2072_USB_Control.Modulation.PM
{
    public class PMModulation : ModulationBase
    {
        public PMModulation(RigolDG2072 device, int channel, Window mainWindow)
            : base(device, channel, mainWindow)
        {
        }

        public override void ApplyModulation()
        {
            if (!IsDeviceConnected())
            {
                Log("Device not connected. Cannot apply PM modulation.");
                return;
            }

            try
            {
                // Get PM parameters from UI (assuming similar control naming convention)
                bool isEnabled = IsToggleButtonChecked("PMStateToggle", false);
                string source = GetComboBoxSelectedValue("PMSourceComboBox", "Internal");
                double deviation = GetDoubleFromTextBox("PMDeviationTextBox", 90.0); // Phase deviation in degrees
                string waveform = GetComboBoxSelectedValue("PMWaveformComboBox", "Sine");
                double frequency = GetDoubleFromTextBox("PMFrequencyTextBox", 10.0);
                string freqUnit = GetComboBoxSelectedValue("PMFrequencyUnitComboBox", "Hz");

                // Convert frequency based on unit
                double freqMultiplier = UnitConversionUtility.GetFrequencyMultiplier(freqUnit);
                double modFrequency = frequency * freqMultiplier;

                // Apply PM modulation parameters
                Device.SendCommand($"SOURCE{ActiveChannelNumber}:PM:SOURCE {source.ToUpper()}");
                Device.SendCommand($"SOURCE{ActiveChannelNumber}:PM:DEVIATION {deviation}");

                if (source.ToUpper() == "INTERNAL")
                {
                    Device.SendCommand($"SOURCE{ActiveChannelNumber}:PM:INTERNAL:FUNCTION {waveform.ToUpper()}");
                    Device.SendCommand($"SOURCE{ActiveChannelNumber}:PM:INTERNAL:FREQUENCY {modFrequency}");
                }

                // Enable/disable PM modulation
                Device.SendCommand($"SOURCE{ActiveChannelNumber}:PM:STATE {(isEnabled ? "ON" : "OFF")}");

                Log($"Applied PM Modulation to CH{ActiveChannelNumber}: " +
                    $"State={isEnabled}, Source={source}, Deviation={deviation}°, " +
                    $"Waveform={waveform}, Frequency={modFrequency}Hz");
            }
            catch (Exception ex)
            {
                Log($"Error applying PM modulation: {ex.Message}");
            }
        }

        public override void SetModulationState(bool enabled)
        {
            if (!IsDeviceConnected()) return;

            try
            {
                Device.SendCommand($"SOURCE{ActiveChannelNumber}:PM:STATE {(enabled ? "ON" : "OFF")}");
                Log($"Set PM modulation state to {(enabled ? "ON" : "OFF")} for CH{ActiveChannelNumber}");
            }
            catch (Exception ex)
            {
                Log($"Error setting PM modulation state: {ex.Message}");
            }
        }

        public override void SetModulationSource(string source)
        {
            if (!IsDeviceConnected()) return;

            try
            {
                Device.SendCommand($"SOURCE{ActiveChannelNumber}:PM:SOURCE {source.ToUpper()}");
                Log($"Set PM modulation source to {source} for CH{ActiveChannelNumber}");
            }
            catch (Exception ex)
            {
                Log($"Error setting PM modulation source: {ex.Message}");
            }
        }

        public override void RefreshParameters()
        {
            if (!IsDeviceConnected()) return;

            try
            {
                // Query PM modulation parameters
                string state = Device.SendQuery($"SOURCE{ActiveChannelNumber}:PM:STATE?").Trim();
                string source = Device.SendQuery($"SOURCE{ActiveChannelNumber}:PM:SOURCE?").Trim();
                string deviation = Device.SendQuery($"SOURCE{ActiveChannelNumber}:PM:DEVIATION?").Trim();
                string waveform = Device.SendQuery($"SOURCE{ActiveChannelNumber}:PM:INTERNAL:FUNCTION?").Trim();
                string frequency = Device.SendQuery($"SOURCE{ActiveChannelNumber}:PM:INTERNAL:FREQUENCY?").Trim();

                // Update UI
                Log($"PM Modulation parameters for CH{ActiveChannelNumber}: " +
                    $"State={state}, Source={source}, Deviation={deviation}°, " +
                    $"Waveform={waveform}, Frequency={frequency}Hz");

                // TODO: Update UI controls if needed
            }
            catch (Exception ex)
            {
                Log($"Error refreshing PM modulation parameters: {ex.Message}");
            }
        }
    }
}