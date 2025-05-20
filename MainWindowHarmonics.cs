//MainWindowHarmonics.cs
using System;
using System.Windows;
using System.Windows.Controls;

namespace DG2072_USB_Control
{
    // Harmonics functionality for MainWindow
    public partial class MainWindow
    {
        #region Harmonics Controls

        /// <summary>
        /// Handles and manages harmonics controls
        /// This is the main controller for all harmonics-related functionality
        /// </summary>
        public void HandleHarmonicsControls()
        {
            LogMessage("Handling harmonics controls");
            // This method would manage the harmonics control UI and integrate with device commands
            // Currently a placeholder for future implementation
        }

        /// <summary>
        /// Toggles harmonics function on/off
        /// </summary>
        private void HarmonicsToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggleButton)
            {
                bool isEnabled = toggleButton.IsChecked == true;
                toggleButton.Content = isEnabled ? "Enabled" : "Disabled";
                LogMessage($"Harmonics function {(isEnabled ? "enabled" : "disabled")}");
                
                // Placeholder for sending command to device
            }
        }

        /// <summary>
        /// Handles amplitude mode change (Percentage or Absolute)
        /// </summary>
        private void AmplitudeModeChanged(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.IsChecked == true)
            {
                string mode = rb.Content.ToString();
                LogMessage($"Harmonic amplitude mode changed to {mode}");
                
                // Update UI based on mode
                if (mode == "Percentage")
                {
                    AmplitudeHeader.Text = "Amplitude (%)";
                    // Hide unit comboboxes
                }
                else // Absolute mode
                {
                    AmplitudeHeader.Text = "Amplitude";
                    // Show unit comboboxes
                }
            }
        }

        /// <summary>
        /// Handles harmonic checkbox state changes
        /// </summary>
        private void HarmonicCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox)
            {
                bool isChecked = checkBox.IsChecked == true;
                int harmonicNumber = int.Parse(checkBox.Tag.ToString());
                LogMessage($"Harmonic {harmonicNumber} {(isChecked ? "enabled" : "disabled")}");
                
                // Placeholder for sending command to device
            }
        }

        /// <summary>
        /// Handles harmonic amplitude validation and update
        /// </summary>
        private void HarmonicAmplitudeTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                int harmonicNumber = int.Parse(textBox.Tag.ToString());
                
                if (double.TryParse(textBox.Text, out double amplitude))
                {
                    LogMessage($"Harmonic {harmonicNumber} amplitude set to {amplitude}");
                    // Placeholder for validation and sending command to device
                }
                else
                {
                    // Reset to default if invalid
                    textBox.Text = "0.0";
                }
            }
        }

        /// <summary>
        /// Handles harmonic amplitude unit changes
        /// </summary>
        private void HarmonicAmplitudeUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem != null)
            {
                int harmonicNumber = int.Parse(comboBox.Tag.ToString());
                string unit = (comboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
                LogMessage($"Harmonic {harmonicNumber} amplitude unit changed to {unit}");
                
                // Placeholder for sending command to device
            }
        }

        /// <summary>
        /// Handles harmonic phase validation and update
        /// </summary>
        private void HarmonicPhaseTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                int harmonicNumber = int.Parse(textBox.Tag.ToString());
                
                if (double.TryParse(textBox.Text, out double phase))
                {
                    LogMessage($"Harmonic {harmonicNumber} phase set to {phase}°");
                    // Placeholder for validation and sending command to device
                }
                else
                {
                    // Reset to default if invalid
                    textBox.Text = "0.0";
                }
            }
        }

        #endregion
    }
}