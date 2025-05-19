using System;
using System.Windows;
using DG2072_USB_Control.Services;

namespace DG2072_USB_Control.Modulation.FSK
{
    public class FSKModulation : ModulationBase
    {
        public FSKModulation(RigolDG2072 device, int channel, Window mainWindow)
            : base(device, channel, mainWindow)
        {
        }

        public override void ApplyModulation()
        {
            if (!IsDeviceConnected())
            {
                Log("Device not connected. Cannot apply FSK modulation.");
                return;
            }

            try
            {
                // Get FSK parameters from UI (assuming control naming convention)
                bool isEnabled = IsToggleButtonChecked("FSKStateToggle", false);
                string source = GetComboBoxSelectedValue("FSKSourceComboBox", "Internal");
                double rate = GetDoubleFromTextBox("FSKRateTextBox", 100.0);
                string rateUnit = GetComboBoxSelectedValue("FSKRateUnitComboBox", "Hz");
                double hopFreq = GetDoubleFromTextBox("FSKHopFreqTextBox", 1000.0);
                string hopFreqUnit = GetComboBoxSelectedValue("FSKHopFreqUnitComboBox", "Hz");

                // Convert values based on units
                double rateMultiplier = UnitConversionUtility.GetFrequencyMultiplier(rateUnit);
                double keyingRate = rate * rateMultiplier;
                
                double freqMultiplier = UnitConversionUtility.GetFrequencyMultiplier(hopFreqUnit);
                double hoppingFrequency = hopFreq * freqMultiplier;

                // Apply FSK modulation parameters
                Device.SendCommand($"SOURCE{ActiveChannelNumber}:FSKey:SOURCE {source.ToUpper()}");
                Device.SendCommand($"SOURCE{ActiveChannelNumber}:FSKey:RATE {keyingRate}");
                Device.SendCommand($"SOURCE{ActiveChannelNumber}:FSKey:FREQUENCY {hoppingFrequency}");

                // Enable/disable FSK modulation
                Device.SendCommand($"SOURCE{ActiveChannelNumber}:FSKey:STATE {(isEnabled ? "ON" : "OFF")}");

                Log($"Applied FSK Modulation to CH{ActiveChannelNumber}: " +
                    $"State={isEnabled}, Source={source}, Rate={keyingRate}Hz, " +
                    $"Hop Frequency={hoppingFrequency}Hz");
            }
            catch (Exception ex)
            {
                Log($"Error applying FSK modulation: {ex.Message}");
            }
        }

        public override void SetModulationState(bool enabled)
        {
            if (!IsDeviceConnected()) return;

            try
            {
                Device.SendCommand($"SOURCE{ActiveChannelNumber}:FSKey:STATE {(enabled ? "ON" : "OFF")}");
                Log($"Set FSK modulation state to {(enabled ? "ON" : "OFF")} for CH{ActiveChannelNumber}");
            }
            catch (Exception ex)
            {
                Log($"Error setting FSK modulation state: {ex.Message}");
            }
        }

        public override void SetModulationSource(string source)
        {
            if (!IsDeviceConnected()) return;

            try
            {
                Device.SendCommand($"SOURCE{ActiveChannelNumber}:FSKey:SOURCE {source.ToUpper()}");
                Log($"Set FSK modulation source to {source} for CH{ActiveChannelNumber}");
            }
            catch (Exception ex)
            {
                Log($"Error setting FSK modulation source: {ex.Message}");
            }
        }

        public override void RefreshParameters()
        {
            if (!IsDeviceConnected()) return;

            try
            {
                // Query FSK modulation parameters
                string state = Device.SendQuery($"SOURCE{ActiveChannelNumber}:FSKey:STATE?").Trim();
                string source = Device.SendQuery($"SOURCE{ActiveChannelNumber}:FSKey:SOURCE?").Trim();
                string rate = Device.SendQuery($"SOURCE{ActiveChannelNumber}:FSKey:RATE?").Trim();
                string hopFreq = Device.SendQuery($"SOURCE{ActiveChannelNumber}:FSKey:FREQUENCY?").Trim();

                // Update UI
                Log($"FSK Modulation parameters for CH{ActiveChannelNumber}: " +
                    $"State={state}, Source={source}, Rate={rate}Hz, " +
                    $"Hop Frequency={hopFreq}Hz");

                // TODO: Update UI controls if needed
            }
            catch (Exception ex)
            {
                Log($"Error refreshing FSK modulation parameters: {ex.Message}");
            }
        }
    }
}