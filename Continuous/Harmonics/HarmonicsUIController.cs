using DG2072_USB_Control.Continuous.Harmonics;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using DG2072_USB_Control.Services;


namespace DG2072_USB_Control.Continuous.Harmonics
{
    /// <summary>
    /// Controls the UI interaction for harmonics functionality
    /// </summary>
    public class HarmonicsUIController
    {
        private readonly HarmonicsManager _harmonicsManager;
        private readonly Window _mainWindow;


        // UI Controls
        private ToggleButton _harmonicsToggle;
        private RadioButton _amplitudePercentageMode;
        private RadioButton _amplitudeAbsoluteMode;
        private TextBlock _amplitudeHeader;

        // Collections of harmonic controls
        private readonly List<CheckBox> _harmonicCheckBoxes = new List<CheckBox>();
        private readonly List<TextBox> _harmonicAmplitudeTextBoxes = new List<TextBox>();
        private readonly List<TextBox> _harmonicPhaseTextBoxes = new List<TextBox>();
        private readonly List<ComboBox> _harmonicAmplitudeUnitComboBoxes = new List<ComboBox>();

        // Flag for amplitude mode
        private bool _isPercentageMode = true;

        // Event for logging
        public event EventHandler<string> LogEvent;

        // Add these fields near the top of the HarmonicsUIController class
        private double _fundamentalAmplitude = 1.0;

        private Dictionary<int, double> _cachedAmplitudes = new Dictionary<int, double>();
        private Dictionary<int, double> _cachedPhases = new Dictionary<int, double>();

        // Add these fields to the HarmonicsUIController class
        private Dictionary<int, double> _cachedAbsoluteAmplitudes = new Dictionary<int, double>();
        private Dictionary<int, double> _cachedPercentageAmplitudes = new Dictionary<int, double>();
        private Dictionary<int, string> _lastUnitSelections = new Dictionary<int, string>();

        private bool[] _cachedEnabledHarmonics = new bool[7];
        private bool _harmonicsEnabled = false;

        public HarmonicsUIController(HarmonicsManager harmonicsManager, Window mainWindow)
        {
            _harmonicsManager = harmonicsManager;
            _mainWindow = mainWindow;

            // Forward log events
            _harmonicsManager.LogEvent += (s, e) => LogEvent?.Invoke(this, e);

            // Initialize UI controls
            InitializeUIControls();
        }

        // Log helper method
        private void Log(string message)
        {
            LogEvent?.Invoke(this, message);
        }

        /// <summary>
        /// Initialize references to UI controls
        /// </summary>
        private void InitializeUIControls()
        {
            try
            {
                // Get main controls
                _harmonicsToggle = _mainWindow.FindName("HarmonicsToggle") as ToggleButton;
                _amplitudePercentageMode = _mainWindow.FindName("AmplitudePercentageMode") as RadioButton;
                _amplitudeAbsoluteMode = _mainWindow.FindName("AmplitudeAbsoluteMode") as RadioButton;
                _amplitudeHeader = _mainWindow.FindName("AmplitudeHeader") as TextBlock;

                // Get all harmonic-specific controls (CheckBoxes, TextBoxes)
                for (int i = 2; i <= 8; i++)
                {
                    CheckBox checkBox = _mainWindow.FindName($"Harmonic{i}CheckBox") as CheckBox;
                    TextBox ampTextBox = _mainWindow.FindName($"Harmonic{i}AmplitudeTextBox") as TextBox;
                    TextBox phaseTextBox = _mainWindow.FindName($"Harmonic{i}PhaseTextBox") as TextBox;

                    if (checkBox != null)
                        _harmonicCheckBoxes.Add(checkBox);

                    if (ampTextBox != null)
                        _harmonicAmplitudeTextBoxes.Add(ampTextBox);

                    // Add reference to amplitude unit combo boxes
                    ComboBox ampUnitComboBox = _mainWindow.FindName($"Harmonic{i}AmplitudeUnitComboBox") as ComboBox;
                    if (ampUnitComboBox != null)
                        _harmonicAmplitudeUnitComboBoxes.Add(ampUnitComboBox);

                    if (phaseTextBox != null)
                        _harmonicPhaseTextBoxes.Add(phaseTextBox);
                }

                // Attach event handlers
                AttachEventHandlers();
            }
            catch (Exception ex)
            {
                Log($"Error initializing UI controls: {ex.Message}");
            }
        }

        /// <summary>
        /// Attach event handlers to UI controls
        /// </summary>
        private void AttachEventHandlers()
        {
            try
            {
                // Attach to main controls
                if (_harmonicsToggle != null)
                    _harmonicsToggle.Click += HarmonicsToggle_Click;

                if (_amplitudePercentageMode != null)
                    _amplitudePercentageMode.Checked += AmplitudeModeChanged;

                if (_amplitudeAbsoluteMode != null)
                    _amplitudeAbsoluteMode.Checked += AmplitudeModeChanged;

                // Attach to harmonic controls
                for (int i = 0; i < _harmonicCheckBoxes.Count; i++)
                {
                    int harmonicNumber = i + 2; // Harmonics start at 2

                    if (_harmonicCheckBoxes[i] != null)
                    {
                        _harmonicCheckBoxes[i].Checked += (s, e) => HarmonicCheckBox_Changed(s, e, harmonicNumber);
                        _harmonicCheckBoxes[i].Unchecked += (s, e) => HarmonicCheckBox_Changed(s, e, harmonicNumber);
                    }

                    if (i < _harmonicAmplitudeTextBoxes.Count && _harmonicAmplitudeTextBoxes[i] != null)
                        _harmonicAmplitudeTextBoxes[i].LostFocus += (s, e) => HarmonicAmplitudeTextBox_LostFocus(s, e, harmonicNumber);

                    if (i < _harmonicPhaseTextBoxes.Count && _harmonicPhaseTextBoxes[i] != null)
                        _harmonicPhaseTextBoxes[i].LostFocus += (s, e) => HarmonicPhaseTextBox_LostFocus(s, e, harmonicNumber);
                }
            }
            catch (Exception ex)
            {
                Log($"Error attaching event handlers: {ex.Message}");
            }
        }


        // Combo Box event handler for unit selection 
        // Add new method to handle unit combo box selection changes
        private void HarmonicAmplitudeUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e, int harmonicNumber)
        {
            ComboBox comboBox = sender as ComboBox;
            if (comboBox == null) return;

            int index = harmonicNumber - 2;
            if (index < 0 || index >= _harmonicAmplitudeTextBoxes.Count)
                return;

            TextBox textBox = _harmonicAmplitudeTextBoxes[index];
            if (textBox == null || !double.TryParse(textBox.Text, out double value))
                return;

            // Get selected unit
            ComboBoxItem selectedItem = comboBox.SelectedItem as ComboBoxItem;
            if (selectedItem == null) return;

            string unit = selectedItem.Content.ToString();

            // Store last selection
            _lastUnitSelections[harmonicNumber] = unit;

            // Convert value to selected unit (based on previously stored absolute value)
            double absoluteValueInVolts = _cachedAbsoluteAmplitudes.TryGetValue(harmonicNumber, out double absValue) ? absValue : 0.0;
            double displayValue = ConvertFromVolts(absoluteValueInVolts, unit);

            // Update text box with formatted value
            textBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(displayValue);
        }

        // Helper methods for unit conversion
        private double ConvertFromVolts(double volts, string toUnit)
        {
            switch (toUnit)
            {
                case "mV": return volts * 1000.0;
                case "µV": return volts * 1000000.0;
                default: return volts; // V
            }
        }

        private double ConvertToVolts(double value, string fromUnit)
        {
            switch (fromUnit)
            {
                case "mV": return value / 1000.0;
                case "µV": return value / 1000000.0;
                default: return value; // V
            }
        }
        // end of new method


        /// <summary>
        /// Event handler for amplitude mode radio buttons
        /// </summary>
        private void AmplitudeModeChanged(object sender, RoutedEventArgs e)
        {
            // Update the mode flag
            _isPercentageMode = _amplitudePercentageMode.IsChecked == true;

            // Update the amplitude header text
            if (_amplitudeHeader != null)
                _amplitudeHeader.Text = _isPercentageMode ? "Amplitude (%)" : "Amplitude";

            // Update UI based on the selected mode
            UpdateUIFromCachedValues();

            Log($"Harmonic amplitude mode changed to {(_isPercentageMode ? "Percentage" : "Absolute")}");
        }



        /// <summary>
        /// Updates harmonic amplitudes when the fundamental amplitude changes
        /// </summary>
        public void UpdateHarmonicsForFundamentalChange(double newFundamentalAmplitude)
        {
            try
            {
                // Only proceed if harmonics are enabled
                if (_harmonicsToggle.IsChecked != true)
                    return;

                // Store the new fundamental amplitude
                _fundamentalAmplitude = newFundamentalAmplitude;

                if (_isPercentageMode)
                {
                    // EXISTING CODE: In percentage mode, recalculate absolute values from stored percentages
                    foreach (var kvp in _cachedPercentageAmplitudes)
                    {
                        int harmonicNumber = kvp.Key;
                        double percentage = kvp.Value;

                        // Update absolute amplitude based on new fundamental
                        _cachedAbsoluteAmplitudes[harmonicNumber] = (percentage / 100.0) * newFundamentalAmplitude;
                    }

                    // Get enabled harmonics and phases
                    bool[] enabledHarmonics = GetEnabledHarmonics();
                    Dictionary<int, double> phases = GetHarmonicPhases();

                    // Apply the new settings (always send absolute values to device)
                    _harmonicsManager.ApplyHarmonicSettings(enabledHarmonics, _cachedAbsoluteAmplitudes, phases, false);
                }
                else
                {
                    // NEW CODE: In absolute mode, keep absolute values the same, but recalculate percentages
                    foreach (var kvp in _cachedAbsoluteAmplitudes)
                    {
                        int harmonicNumber = kvp.Key;
                        double absolute = kvp.Value;

                        // Recalculate percentage based on new fundamental amplitude
                        _cachedPercentageAmplitudes[harmonicNumber] = (absolute / newFundamentalAmplitude) * 100.0;
                    }

                    // No need to apply to device since absolute values haven't changed
                }

                // Update UI display
                UpdateUIFromCachedValues();

                Log("Harmonic amplitudes updated for new fundamental amplitude");
            }
            catch (Exception ex)
            {
                Log($"Error updating harmonics for fundamental change: {ex.Message}");
            }
        }


        /// <summary>
        /// Event handler for harmonic toggle button
        /// </summary>
        private void HarmonicsToggle_Click(object sender, RoutedEventArgs e)
        {
            bool isEnabled = _harmonicsToggle.IsChecked == true;
            _harmonicsToggle.Content = isEnabled ? "ENABLED" : "DISABLED";

            try
            {
                if (isEnabled)
                {
                    // Enable harmonic UI elements
                    SetHarmonicUIElementsState(true);

                    // Check if there are any harmonics already configured
                    bool anyHarmonicEnabled = false;
                    for (int i = 0; i < _harmonicCheckBoxes.Count; i++)
                    {
                        if (_harmonicCheckBoxes[i].IsChecked == true ||
                            (i < _harmonicAmplitudeTextBoxes.Count &&
                             double.TryParse(_harmonicAmplitudeTextBoxes[i].Text, out double amp) &&
                             amp > 0))
                        {
                            anyHarmonicEnabled = true;
                            break;
                        }
                    }

                    if (anyHarmonicEnabled)
                    {
                        Log("Harmonics enabled. Auto-applying current harmonic settings...");
                        ApplyFullHarmonicSettings();
                    }
                    else
                    {
                        Log("Harmonics enabled. Adjust parameters to apply settings automatically.");
                    }
                }
                else
                {
                    // Disable harmonic mode
                    _harmonicsManager.SetHarmonicState(false);

                    // Set UI elements to read-only but preserve their values
                    SetHarmonicUIElementsState(false);
                }
            }
            catch (Exception ex)
            {
                Log($"Error toggling harmonics: {ex.Message}");
                MessageBox.Show($"Error toggling harmonics: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Event handler for harmonic checkbox changes
        /// </summary>
        private void HarmonicCheckBox_Changed(object sender, RoutedEventArgs e, int harmonicNumber)
        {
            CheckBox checkBox = sender as CheckBox;
            if (checkBox == null)
                return;

            bool isChecked = checkBox.IsChecked == true;
            Log($"Harmonic {harmonicNumber} {(isChecked ? "enabled" : "disabled")}");

            try
            {
                // Get current harmonic state
                bool harmonicsEnabled = _harmonicsToggle.IsChecked == true;
                if (harmonicsEnabled)
                {
                    // Get all currently enabled harmonics
                    bool[] enabledHarmonics = GetEnabledHarmonics();

                    // Update the pattern on the device
                    _harmonicsManager.UpdateHarmonicPattern(enabledHarmonics);

                    // If being enabled, also apply stored amplitude and phase
                    if (isChecked)
                    {
                        // Get amplitude and phase values
                        int index = harmonicNumber - 2;
                        double amplitude = 0;
                        double phase = 0;

                        if (index < _harmonicAmplitudeTextBoxes.Count &&
                            double.TryParse(_harmonicAmplitudeTextBoxes[index].Text, out amplitude))
                        {
                            _harmonicsManager.SetHarmonicAmplitude(harmonicNumber, amplitude, _isPercentageMode);
                        }

                        if (index < _harmonicPhaseTextBoxes.Count &&
                            double.TryParse(_harmonicPhaseTextBoxes[index].Text, out phase))
                        {
                            _harmonicsManager.SetHarmonicPhase(harmonicNumber, phase);
                        }
                    }

                    // Apply all settings
                    ApplyFullHarmonicSettings();
                }
            }
            catch (Exception ex)
            {
                Log($"Error updating harmonic selection: {ex.Message}");
            }
        }

        /// <summary>
        /// Event handler for harmonic amplitude textbox lost focus
        /// </summary>
        // Modify these methods to update the cache when user changes values
        // When the user changes amplitude values:
        // Modify HarmonicAmplitudeTextBox_LostFocus to handle unit conversions
        private void HarmonicAmplitudeTextBox_LostFocus(object sender, RoutedEventArgs e, int harmonicNumber)
        {
            TextBox textBox = sender as TextBox;
            if (textBox == null || !double.TryParse(textBox.Text, out double value))
                return;

            // Format value with minimum decimal places
            textBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(value, 1); // 1 decimal for frequency

            try
            {
                // Get current harmonic state
                bool isEnabled = _harmonicsToggle.IsChecked == true;
                if (isEnabled)
                {
                    // Store values based on mode
                    if (_isPercentageMode)
                    {
                        _cachedPercentageAmplitudes[harmonicNumber] = value;
                        _cachedAbsoluteAmplitudes[harmonicNumber] = (value / 100.0) * _fundamentalAmplitude;
                    }
                    else
                    {
                        // Get the current unit
                        string unit = "V";
                        int index = harmonicNumber - 2;
                        if (index >= 0 && index < _harmonicAmplitudeUnitComboBoxes.Count)
                        {
                            ComboBox unitComboBox = _harmonicAmplitudeUnitComboBoxes[index];
                            if (unitComboBox != null && unitComboBox.SelectedItem != null)
                            {
                                unit = ((ComboBoxItem)unitComboBox.SelectedItem).Content.ToString();
                            }
                        }

                        // Convert to volts and store
                        double volts = ConvertToVolts(value, unit);
                        _cachedAbsoluteAmplitudes[harmonicNumber] = volts;

                        // Calculate percentage based on fundamental
                        _cachedPercentageAmplitudes[harmonicNumber] = (volts / _fundamentalAmplitude) * 100.0;
                    }

                    // Only send to device if the harmonic is enabled
                    // CHANGE HERE: Renamed to harmonicIndex to avoid the conflict
                    int harmonicIndex = harmonicNumber - 2;
                    if (harmonicIndex < _harmonicCheckBoxes.Count && _harmonicCheckBoxes[harmonicIndex].IsChecked == true)
                    {
                        // Always send absolute value to the device
                        _harmonicsManager.SetHarmonicAmplitude(harmonicNumber, _cachedAbsoluteAmplitudes[harmonicNumber], false);

                        // Apply the full settings to ensure consistency
                        ApplyFullHarmonicSettings();
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error setting harmonic amplitude: {ex.Message}");
            }
        }




        /// <summary>
        /// Event handler for harmonic phase textbox lost focus
        /// </summary>
        private void HarmonicPhaseTextBox_LostFocus(object sender, RoutedEventArgs e, int harmonicNumber)
        {
            TextBox textBox = sender as TextBox;
            if (textBox == null || !double.TryParse(textBox.Text, out double phase))
                return;

            // Normalize phase to 0-360 range
            phase = ((phase % 360) + 360) % 360;

            // Format the value for display
            textBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(phase);

            try
            {
                // Get current harmonic state
                bool isEnabled = _harmonicsToggle.IsChecked == true;
                if (isEnabled)
                {
                    // Ensure the harmonic is enabled
                    int index = harmonicNumber - 2;
                    if (index < _harmonicCheckBoxes.Count && _harmonicCheckBoxes[index].IsChecked == true)
                    {
                        // Set the phase on the device
                        _harmonicsManager.SetHarmonicPhase(harmonicNumber, phase);

                        // Apply the change
                        ApplyFullHarmonicSettings();
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error setting harmonic phase: {ex.Message}");
            }
        }

        /// <summary>
        /// Apply all harmonic settings
        /// </summary>
        private void ApplyFullHarmonicSettings()
        {
            try
            {
                // Get current values from UI
                bool[] enabledHarmonics = GetEnabledHarmonics();
                Dictionary<int, double> amplitudes = GetHarmonicAmplitudes();
                Dictionary<int, double> phases = GetHarmonicPhases();

                // Apply to device
                _harmonicsManager.ApplyHarmonicSettings(enabledHarmonics, amplitudes, phases, _isPercentageMode);
            }
            catch (Exception ex)
            {
                Log($"Error applying harmonic settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Get enabled harmonics from UI
        /// </summary>
        private bool[] GetEnabledHarmonics()
        {
            bool[] enabledHarmonics = new bool[7]; // For harmonics 2-8

            for (int i = 0; i < _harmonicCheckBoxes.Count && i < 7; i++)
            {
                enabledHarmonics[i] = _harmonicCheckBoxes[i].IsChecked == true;
            }

            return enabledHarmonics;
        }

        /// <summary>
        /// Get harmonic amplitudes from UI
        /// </summary>
        private Dictionary<int, double> GetHarmonicAmplitudes()
        {
            Dictionary<int, double> amplitudes = new Dictionary<int, double>();

            for (int i = 0; i < _harmonicAmplitudeTextBoxes.Count && i < 7; i++)
            {
                int harmonicNumber = i + 2;
                if (double.TryParse(_harmonicAmplitudeTextBoxes[i].Text, out double amplitude))
                {
                    amplitudes[harmonicNumber] = amplitude;
                }
            }

            return amplitudes;
        }

        /// <summary>
        /// Get harmonic phases from UI
        /// </summary>
        private Dictionary<int, double> GetHarmonicPhases()
        {
            Dictionary<int, double> phases = new Dictionary<int, double>();

            for (int i = 0; i < _harmonicPhaseTextBoxes.Count && i < 7; i++)
            {
                int harmonicNumber = i + 2;
                if (double.TryParse(_harmonicPhaseTextBoxes[i].Text, out double phase))
                {
                    phases[harmonicNumber] = phase;
                }
            }

            return phases;
        }

        /// <summary>
        /// Reset all harmonic values to zero
        /// </summary>
        public void ResetHarmonicValues()
        {
            try
            {
                // Reset amplitude and phase values
                for (int i = 0; i < _harmonicAmplitudeTextBoxes.Count; i++)
                {
                    _harmonicAmplitudeTextBoxes[i].Text = "0.0";
                }

                for (int i = 0; i < _harmonicPhaseTextBoxes.Count; i++)
                {
                    _harmonicPhaseTextBoxes[i].Text = "0.0";
                }

                // Uncheck all harmonics
                for (int i = 0; i < _harmonicCheckBoxes.Count; i++)
                {
                    _harmonicCheckBoxes[i].IsChecked = false;
                }
            }
            catch (Exception ex)
            {
                Log($"Error resetting harmonic values: {ex.Message}");
            }
        }

        /// <summary>
        /// Set the enabled state of harmonic UI elements
        /// </summary>
        public void SetHarmonicUIElementsState(bool enabled)
        {
            try
            {
                // Set checkbox enabled state
                foreach (var checkBox in _harmonicCheckBoxes)
                {
                    checkBox.IsEnabled = enabled;
                }

                // Set amplitude textbox read-only state
                foreach (var textBox in _harmonicAmplitudeTextBoxes)
                {
                    textBox.IsReadOnly = !enabled;
                }

                // Set phase textbox read-only state
                foreach (var textBox in _harmonicPhaseTextBoxes)
                {
                    textBox.IsReadOnly = !enabled;
                }

                // Set mode radio buttons enabled state
                if (_amplitudePercentageMode != null)
                    _amplitudePercentageMode.IsEnabled = enabled;

                if (_amplitudeAbsoluteMode != null)
                    _amplitudeAbsoluteMode.IsEnabled = enabled;
            }
            catch (Exception ex)
            {
                Log($"Error setting harmonic UI state: {ex.Message}");
            }
        }

        /// <summary>
        /// Refresh the harmonic settings in the UI
        /// </summary>
        // Modify the RefreshHarmonicSettings method
        // Also update RefreshHarmonicSettings to populate both cache dictionaries
        public void RefreshHarmonicSettings()
        {
            try
            {
                // Get current settings from device
                var (isEnabled, amplitudes, phases, enabledHarmonics) =
                    _harmonicsManager.GetCurrentHarmonicSettings(_isPercentageMode);

                // Cache the retrieved values
                _harmonicsEnabled = isEnabled;

                // Get fundamental amplitude
                _fundamentalAmplitude = _harmonicsManager.GetFundamentalAmplitude();

                // Store both percentage and absolute values
                _cachedAbsoluteAmplitudes.Clear();
                _cachedPercentageAmplitudes.Clear();

                foreach (var kvp in amplitudes)
                {
                    int harmonicNumber = kvp.Key;
                    double value = kvp.Value;

                    if (_isPercentageMode)
                    {
                        // Device returned percentages
                        _cachedPercentageAmplitudes[harmonicNumber] = value;
                        _cachedAbsoluteAmplitudes[harmonicNumber] = (value / 100.0) * _fundamentalAmplitude;
                    }
                    else
                    {
                        // Device returned absolute values
                        _cachedAbsoluteAmplitudes[harmonicNumber] = value;
                        _cachedPercentageAmplitudes[harmonicNumber] = (value / _fundamentalAmplitude) * 100.0;
                    }
                }

                Array.Copy(enabledHarmonics, _cachedEnabledHarmonics,
                    Math.Min(enabledHarmonics.Length, _cachedEnabledHarmonics.Length));

                // Update UI controls
                _harmonicsToggle.IsChecked = isEnabled;
                _harmonicsToggle.Content = isEnabled ? "ENABLED" : "DISABLED";

                // Set UI elements state
                SetHarmonicUIElementsState(isEnabled);

                if (isEnabled)
                {
                    UpdateUIFromCachedValues();
                }

                Log("Harmonic settings refreshed from device");
            }
            catch (Exception ex)
            {
                Log($"Error refreshing harmonic settings: {ex.Message}");
            }
        }

        // Add a new method to update UI from cached values
        // Update the display method to use the correct cached values based on mode
        private void UpdateUIFromCachedValues()
        {
            // Update amplitude values
            for (int i = 0; i < _harmonicAmplitudeTextBoxes.Count; i++)
            {
                int harmonicNumber = i + 2;

                if (_isPercentageMode)
                {
                    // Percentage mode handling (unchanged)
                    if (_cachedPercentageAmplitudes.TryGetValue(harmonicNumber, out double percentage))
                    {
                        _harmonicAmplitudeTextBoxes[i].Text = UnitConversionUtility.FormatWithMinimumDecimals(percentage);
                    }
                }
                else // Absolute mode
                {
                    if (_cachedAbsoluteAmplitudes.TryGetValue(harmonicNumber, out double volts))
                    {
                        // Handle auto-ranging based on value
                        string unit = "V";
                        double displayValue = volts;

                        if (Math.Abs(volts) < 0.1 && Math.Abs(volts) >= 0.0001)
                        {
                            unit = "mV";
                            displayValue = volts * 1000.0;
                        }
                        else if (Math.Abs(volts) < 0.0001 && Math.Abs(volts) >= 0.0000001)
                        {
                            unit = "µV";
                            displayValue = volts * 1000000.0;
                        }
                        else if (Math.Abs(volts) < 0.0000001)
                        {
                            // Special handling for very small values
                            unit = "V";
                            displayValue = 0.0;
                        }

                        // Update text box with formatted value
                        _harmonicAmplitudeTextBoxes[i].Text = UnitConversionUtility.FormatWithMinimumDecimals(displayValue);

                        // Update unit combo box selection
                        if (i < _harmonicAmplitudeUnitComboBoxes.Count)
                        {
                            ComboBox unitComboBox = _harmonicAmplitudeUnitComboBoxes[i];
                            if (unitComboBox != null)
                            {
                                foreach (ComboBoxItem item in unitComboBox.Items)
                                {
                                    if (item.Content.ToString() == unit)
                                    {
                                        unitComboBox.SelectedItem = item;
                                        break;
                                    }
                                }
                            }
                        }

                        // Store the selected unit
                        _lastUnitSelections[harmonicNumber] = unit;
                    }
                }
            }

            // Update phase values (unchanged)
            for (int i = 0; i < _harmonicPhaseTextBoxes.Count; i++)
            {
                int harmonicNumber = i + 2;

                if (_cachedPhases.TryGetValue(harmonicNumber, out double phase))
                {
                    _harmonicPhaseTextBoxes[i].Text = UnitConversionUtility.FormatWithMinimumDecimals(phase);
                }
            }
        }

        /// <summary>
        /// Format a double value with minimum decimals
        /// </summary>
        //private string FormatWithMinimumDecimals(double value, int minDecimals = 2)
        //{
        //    // Get the number as a string with many decimal places
        //    string fullPrecision = value.ToString("F12");

        //    // Trim trailing zeros, but ensure at least minDecimals decimal places
        //    string[] parts = fullPrecision.Split('.');

        //    if (parts.Length == 1)
        //    {
        //        // No decimal part
        //        return value.ToString($"F{minDecimals}");
        //    }

        //    // Trim trailing zeros but keep at least minDecimals digits
        //    string decimals = parts[1].TrimEnd('0');

        //    // If we trimmed too much, pad with zeros to meet minimum
        //    if (decimals.Length < minDecimals)
        //    {
        //        decimals = decimals.PadRight(minDecimals, '0');
        //    }

        //    return $"{parts[0]}.{decimals}";
        //}
    }
}