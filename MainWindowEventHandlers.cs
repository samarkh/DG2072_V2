using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace DG2072_USB_Control
{
    // Additional event handlers for MainWindow
    public partial class MainWindow
    {
        #region PM Modulation Handlers

        private void PMStateToggle_Click(object sender, RoutedEventArgs e)
        {
            // Toggle PM state and update UI
            ToggleButton toggleButton = sender as ToggleButton;
            bool isOn = toggleButton.IsChecked ?? false;
            toggleButton.Content = isOn ? "ON" : "OFF";

            if (IsConnected)
            {
                // Send PM state command to device
                string command = $":SOURce{ActiveChannel}:PM:STATe {(isOn ? "ON" : "OFF")}";
                SendCommand(command);
            }

            LogMessage($"PM State set to {(isOn ? "ON" : "OFF")} on Channel {ActiveChannel}");
        }

        private void PMSourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PMSourceComboBox.SelectedItem == null) return;

            string source = (PMSourceComboBox.SelectedItem as ComboBoxItem).Content.ToString();
            bool isInternal = source == "Internal";

            // Show/hide internal settings based on source
            PMInternalSettingsGroupBox.Visibility = isInternal ? Visibility.Visible : Visibility.Collapsed;

            if (IsConnected)
            {
                // Send PM source command to device
                string command = $":SOURce{ActiveChannel}:PM:SOURce {(isInternal ? "INTernal" : "EXTernal")}";
                SendCommand(command);
            }

            LogMessage($"PM Source set to {source} on Channel {ActiveChannel}");
        }

        private void PMDeviationTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Real-time validation could be implemented here
        }

        private void PMDeviationTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(PMDeviationTextBox.Text, out double deviation))
            {
                PMDeviationTextBox.Text = "90.0"; // Reset to default
                return;
            }

            // Ensure value is within valid range (0-360 degrees for phase)
            if (deviation < 0) deviation = 0;
            if (deviation > 360) deviation = 360;
            PMDeviationTextBox.Text = deviation.ToString("F1");

            if (IsConnected)
            {
                // Send PM deviation command to device
                string command = $":SOURce{ActiveChannel}:PM:DEViation {deviation}";
                SendCommand(command);
            }

            LogMessage($"PM Deviation set to {deviation}° on Channel {ActiveChannel}");
        }

        private void PMWaveformComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PMWaveformComboBox.SelectedItem == null) return;

            string waveform = (PMWaveformComboBox.SelectedItem as ComboBoxItem).Content.ToString();

            if (IsConnected)
            {
                // Send PM waveform command to device
                string command = $":SOURce{ActiveChannel}:PM:INTernal:FUNCtion {waveform}";
                SendCommand(command);
            }

            LogMessage($"PM Modulation Waveform set to {waveform} on Channel {ActiveChannel}");
        }

        private void PMFrequencyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Real-time validation could be implemented here
        }

        private void PMFrequencyTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(PMFrequencyTextBox.Text, out double frequency))
            {
                PMFrequencyTextBox.Text = "10"; // Reset to default
                return;
            }

            // Ensure value is within valid range
            if (frequency <= 0) frequency = 0.001;
            PMFrequencyTextBox.Text = frequency.ToString("F3");

            if (IsConnected)
            {
                // Get selected unit and multiplier
                string unit = (PMFrequencyUnitComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Hz";
                double multiplier = GetUnitMultiplier(unit);
                double actualFrequency = frequency * multiplier;

                // Send PM frequency command to device
                string command = $":SOURce{ActiveChannel}:PM:INTernal:FREQuency {actualFrequency}";
                SendCommand(command);
            }

            LogMessage($"PM Modulation Frequency set to {frequency} {(PMFrequencyUnitComboBox.SelectedItem as ComboBoxItem)?.Content} on Channel {ActiveChannel}");
        }

        private void PMFrequencyUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PMFrequencyUnitComboBox.SelectedItem == null || PMFrequencyTextBox == null) return;

            if (double.TryParse(PMFrequencyTextBox.Text, out double frequency) && IsConnected)
            {
                string unit = (PMFrequencyUnitComboBox.SelectedItem as ComboBoxItem).Content.ToString();
                double multiplier = GetUnitMultiplier(unit);
                double actualFrequency = frequency * multiplier;

                // Send PM frequency command to device
                string command = $":SOURce{ActiveChannel}:PM:INTernal:FREQuency {actualFrequency}";
                SendCommand(command);

                LogMessage($"PM Modulation Frequency updated to {frequency} {unit} on Channel {ActiveChannel}");
            }
        }

        private void PMApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsConnected) return;

            // Apply all PM settings at once
            bool isOn = PMStateToggle.IsChecked ?? false;
            string source = (PMSourceComboBox.SelectedItem as ComboBoxItem).Content.ToString();
            bool isInternal = source == "Internal";

            if (double.TryParse(PMDeviationTextBox.Text, out double deviation))
            {
                // Send all PM commands in sequence
                SendCommand($":SOURce{ActiveChannel}:PM:STATe {(isOn ? "ON" : "OFF")}");
                SendCommand($":SOURce{ActiveChannel}:PM:SOURce {(isInternal ? "INTernal" : "EXTernal")}");
                SendCommand($":SOURce{ActiveChannel}:PM:DEViation {deviation}");

                if (isInternal)
                {
                    string waveform = (PMWaveformComboBox.SelectedItem as ComboBoxItem).Content.ToString();
                    SendCommand($":SOURce{ActiveChannel}:PM:INTernal:FUNCtion {waveform}");

                    if (double.TryParse(PMFrequencyTextBox.Text, out double frequency))
                    {
                        string unit = (PMFrequencyUnitComboBox.SelectedItem as ComboBoxItem).Content.ToString();
                        double multiplier = GetUnitMultiplier(unit);
                        double actualFrequency = frequency * multiplier;
                        SendCommand($":SOURce{ActiveChannel}:PM:INTernal:FREQuency {actualFrequency}");
                    }
                }

                LogMessage($"Applied all PM Modulation settings to Channel {ActiveChannel}");
            }
        }

        #endregion

        #region ASK Modulation Handlers

        private void ASKStateToggle_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton toggleButton = sender as ToggleButton;
            bool isOn = toggleButton.IsChecked ?? false;
            toggleButton.Content = isOn ? "ON" : "OFF";

            if (IsConnected)
            {
                string command = $":SOURce{ActiveChannel}:ASKey:STATe {(isOn ? "ON" : "OFF")}";
                SendCommand(command);
            }

            LogMessage($"ASK State set to {(isOn ? "ON" : "OFF")} on Channel {ActiveChannel}");
        }

        private void ASKSourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ASKSourceComboBox.SelectedItem == null) return;

            string source = (ASKSourceComboBox.SelectedItem as ComboBoxItem).Content.ToString();

            if (IsConnected)
            {
                string command = $":SOURce{ActiveChannel}:ASKey:SOURce {(source == "Internal" ? "INTernal" : "EXTernal")}";
                SendCommand(command);
            }

            LogMessage($"ASK Source set to {source} on Channel {ActiveChannel}");
        }

        private void ASKApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsConnected) return;

            // Apply all ASK settings
            bool isOn = ASKStateToggle.IsChecked ?? false;
            string source = (ASKSourceComboBox.SelectedItem as ComboBoxItem).Content.ToString();

            // Send commands to device
            SendCommand($":SOURce{ActiveChannel}:ASKey:STATe {(isOn ? "ON" : "OFF")}");
            SendCommand($":SOURce{ActiveChannel}:ASKey:SOURce {(source == "Internal" ? "INTernal" : "EXTernal")}");

            LogMessage($"Applied all ASK Modulation settings to Channel {ActiveChannel}");
        }

        #endregion

        #region FSK Modulation Handlers

        private void FSKStateToggle_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton toggleButton = sender as ToggleButton;
            bool isOn = toggleButton.IsChecked ?? false;
            toggleButton.Content = isOn ? "ON" : "OFF";

            if (IsConnected)
            {
                string command = $":SOURce{ActiveChannel}:FSKey:STATe {(isOn ? "ON" : "OFF")}";
                SendCommand(command);
            }

            LogMessage($"FSK State set to {(isOn ? "ON" : "OFF")} on Channel {ActiveChannel}");
        }

        private void FSKSourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FSKSourceComboBox.SelectedItem == null) return;

            string source = (FSKSourceComboBox.SelectedItem as ComboBoxItem).Content.ToString();

            if (IsConnected)
            {
                string command = $":SOURce{ActiveChannel}:FSKey:SOURce {(source == "Internal" ? "INTernal" : "EXTernal")}";
                SendCommand(command);
            }

            LogMessage($"FSK Source set to {source} on Channel {ActiveChannel}");
        }

        private void FSKApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsConnected) return;

            // Apply all FSK settings
            bool isOn = FSKStateToggle.IsChecked ?? false;
            string source = (FSKSourceComboBox.SelectedItem as ComboBoxItem).Content.ToString();

            // Send commands to device
            SendCommand($":SOURce{ActiveChannel}:FSKey:STATe {(isOn ? "ON" : "OFF")}");
            SendCommand($":SOURce{ActiveChannel}:FSKey:SOURce {(source == "Internal" ? "INTernal" : "EXTernal")}");

            LogMessage($"Applied all FSK Modulation settings to Channel {ActiveChannel}");
        }

        #endregion

        #region PSK Modulation Handlers

        private void PSKStateToggle_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton toggleButton = sender as ToggleButton;
            bool isOn = toggleButton.IsChecked ?? false;
            toggleButton.Content = isOn ? "ON" : "OFF";

            if (IsConnected)
            {
                string command = $":SOURce{ActiveChannel}:PSKey:STATe {(isOn ? "ON" : "OFF")}";
                SendCommand(command);
            }

            LogMessage($"PSK State set to {(isOn ? "ON" : "OFF")} on Channel {ActiveChannel}");
        }

        private void PSKSourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PSKSourceComboBox.SelectedItem == null) return;

            string source = (PSKSourceComboBox.SelectedItem as ComboBoxItem).Content.ToString();

            if (IsConnected)
            {
                string command = $":SOURce{ActiveChannel}:PSKey:SOURce {(source == "Internal" ? "INTernal" : "EXTernal")}";
                SendCommand(command);
            }

            LogMessage($"PSK Source set to {source} on Channel {ActiveChannel}");
        }

        private void PSKApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsConnected) return;

            // Apply all PSK settings
            bool isOn = PSKStateToggle.IsChecked ?? false;
            string source = (PSKSourceComboBox.SelectedItem as ComboBoxItem).Content.ToString();

            // Send commands to device
            SendCommand($":SOURce{ActiveChannel}:PSKey:STATe {(isOn ? "ON" : "OFF")}");
            SendCommand($":SOURce{ActiveChannel}:PSKey:SOURce {(source == "Internal" ? "INTernal" : "EXTernal")}");

            LogMessage($"Applied all PSK Modulation settings to Channel {ActiveChannel}");
        }

        #endregion

        #region PWM Modulation Handlers

        private void PWMStateToggle_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton toggleButton = sender as ToggleButton;
            bool isOn = toggleButton.IsChecked ?? false;
            toggleButton.Content = isOn ? "ON" : "OFF";

            if (IsConnected)
            {
                string command = $":SOURce{ActiveChannel}:PWM:STATe {(isOn ? "ON" : "OFF")}";
                SendCommand(command);
            }

            LogMessage($"PWM State set to {(isOn ? "ON" : "OFF")} on Channel {ActiveChannel}");
        }

        private void PWMSourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PWMSourceComboBox.SelectedItem == null) return;

            string source = (PWMSourceComboBox.SelectedItem as ComboBoxItem).Content.ToString();

            if (IsConnected)
            {
                string command = $":SOURce{ActiveChannel}:PWM:SOURce {(source == "Internal" ? "INTernal" : "EXTernal")}";
                SendCommand(command);
            }

            LogMessage($"PWM Source set to {source} on Channel {ActiveChannel}");
        }

        private void PWMApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsConnected) return;

            // Apply all PWM settings
            bool isOn = PWMStateToggle.IsChecked ?? false;
            string source = (PWMSourceComboBox.SelectedItem as ComboBoxItem).Content.ToString();

            // Send commands to device
            SendCommand($":SOURce{ActiveChannel}:PWM:STATe {(isOn ? "ON" : "OFF")}");
            SendCommand($":SOURce{ActiveChannel}:PWM:SOURce {(source == "Internal" ? "INTernal" : "EXTernal")}");

            LogMessage($"Applied all PWM Modulation settings to Channel {ActiveChannel}");
        }

        #endregion

        #region Dual Tone Handlers

        private void PrimaryFrequencyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Real-time validation could be implemented here
        }

        private void PrimaryFrequencyUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PrimaryFrequencyUnitComboBox.SelectedItem == null || PrimaryFrequencyTextBox == null) return;

            if (double.TryParse(PrimaryFrequencyTextBox.Text, out double frequency) && IsConnected)
            {
                string unit = (PrimaryFrequencyUnitComboBox.SelectedItem as ComboBoxItem).Content.ToString();

                // Update secondary frequency if auto-sync is enabled
                if (SynchronizeFrequenciesCheckBox.IsChecked == true)
                {
                    UpdateSecondaryFrequency();
                }

                LogMessage($"Primary Frequency unit changed to {unit} on Channel {ActiveChannel}");
            }
        }

        // Helper method for updating the secondary frequency when using auto-sync
        private void UpdateSecondaryFrequency()
        {
            if (!double.TryParse(PrimaryFrequencyTextBox.Text, out double primaryFreq)) return;
            if (!double.TryParse((FrequencyRatioComboBox.SelectedItem as ComboBoxItem)?.Content.ToString(),
                                out double ratio)) return;

            double secondaryFreq = primaryFreq * ratio;
            SecondaryFrequencyTextBox.Text = secondaryFreq.ToString("F2");
        }

        #endregion

        // Helper method for getting unit multipliers
        private double GetUnitMultiplier(string unit)
        {
            switch (unit)
            {
                case "MHz": return 1e6;
                case "kHz": return 1e3;
                case "Hz": return 1.0;
                case "mHz": return 1e-3;
                case "µHz": return 1e-6;
                default: return 1.0;
            }
        }
    }
}
