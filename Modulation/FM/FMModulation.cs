using System;
using System.Windows;
using DG2072_USB_Control.Services;

namespace DG2072_USB_Control.Modulation.FM
{
    public class FMModulation : ModulationBase
    {
        public FMModulation(RigolDG2072 device, int channel, Window mainWindow)
            : base(device, channel, mainWindow)
        {
        }

        public override void ApplyModulation()
        {
            if (!IsDeviceConnected())
            {
                Log("Device not connected. Cannot apply FM modulation.");
                return;
            }

            try
            {
                // Get FM parameters from UI
                bool isEnabled = IsToggleButtonChecked("FMStateToggle", false);
                string source = GetComboBoxSelectedValue("FMSourceComboBox", "Internal");
                double deviation = GetDoubleFromTextBox("FMDeviationTextBox", 1.0);
                string deviationUnit = GetComboBoxSelectedValue("FMDeviationUnitComboBox", "Hz");
                string waveform = GetComboBoxSelectedValue("FMWaveformComboBox", "Sine");
                double frequency = GetDoubleFromTextBox("FMFrequencyTextBox", 10.0);
                string freqUnit = GetComboBoxSelectedValue("FMFrequencyUnitComboBox", "Hz");

                // Convert values based on units
                double freqMultiplier = UnitConversionUtility.GetFrequencyMultiplier(freqUnit);
                double modFrequency = frequency * freqMultiplier;
                
                double devMultiplier = UnitConversionUtility.GetFrequencyMultiplier(deviationUnit);
                double modDeviation = deviation * devMultiplier;

                // Apply FM modulation parameters
                Device.SendCommand($"SOURCE{ActiveChannelNumber}:FM:SOURCE {source.ToUpper()}");
                Device.SendCommand($"SOURCE{ActiveChannelNumber}:FM:DEVIATION {modDeviation}");

                if (source.ToUpper() == "INTERNAL")
                {
                    Device.SendCommand($"SOURCE{ActiveChannelNumber}:FM:INTERNAL:FUNCTION {waveform.ToUpper()}");
                    Device.SendCommand($"SOURCE{ActiveChannelNumber}:FM:INTERNAL:FREQUENCY {modFrequency}");
                }

                // Enable/disable FM modulation
                Device.SendCommand($"SOURCE{ActiveChannelNumber}:FM:STATE {(isEnabled ? "ON" : "OFF")}");

                Log($"Applied FM Modulation to CH{ActiveChannelNumber}: " +
                    $"State={isEnabled}, Source={source}, Deviation={modDeviation}Hz, " +
                    $"Waveform={waveform}, Frequency={modFrequency}Hz");
            }
            catch (Exception ex)
            {
                Log($"Error applying FM modulation: {ex.Message}");
            }
        }

        public override void SetModulationState(bool enabled)
        {
            if (!IsDeviceConnected()) return;

            try
            {
                Device.SendCommand($"SOURCE{ActiveChannelNumber}:FM:STATE {(enabled ? "ON" : "OFF")}");
                Log($"Set FM modulation state to {(enabled ? "ON" : "OFF")} for CH{ActiveChannelNumber}");
            }
            catch (Exception ex)
            {
                Log($"Error setting FM modulation state: {ex.Message}");
            }
        }

        public override void SetModulationSource(string source)
        {
            if (!IsDeviceConnected()) return;

            try
            {
                Device.SendCommand($"SOURCE{ActiveChannelNumber}:FM:SOURCE {source.ToUpper()}");
                Log($"Set FM modulation source to {source} for CH{ActiveChannelNumber}");
            }
            catch (Exception ex)
            {
                Log($"Error setting FM modulation source: {ex.Message}");
            }
        }

        public override void RefreshParameters()
        {
            if (!IsDeviceConnected()) return;

            try
            {
                // Query FM modulation parameters
                string state = Device.SendQuery($"SOURCE{ActiveChannelNumber}:FM:STATE?").Trim();
                string source = Device.SendQuery($"SOURCE{ActiveChannelNumber}:FM:SOURCE?").Trim();
                string deviation = Device.SendQuery($"SOURCE{ActiveChannelNumber}:FM:DEVIATION?").Trim();
                string waveform = Device.SendQuery($"SOURCE{ActiveChannelNumber}:FM:INTERNAL:FUNCTION?").Trim();
                string frequency = Device.SendQuery($"SOURCE{ActiveChannelNumber}:FM:INTERNAL:FREQUENCY?").Trim();

                // Update UI
                Log($"FM Modulation parameters for CH{ActiveChannelNumber}: " +
                    $"State={state}, Source={source}, Deviation={deviation}Hz, " +
                    $"Waveform={waveform}, Frequency={frequency}Hz");

                // TODO: Update UI controls if needed
            }
            catch (Exception ex)
            {
                Log($"Error refreshing FM modulation parameters: {ex.Message}");
            }
        }
    }
}