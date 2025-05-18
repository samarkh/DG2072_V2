using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using DG2072_USB_Control.Services;
using System.Collections.Generic;

namespace DG2072_USB_Control.Continuous.DualTone
{
    public class DualToneGen : WaveformGenerator, IDualToneEventHandler
    {
        // UI elements - keeping all the existing UI elements
        private readonly TextBox _secondaryFrequencyTextBox;
        private readonly ComboBox _secondaryFrequencyUnitComboBox;
        private readonly CheckBox _synchronizeFrequenciesCheckBox;
        private readonly ComboBox _frequencyRatioComboBox;
        private readonly RadioButton _directFrequencyMode;
        private readonly RadioButton _centerOffsetMode;
        private readonly StackPanel _directFrequencyPanel;
        private readonly StackPanel _centerOffsetPanel;
        private readonly TextBox _centerFrequencyTextBox;
        private readonly ComboBox _centerFrequencyUnitComboBox;
        private readonly TextBox _offsetFrequencyTextBox;
        private readonly ComboBox _offsetFrequencyUnitComboBox;
        private readonly TextBlock _calculatedF1Display;
        private readonly TextBlock _calculatedF2Display;
        private readonly DockPanel _secondaryFrequencyDockPanel;

        // Primary frequency TextBox and ComboBox (from MainWindow)
        private readonly TextBox _primaryFrequencyTextBox;
        private readonly ComboBox _primaryFrequencyUnitComboBox;

        // Update timers for debouncing
        private DispatcherTimer _secondaryFrequencyUpdateTimer;
        private DispatcherTimer _centerFrequencyUpdateTimer;
        private DispatcherTimer _offsetFrequencyUpdateTimer;

        // Add a private field to store the frequency label
        private readonly Label _frequencyLabel;

        // Settings
        private double _frequencyRatio = 2.0; // Default frequency ratio

        // Constructor
        public DualToneGen(RigolDG2072 device, int channel, Window mainWindow)
            : base(device, channel, mainWindow) // Call the base constructor
        {
            // Initialize UI references
            _secondaryFrequencyTextBox = mainWindow.FindName("SecondaryFrequencyTextBox") as TextBox;
            _secondaryFrequencyUnitComboBox = mainWindow.FindName("SecondaryFrequencyUnitComboBox") as ComboBox;
            _synchronizeFrequenciesCheckBox = mainWindow.FindName("SynchronizeFrequenciesCheckBox") as CheckBox;
            _frequencyRatioComboBox = mainWindow.FindName("FrequencyRatioComboBox") as ComboBox;
            _directFrequencyMode = mainWindow.FindName("DirectFrequencyMode") as RadioButton;
            _centerOffsetMode = mainWindow.FindName("CenterOffsetMode") as RadioButton;
            _directFrequencyPanel = mainWindow.FindName("DirectFrequencyPanel") as StackPanel;
            _centerOffsetPanel = mainWindow.FindName("CenterOffsetPanel") as StackPanel;
            _centerFrequencyTextBox = mainWindow.FindName("CenterFrequencyTextBox") as TextBox;
            _centerFrequencyUnitComboBox = mainWindow.FindName("CenterFrequencyUnitComboBox") as ComboBox;
            _offsetFrequencyTextBox = mainWindow.FindName("OffsetFrequencyTextBox") as TextBox;
            _offsetFrequencyUnitComboBox = mainWindow.FindName("OffsetFrequencyUnitComboBox") as ComboBox;
            _calculatedF1Display = mainWindow.FindName("CalculatedF1Display") as TextBlock;
            _calculatedF2Display = mainWindow.FindName("CalculatedF2Display") as TextBlock;
            _secondaryFrequencyDockPanel = mainWindow.FindName("SecondaryFrequencyDockPanel") as DockPanel;

            _primaryFrequencyTextBox = mainWindow.FindName("PrimaryFrequencyTextBox") as TextBox;
            _primaryFrequencyUnitComboBox = mainWindow.FindName("PrimaryFrequencyUnitComboBox") as ComboBox;

            // Main frequency controls (needed for synchronization)
            _primaryFrequencyTextBox = mainWindow.FindName("ChannelFrequencyTextBox") as TextBox;
            _primaryFrequencyUnitComboBox = mainWindow.FindName("ChannelFrequencyUnitComboBox") as ComboBox;

            // Find the frequency label within the FrequencyDockPanel
            DockPanel freqDockPanel = mainWindow.FindName("FrequencyDockPanel") as DockPanel;
            if (freqDockPanel != null && freqDockPanel.Children.Count > 0)
            {
                _frequencyLabel = freqDockPanel.Children[0] as Label;
            }
        }

        #region WaveformGenerator Abstract Method Implementations

        /// <summary>
        /// Implementation of abstract method from WaveformGenerator base class
        /// Apply all dual tone parameters
        /// </summary>
        public override void ApplyParameters()
        {
            if (!IsDeviceConnected()) return;

            // Simply call the existing method that handles the application of parameters
            ApplyDualToneParameters();
        }

        /// <summary>
        /// Implementation of abstract method from WaveformGenerator base class
        /// Refresh dual tone settings from device
        /// </summary>
        public override void RefreshParameters()
        {
            if (!IsDeviceConnected()) return;

            // Call the existing method that refreshes parameters from the device
            RefreshDualToneSettings();
        }

        #endregion

        #region IDualToneEventHandler Implementation

        // Keeping all the existing event handler methods
        public void OnSecondaryFrequencyTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsDeviceConnected()) return;
            if (!double.TryParse(_secondaryFrequencyTextBox.Text, out double _)) return;

            // Use base class timer method instead of local implementation
            CreateOrResetTimer(ref _secondaryFrequencyUpdateTimer, () => {
                if (double.TryParse(_secondaryFrequencyTextBox.Text, out double _))
                {
                    ApplyDualToneParameters();
                }
            });
        }

        public void OnSecondaryFrequencyLostFocus(object sender, RoutedEventArgs e)
        {
            if (!IsDeviceConnected()) return;
            if (double.TryParse(_secondaryFrequencyTextBox.Text, out double _))
            {
                AdjustFrequencyAndUnit(_secondaryFrequencyTextBox, _secondaryFrequencyUnitComboBox);
                ApplyDualToneParameters();
            }
        }

        public void OnSecondaryFrequencyUnitChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsDeviceConnected()) return;
            if (_directFrequencyMode.IsChecked != true) return;

            if (double.TryParse(_secondaryFrequencyTextBox.Text, out double _))
            {
                ApplyDualToneParameters();
            }
        }

        public void OnSynchronizeFrequenciesCheckChanged(object sender, RoutedEventArgs e)
        {
            if (!IsDeviceConnected()) return;

            bool isSynchronized = _synchronizeFrequenciesCheckBox.IsChecked == true;

            if (_secondaryFrequencyDockPanel != null)
            {
                _secondaryFrequencyDockPanel.IsEnabled = !isSynchronized;
            }

            _frequencyRatioComboBox.IsEnabled = isSynchronized;

            if (isSynchronized && double.TryParse(_primaryFrequencyTextBox.Text, out double _))
            {
                UpdateSecondaryFrequencyForDualTone();
            }
        }

        public void OnFrequencyRatioSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsDeviceConnected()) return;

            ComboBox ratioComboBox = sender as ComboBox;
            if (ratioComboBox != null && ratioComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string ratioText = selectedItem.Content.ToString();
                if (double.TryParse(ratioText, out double ratio))
                {
                    _frequencyRatio = ratio;

                    // If synchronized is checked, update the secondary frequency
                    if (_synchronizeFrequenciesCheckBox.IsChecked == true &&
                        double.TryParse(_primaryFrequencyTextBox.Text, out double _))
                    {
                        UpdateSecondaryFrequencyForDualTone();
                    }
                }
            }
        }

        public void OnDualToneModeChanged(object sender, RoutedEventArgs e)
        {
            if (!IsDeviceConnected()) return;

            bool isDirectMode = _directFrequencyMode.IsChecked == true;

            // Toggle visibility of panels
            _directFrequencyPanel.Visibility = isDirectMode ? Visibility.Visible : Visibility.Collapsed;
            _centerOffsetPanel.Visibility = isDirectMode ? Visibility.Collapsed : Visibility.Visible;

            // Get the row for CenterFrequency in CenterOffsetPanel
            if (_centerOffsetPanel != null && _centerOffsetPanel.Children.Count > 0)
            {
                // Find the DockPanel for Center Frequency (first child)
                DockPanel centerFreqPanel = _centerOffsetPanel.Children[0] as DockPanel;
                if (centerFreqPanel != null)
                {
                    // Always ensure the Center Freq control is visible
                    centerFreqPanel.Visibility = Visibility.Visible;
                }
            }

            // If switching modes, update the displayed values
            if (isDirectMode)
            {
                // Set values from primary frequency controls and ratio
                UpdateSecondaryFrequencyForDualTone();
            }
            else
            {
                // Calculate center and offset from current F1 and F2
                UpdateCenterOffsetFromFrequencies();
            }
        }

        public void OnCenterFrequencyTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsDeviceConnected()) return;
            if (!double.TryParse(_centerFrequencyTextBox.Text, out double _)) return;
            if (_centerOffsetMode.IsChecked != true) return;

            // Use base class method for timer management
            CreateOrResetTimer(ref _centerFrequencyUpdateTimer, () => {
                UpdateFrequenciesFromCenterOffset();
            });
        }

        public void OnCenterFrequencyLostFocus(object sender, RoutedEventArgs e)
        {
            if (!IsDeviceConnected()) return;
            if (double.TryParse(_centerFrequencyTextBox.Text, out double _))
            {
                AdjustFrequencyAndUnit(_centerFrequencyTextBox, _centerFrequencyUnitComboBox);
                UpdateFrequenciesFromCenterOffset();
            }
        }

        public void OnCenterFrequencyUnitChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsDeviceConnected()) return;
            if (_centerOffsetMode.IsChecked != true) return;

            if (double.TryParse(_centerFrequencyTextBox.Text, out double _))
            {
                UpdateFrequenciesFromCenterOffset();
            }
        }

        public void OnOffsetFrequencyTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsDeviceConnected()) return;
            if (!double.TryParse(_offsetFrequencyTextBox.Text, out double _)) return;
            if (_centerOffsetMode.IsChecked != true) return;

            // Use base class method for timer management
            CreateOrResetTimer(ref _offsetFrequencyUpdateTimer, () => {
                UpdateFrequenciesFromCenterOffset();
            });
        }

        public void OnOffsetFrequencyLostFocus(object sender, RoutedEventArgs e)
        {
            if (!IsDeviceConnected()) return;
            if (double.TryParse(_offsetFrequencyTextBox.Text, out double _))
            {
                AdjustFrequencyAndUnit(_offsetFrequencyTextBox, _offsetFrequencyUnitComboBox);
                UpdateFrequenciesFromCenterOffset();
            }
        }

        public void OnOffsetFrequencyUnitChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsDeviceConnected()) return;
            if (_centerOffsetMode.IsChecked != true) return;

            if (double.TryParse(_offsetFrequencyTextBox.Text, out double _))
            {
                UpdateFrequenciesFromCenterOffset();
            }
        }

        #endregion

        #region Core Functionality
        // Update the secondary frequency based on the primary frequency and ratio
        public void UpdateSecondaryFrequencyForDualTone()
        {
            if (!IsDeviceConnected()) return;

            try
            {
                if (double.TryParse(_primaryFrequencyTextBox.Text, out double primaryFreq))
                {
                    // Calculate secondary frequency
                    double secondaryFreq = primaryFreq * _frequencyRatio;

                    // Update secondary frequency display using UnitConversionUtility for formatting
                    _secondaryFrequencyTextBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(secondaryFreq);

                    // If in dual tone mode, apply the changes
                    ApplyDualToneParameters();
                }
            }
            catch (Exception ex)
            {
                Log($"Error updating secondary frequency: {ex.Message}");
            }
        }

        // Apply dual tone parameters to the device
        public void ApplyDualToneParameters()
        {
            if (!IsDeviceConnected()) return;

            try
            {
                // Handle based on mode
                if (_centerOffsetMode.IsChecked == true)
                {
                    // Using center/offset mode, just update calculations
                    UpdateFrequenciesFromCenterOffset();
                }
                else
                {
                    // Direct mode - get primary and secondary frequencies

                    // Get primary frequency
                    if (!double.TryParse(_primaryFrequencyTextBox.Text, out double frequency))
                        return;

                    string freqUnit = UnitConversionUtility.GetFrequencyUnit(_primaryFrequencyUnitComboBox);
                    double freqMultiplier = UnitConversionUtility.GetFrequencyMultiplier(freqUnit);
                    double actualPrimaryFrequency = frequency * freqMultiplier;

                    // Get secondary frequency
                    double actualSecondaryFrequency = actualPrimaryFrequency * _frequencyRatio; // Default
                    if (_secondaryFrequencyTextBox != null && double.TryParse(_secondaryFrequencyTextBox.Text, out double secondaryFreq))
                    {
                        string secondaryFreqUnit = UnitConversionUtility.GetFrequencyUnit(_secondaryFrequencyUnitComboBox);
                        double secondaryFreqMultiplier = UnitConversionUtility.GetFrequencyMultiplier(secondaryFreqUnit);
                        actualSecondaryFrequency = secondaryFreq * secondaryFreqMultiplier;
                    }

                    // Get amplitude, offset, phase using base class methods
                    double amplitude = GetAmplitudeFromUI();
                    double offset = GetOffsetFromUI();
                    double phase = GetPhaseFromUI();

                    // Create parameters dictionary
                    Dictionary<string, object> parameters = new Dictionary<string, object>
                    {
                        { "Frequency", actualPrimaryFrequency },
                        { "Frequency2", actualSecondaryFrequency },
                        { "Amplitude", amplitude },
                        { "Offset", offset },
                        { "Phase", phase }
                    };

                    // Apply the dual tone waveform
                    Device.ApplyDualToneWaveform(ActiveChannel, parameters);

                    Log($"Applied Dual Tone waveform to CH{ActiveChannel} with Primary Freq={frequency} {freqUnit}, " +
                        $"Secondary Freq={_secondaryFrequencyTextBox.Text} {UnitConversionUtility.GetFrequencyUnit(_secondaryFrequencyUnitComboBox)}, " +
                        $"Amp={amplitude}Vpp, Offset={offset}V, Phase={phase}°");
                }
            }
            catch (Exception ex)
            {
                Log($"Error applying dual tone settings: {ex.Message}");
            }
        }

        // Calculate center and offset from F1 and F2
        private void UpdateCenterOffsetFromFrequencies()
        {
            try
            {
                // Get current F1 (primary) and F2 (secondary) in Hz
                double f1Hz = 0, f2Hz = 0;

                if (double.TryParse(_primaryFrequencyTextBox.Text, out double f1))
                {
                    string f1Unit = UnitConversionUtility.GetFrequencyUnit(_primaryFrequencyUnitComboBox);
                    f1Hz = f1 * UnitConversionUtility.GetFrequencyMultiplier(f1Unit);
                }

                if (double.TryParse(_secondaryFrequencyTextBox.Text, out double f2))
                {
                    string f2Unit = UnitConversionUtility.GetFrequencyUnit(_secondaryFrequencyUnitComboBox);
                    f2Hz = f2 * UnitConversionUtility.GetFrequencyMultiplier(f2Unit);
                }

                // Use helper methods to calculate center and offset
                double centerFreqHz = CalculateCenterFrequency(f1Hz, f2Hz);
                double offsetFreqHz = CalculateOffsetFrequency(f1Hz, f2Hz);

                // Update UI with calculated values using UnitConversionUtility for proper unit conversion
                string centerUnit = UnitConversionUtility.GetFrequencyUnit(_centerFrequencyUnitComboBox);
                double displayCenterFreq = UnitConversionUtility.ConvertFromMicroHz(centerFreqHz * 1e6, centerUnit);
                _centerFrequencyTextBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(displayCenterFreq);

                string offsetUnit = UnitConversionUtility.GetFrequencyUnit(_offsetFrequencyUnitComboBox);
                double displayOffsetFreq = UnitConversionUtility.ConvertFromMicroHz(offsetFreqHz * 1e6, offsetUnit);
                _offsetFrequencyTextBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(displayOffsetFreq, 1);
            }
            catch (Exception ex)
            {
                Log($"Error updating center/offset values: {ex.Message}");
            }
        }

        // Calculate F1 and F2 from center and offset
        public void UpdateFrequenciesFromCenterOffset()
        {
            try
            {
                // Get current center and offset frequencies in Hz
                double centerFreqHz = 0, offsetFreqHz = 0;

                // Always use the Center Frequency TextBox in Center/Offset mode
                // Do NOT use the primary frequency control
                if (double.TryParse(_centerFrequencyTextBox.Text, out double center))
                {
                    string centerUnit = UnitConversionUtility.GetFrequencyUnit(_centerFrequencyUnitComboBox);
                    centerFreqHz = center * UnitConversionUtility.GetFrequencyMultiplier(centerUnit);
                }

                if (double.TryParse(_offsetFrequencyTextBox.Text, out double offset))
                {
                    string offsetUnit = UnitConversionUtility.GetFrequencyUnit(_offsetFrequencyUnitComboBox);
                    offsetFreqHz = offset * UnitConversionUtility.GetFrequencyMultiplier(offsetUnit);
                }

                // Use helper methods to calculate F1 and F2
                double f1Hz = CalculateF1FromCenterOffset(centerFreqHz, offsetFreqHz);
                double f2Hz = CalculateF2FromCenterOffset(centerFreqHz, offsetFreqHz);

                // Update the calculated values display
                _calculatedF1Display.Text = $"{UnitConversionUtility.FormatWithMinimumDecimals(f1Hz)} Hz";
                _calculatedF2Display.Text = $"{UnitConversionUtility.FormatWithMinimumDecimals(f2Hz)} Hz";

                // Apply to device with f1Hz and f2Hz
                ApplyDualToneWithFrequencies(f1Hz, f2Hz);
            }
            catch (Exception ex)
            {
                Log($"Error updating frequencies from center/offset: {ex.Message}");
            }
        }

        // Apply dual tone with specific frequencies
        private void ApplyDualToneWithFrequencies(double f1Hz, double f2Hz)
        {
            try
            {
                // Get amplitude, offset, phase using base class methods
                double amplitude = GetAmplitudeFromUI();
                double voltageOffset = GetOffsetFromUI();
                double phase = GetPhaseFromUI();

                // Create parameters dictionary for the device
                Dictionary<string, object> parameters = new Dictionary<string, object>
                {
                    { "Frequency", f1Hz },
                    { "Frequency2", f2Hz },
                    { "Amplitude", amplitude },
                    { "Offset", voltageOffset },
                    { "Phase", phase }
                };

                // Apply the dual tone waveform
                Device.ApplyDualToneWaveform(ActiveChannel, parameters);

                // Calculate center and frequency offset using the same formula as the UI
                double centerFreq = (f1Hz + f2Hz) / 2.0;
                double freqOffset = (f2Hz - f1Hz) / 2.0;  // Half the distance between frequencies

                // Fixed log message with correctly calculated offset
                Log($"Applied Dual Tone waveform to CH{ActiveChannel} with F1={f1Hz}Hz, F2={f2Hz}Hz, " +
                    $"Center={centerFreq}Hz, Freq Offset={freqOffset}Hz, " +
                    $"Amp={amplitude}Vpp, Voltage Offset={voltageOffset}V, Phase={phase}°");
            }
            catch (Exception ex)
            {
                Log($"Error applying dual tone with frequencies: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Calculates the center frequency from two individual frequencies
        /// </summary>
        private double CalculateCenterFrequency(double f1, double f2)
        {
            return (f1 + f2) / 2.0;
        }

        /// <summary>
        /// Calculates the offset frequency from two individual frequencies
        /// </summary>
        private double CalculateOffsetFrequency(double f1, double f2)
        {
            return (f2 - f1) / 2.0;  // Half the distance between tones (distance from center to each tone)
        }

        /// <summary>
        /// Calculates f1 from center and offset frequencies
        /// </summary>
        private double CalculateF1FromCenterOffset(double center, double offset)
        {
            return center - offset;  // Offset is distance from center to each tone
        }

        /// <summary>
        /// Calculates f2 from center and offset frequencies
        /// </summary>
        private double CalculateF2FromCenterOffset(double center, double offset)
        {
            return center + offset;  // Offset is distance from center to each tone
        }

        // Helper for adjusting frequency and unit display
        private void AdjustFrequencyAndUnit(TextBox textBox, ComboBox unitComboBox)
        {
            if (!double.TryParse(textBox.Text, out double value))
                return;

            string currentUnit = ((ComboBoxItem)unitComboBox.SelectedItem)?.Content.ToString();
            if (string.IsNullOrEmpty(currentUnit)) return;

            // Convert current value to µHz to maintain precision
            double microHzValue = UnitConversionUtility.ConvertToMicroHz(value, currentUnit);

            // Define units in order from smallest to largest
            string[] frequencyUnits = { "µHz", "mHz", "Hz", "kHz", "MHz" };

            // Map the combo box selection to our array index
            int unitIndex = 0;
            for (int i = 0; i < frequencyUnits.Length; i++)
            {
                if (frequencyUnits[i] == currentUnit)
                {
                    unitIndex = i;
                    break;
                }
            }

            // Get the current value in the selected unit
            double displayValue = UnitConversionUtility.ConvertFromMicroHz(microHzValue, frequencyUnits[unitIndex]);

            // Auto-range: handle values that are too large (> 9999)
            while (displayValue > 9999 && unitIndex < frequencyUnits.Length - 1)
            {
                unitIndex++;
                displayValue = UnitConversionUtility.ConvertFromMicroHz(microHzValue, frequencyUnits[unitIndex]);
            }

            // Auto-range: handle values that are too small (< 0.1)
            while (displayValue < 0.1 && unitIndex > 0)
            {
                unitIndex--;
                displayValue = UnitConversionUtility.ConvertFromMicroHz(microHzValue, frequencyUnits[unitIndex]);
            }

            // Update the textbox with formatted value using UnitConversionUtility
            textBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(displayValue);

            // Find and select the unit in the combo box
            for (int i = 0; i < unitComboBox.Items.Count; i++)
            {
                ComboBoxItem item = unitComboBox.Items[i] as ComboBoxItem;
                if (item != null && item.Content.ToString() == frequencyUnits[unitIndex])
                {
                    unitComboBox.SelectedIndex = i;
                    break;
                }
            }
        }

        // Helper method to select matching unit in a ComboBox
        private void SelectMatchingUnit(ComboBox unitComboBox, string unitToMatch)
        {
            if (unitComboBox != null)
            {
                foreach (var item in unitComboBox.Items)
                {
                    ComboBoxItem comboItem = item as ComboBoxItem;
                    if (comboItem != null && comboItem.Content.ToString() == unitToMatch)
                    {
                        unitComboBox.SelectedItem = comboItem;
                        break;
                    }
                }
            }
        }

        // Update frequency ratio combobox based on the actual ratio
        private void UpdateFrequencyRatioComboBox(double ratio)
        {
            // Find the closest matching ratio in the combo box
            if (_frequencyRatioComboBox != null)
            {
                double closestDiff = double.MaxValue;
                int closestIndex = 0;

                // Loop through all items and find the closest match
                for (int i = 0; i < _frequencyRatioComboBox.Items.Count; i++)
                {
                    var item = _frequencyRatioComboBox.Items[i] as ComboBoxItem;
                    if (item != null && double.TryParse(item.Content.ToString(), out double itemRatio))
                    {
                        double diff = Math.Abs(itemRatio - ratio);
                        if (diff < closestDiff)
                        {
                            closestDiff = diff;
                            closestIndex = i;
                        }
                    }
                }

                // Set the selected item to the closest match
                _frequencyRatioComboBox.SelectedIndex = closestIndex;
            }
        }

        #endregion

        #region Public Methods

        // Refresh dual tone settings from device
        public void RefreshDualToneSettings()
        {
            try
            {
                // Get all dual tone parameters
                var parameters = Device.GetAllDualToneParameters(ActiveChannel);

                // Update primary frequency (from main controls)
                if (parameters.TryGetValue("Frequency1", out double freq1))
                {
                    // Update using UnitConversionUtility
                    double displayValue = UnitConversionUtility.ConvertFromMicroHz(
                        freq1 * 1e6,
                        UnitConversionUtility.GetFrequencyUnit(_primaryFrequencyUnitComboBox)
                    );
                    _primaryFrequencyTextBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(displayValue);
                }

                // Update secondary frequency
                if (parameters.TryGetValue("Frequency2", out double freq2) &&
                    _secondaryFrequencyTextBox != null)
                {
                    string unit = UnitConversionUtility.GetFrequencyUnit(_secondaryFrequencyUnitComboBox);
                    double displayValue = UnitConversionUtility.ConvertFromMicroHz(freq2 * 1e6, unit);
                    _secondaryFrequencyTextBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(displayValue);

                    // Update frequency ratio if needed
                    if (freq1 > 0)
                    {
                        _frequencyRatio = freq2 / freq1;
                        UpdateFrequencyRatioComboBox(_frequencyRatio);
                    }
                }

                // Update center and offset for center/offset mode
                if (parameters.TryGetValue("CenterFrequency", out double center) &&
                    _centerFrequencyTextBox != null)
                {
                    string unit = UnitConversionUtility.GetFrequencyUnit(_centerFrequencyUnitComboBox);
                    double displayValue = UnitConversionUtility.ConvertFromMicroHz(center * 1e6, unit);
                    _centerFrequencyTextBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(displayValue);
                }

                if (parameters.TryGetValue("OffsetFrequency", out double offset) &&
                    _offsetFrequencyTextBox != null)
                {
                    string unit = UnitConversionUtility.GetFrequencyUnit(_offsetFrequencyUnitComboBox);
                    double displayValue = UnitConversionUtility.ConvertFromMicroHz(offset * 1e6, unit);
                    _offsetFrequencyTextBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(displayValue);
                }

                // Update calculated F1/F2 displays if in center/offset mode
                if (_centerOffsetMode.IsChecked == true)
                {
                    _calculatedF1Display.Text = $"{UnitConversionUtility.FormatWithMinimumDecimals(freq1)} Hz";
                    _calculatedF2Display.Text = $"{UnitConversionUtility.FormatWithMinimumDecimals(freq2)} Hz";
                }
            }
            catch (Exception ex)
            {
                Log($"Error refreshing dual tone settings: {ex.Message}");
            }
        }

        #endregion
    }
}