using System;
using System.Windows;
using DG2072_USB_Control.Services;

namespace DG2072_USB_Control.Modulation.PSK
{
    public class PSKModulation : ModulationBase
    {
        public PSKModulation(RigolDG2072 device, int channel, Window mainWindow)
            : base(device, channel, mainWindow)
        {
        }

        public override void ApplyModulation()
        {
            if (!IsDeviceConnected())
            {
                Log("Device not connected. Cannot apply PSK modulation.");
                return;
            }

            try
            {
                // Get PSK parameters from UI (assuming control naming convention)
                bool isEnabled = IsToggleButtonChecked("PSKStateToggle", false);
                string source = GetComboBoxSelectedValue("PSKSourceComboBox", "Internal");
                double rate = GetDoubleFromTextBox("PSKRateTextBox", 100.0);
                string rateUnit = GetComboBoxSelectedValue("PSKRateUnitComboBox", "Hz");
                double phase = GetDoubleFromTextBox("PSKPhaseTextBox", 180.0); // Phase in degrees

                // Convert rate based on unit
                double rateMultiplier = UnitConversionUtility.GetFrequencyMultiplier(rateUnit);
                double keyingRate = rate * rateMultiplier;

                // Apply PSK modulation parameters
                Device.SendCommand($"SOURCE{ActiveChannelNumber}:PSKey:SOURCE {source.ToUpper()}");
                Device.SendCommand($"SOURCE{ActiveChannelNumber}:PSKey:RATE {keyingRate}");
                Device.SendCommand($"SOURCE{ActiveChannelNumber}:PSKey:PHASE {phase}");

                // Enable/disable PSK modulation
                Device.SendCommand($"SOURCE{ActiveChannelNumber}:PSKey:STATE {(isEnabled ? "ON" : "OFF")}");

                Log($"Applied PSK Modulation to CH{ActiveChannelNumber}: " +
                    $"State={isEnabled}, Source={source}, Rate={keyingRate}Hz, " +
                    $"Phase={phase}°");
            }
            catch (Exception ex)
            {
                Log($"Error applying PSK modulation: {ex.Message}");
            }
        }

        public override void SetModulationState(bool enabled)
        {
            if (!IsDeviceConnected()) return;

            try
            {
                Device.SendCommand($"SOURCE{ActiveChannelNumber}:PSKey:STATE {(enabled ? "ON" : "OFF")}");
                Log($"Set PSK modulation state to {(enabled ? "ON" : "OFF")} for CH{ActiveChannelNumber}");
            }
            catch (Exception ex)
            {
                Log($"Error setting PSK modulation state: {ex.Message}");
            }
        }

        public override void SetModulationSource(string source)
        {
            if (!IsDeviceConnected()) return;

            try
            {
                Device.SendCommand($"SOURCE{ActiveChannelNumber}:PSKey:SOURCE {source.ToUpper()}");
                Log($"Set PSK modulation source to {source} for CH{ActiveChannelNumber}");
            }
            catch (Exception ex)
            {
                Log($"Error setting PSK modulation source: {ex.Message}");
            }
        }

        public override void RefreshParameters()
        {
            if (!IsDeviceConnected()) return;

            try
            {
                // Query PSK modulation parameters
                string state = Device.SendQuery($"SOURCE{ActiveChannelNumber}:PSKey:STATE?").Trim();
                string source = Device.SendQuery($"SOURCE{ActiveChannelNumber}:PSKey:SOURCE?").Trim();
                string rate = Device.SendQuery($"SOURCE{ActiveChannelNumber}:PSKey:RATE?").Trim();
                string phase = Device.SendQuery($"SOURCE{ActiveChannelNumber}:PSKey:PHASE?").Trim();

                // Update UI
                Log($"PSK Modulation parameters for CH{ActiveChannelNumber}: " +
                    $"State={state}, Source={source}, Rate={rate}Hz, " +
                    $"Phase={phase}°");

                // TODO: Update UI controls if needed
            }
            catch (Exception ex)
            {
                Log($"Error refreshing PSK modulation parameters: {ex.Message}");
            }
        }
    }
}