using System;
using System.Windows;
using DG2072_USB_Control.Services;

namespace DG2072_USB_Control.Modulation.ASK
{
    public class ASKModulation : ModulationBase
    {
        public ASKModulation(RigolDG2072 device, int channel, Window mainWindow)
            : base(device, channel, mainWindow)
        {
        }

        public override void ApplyModulation()
        {
            if (!IsDeviceConnected())
            {
                Log("Device not connected. Cannot apply ASK modulation.");
                return;
            }

            try
            {
                // Get ASK parameters from UI (assuming control naming convention)
                bool isEnabled = IsToggleButtonChecked("ASKStateToggle", false);
                string source = GetComboBoxSelectedValue("ASKSourceComboBox", "Internal");
                double rate = GetDoubleFromTextBox("ASKRateTextBox", 100.0);
                string rateUnit = GetComboBoxSelectedValue("ASKRateUnitComboBox", "Hz");

                // Convert rate based on unit
                double rateMultiplier = UnitConversionUtility.GetFrequencyMultiplier(rateUnit);
                double keyingRate = rate * rateMultiplier;

                // Apply ASK modulation parameters
                Device.SendCommand($"SOURCE{ActiveChannelNumber}:ASKey:SOURCE {source.ToUpper()}");
                Device.SendCommand($"SOURCE{ActiveChannelNumber}:ASKey:RATE {keyingRate}");

                // Enable/disable ASK modulation
                Device.SendCommand($"SOURCE{ActiveChannelNumber}:ASKey:STATE {(isEnabled ? "ON" : "OFF")}");

                Log($"Applied ASK Modulation to CH{ActiveChannelNumber}: " +
                    $"State={isEnabled}, Source={source}, Rate={keyingRate}Hz");
            }
            catch (Exception ex)
            {
                Log($"Error applying ASK modulation: {ex.Message}");
            }
        }

        public override void SetModulationState(bool enabled)
        {
            if (!IsDeviceConnected()) return;

            try
            {
                Device.SendCommand($"SOURCE{ActiveChannelNumber}:ASKey:STATE {(enabled ? "ON" : "OFF")}");
                Log($"Set ASK modulation state to {(enabled ? "ON" : "OFF")} for CH{ActiveChannelNumber}");
            }
            catch (Exception ex)
            {
                Log($"Error setting ASK modulation state: {ex.Message}");
            }
        }

        public override void SetModulationSource(string source)
        {
            if (!IsDeviceConnected()) return;

            try
            {
                Device.SendCommand($"SOURCE{ActiveChannelNumber}:ASKey:SOURCE {source.ToUpper()}");
                Log($"Set ASK modulation source to {source} for CH{ActiveChannelNumber}");
            }
            catch (Exception ex)
            {
                Log($"Error setting ASK modulation source: {ex.Message}");
            }
        }

        public override void RefreshParameters()
        {
            if (!IsDeviceConnected()) return;

            try
            {
                // Query ASK modulation parameters
                string state = Device.SendQuery($"SOURCE{ActiveChannelNumber}:ASKey:STATE?").Trim();
                string source = Device.SendQuery($"SOURCE{ActiveChannelNumber}:ASKey:SOURCE?").Trim();
                string rate = Device.SendQuery($"SOURCE{ActiveChannelNumber}:ASKey:RATE?").Trim();

                // Update UI
                Log($"ASK Modulation parameters for CH{ActiveChannelNumber}: " +
                    $"State={state}, Source={source}, Rate={rate}Hz");

                // TODO: Update UI controls if needed
            }
            catch (Exception ex)
            {
                Log($"Error refreshing ASK modulation parameters: {ex.Message}");
            }
        }
    }
}