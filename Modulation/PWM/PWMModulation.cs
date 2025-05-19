using System;
using System.Windows;
using DG2072_USB_Control.Services;

namespace DG2072_USB_Control.Modulation.PWM
{
    public class PWMModulation : ModulationBase
    {
        public PWMModulation(RigolDG2072 device, int channel, Window mainWindow)
            : base(device, channel, mainWindow)
        {
        }

        public override void ApplyModulation()
        {
            if (!IsDeviceConnected())
            {
                Log("Device not connected. Cannot apply PWM modulation.");
                return;
            }

            try
            {
                // Get PWM parameters from UI (assuming control naming convention)
                bool isEnabled = IsToggleButtonChecked("PWMStateToggle", false);
                string source = GetComboBoxSelectedValue("PWMSourceComboBox", "Internal");
                double dutyCycle = GetDoubleFromTextBox("PWMDutyCycleTextBox", 50.0); // In percentage
                string waveform = GetComboBoxSelectedValue("PWMWaveformComboBox", "Sine");
                double frequency = GetDoubleFromTextBox("PWMFrequencyTextBox", 10.0);
                string freqUnit = GetComboBoxSelectedValue("PWMFrequencyUnitComboBox", "Hz");

                // Convert frequency based on unit
                double freqMultiplier = UnitConversionUtility.GetFrequencyMultiplier(freqUnit);
                double modFrequency = frequency * freqMultiplier;

                // Apply PWM modulation parameters
                Device.SendCommand($"SOURCE{ActiveChannelNumber}:PWM:SOURCE {source.ToUpper()}");
                Device.SendCommand($"SOURCE{ActiveChannelNumber}:PWM:DUTY {dutyCycle}");

                if (source.ToUpper() == "INTERNAL")
                {
                    Device.SendCommand($"SOURCE{ActiveChannelNumber}:PWM:INTERNAL:FUNCTION {waveform.ToUpper()}");
                    Device.SendCommand($"SOURCE{ActiveChannelNumber}:PWM:INTERNAL:FREQUENCY {modFrequency}");
                }

                // Enable/disable PWM modulation
                Device.SendCommand($"SOURCE{ActiveChannelNumber}:PWM:STATE {(isEnabled ? "ON" : "OFF")}");

                Log($"Applied PWM Modulation to CH{ActiveChannelNumber}: " +
                    $"State={isEnabled}, Source={source}, Duty Cycle={dutyCycle}%, " +
                    $"Waveform={waveform}, Frequency={modFrequency}Hz");
            }
            catch (Exception ex)
            {
                Log($"Error applying PWM modulation: {ex.Message}");
            }
        }

        public override void SetModulationState(bool enabled)
        {
            if (!IsDeviceConnected()) return;

            try
            {
                Device.SendCommand($"SOURCE{ActiveChannelNumber}:PWM:STATE {(enabled ? "ON" : "OFF")}");
                Log($"Set PWM modulation state to {(enabled ? "ON" : "OFF")} for CH{ActiveChannelNumber}");
            }
            catch (Exception ex)
            {
                Log($"Error setting PWM modulation state: {ex.Message}");
            }
        }

        public override void SetModulationSource(string source)
        {
            if (!IsDeviceConnected()) return;

            try
            {
                Device.SendCommand($"SOURCE{ActiveChannelNumber}:PWM:SOURCE {source.ToUpper()}");
                Log($"Set PWM modulation source to {source} for CH{ActiveChannelNumber}");
            }
            catch (Exception ex)
            {
                Log($"Error setting PWM modulation source: {ex.Message}");
            }
        }

        public override void RefreshParameters()
        {
            if (!IsDeviceConnected()) return;

            try
            {
                // Query PWM modulation parameters
                string state = Device.SendQuery($"SOURCE{ActiveChannelNumber}:PWM:STATE?").Trim();
                string source = Device.SendQuery($"SOURCE{ActiveChannelNumber}:PWM:SOURCE?").Trim();
                string duty = Device.SendQuery($"SOURCE{ActiveChannelNumber}:PWM:DUTY?").Trim();
                string waveform = Device.SendQuery($"SOURCE{ActiveChannelNumber}:PWM:INTERNAL:FUNCTION?").Trim();
                string frequency = Device.SendQuery($"SOURCE{ActiveChannelNumber}:PWM:INTERNAL:FREQUENCY?").Trim();

                // Update UI
                Log($"PWM Modulation parameters for CH{ActiveChannelNumber}: " +
                    $"State={state}, Source={source}, Duty Cycle={duty}%, " +
                    $"Waveform={waveform}, Frequency={frequency}Hz");

                // TODO: Update UI controls if needed
            }
            catch (Exception ex)
            {
                Log($"Error refreshing PWM modulation parameters: {ex.Message}");
            }
        }
    }
}