using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

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

        // Flag for amplitude mode
        private bool _isPercentageMode = true;

        // Event for logging
        public event EventHandler<string> LogEvent;

        // Add these fields near the top of the HarmonicsUIController class
        private double _fundamentalAmplitude = 1.0;
        private Dictionary<int, double> _cachedAmplitudes = new Dictionary<int, double>();
        private Dictionary<int, double> _cachedPhases = new Dictionary<int, double>();
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
        /// <summary>
        /// Updates harmonic amplitudes when the fundamental amplitude changes
        /// </summary>
        public void UpdateHarmonicsForFundamentalChange(double newFundamentalAmplitude)
        {
            try
            {
                // Only proceed if harmonics are enabled and in percentage mode
                if (HarmonicsToggle.IsChecked != true || !_isPercentageMode)
                    return;

                // Store the new fundamental amplitude
                _fundamentalAmplitude = newFundamentalAmplitude;

                // Get current UI values (which are percentages in the UI)
                bool[] enabledHarmonics = GetEnabledHarmonics();
                Dictionary<int, double> amplitudes = GetHarmonicAmplitudes();
                Dictionary<int, double> phases = GetHarmonicPhases();

                // Apply all settings - this will convert percentages to absolute values
                // using the new fundamental amplitude
                _harmonicsManager.ApplyHarmonicSettings(enabledHarmonics, amplitudes, phases, _isPercentageMode);

                Log("Harmonic amplitudes updated for new fundamental amplitude");
            }
            catch (Exception ex)
            {
                Log($"Error updating harmonics for fundamental change: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates harmonic amplitudes when the fundamental amplitude changes
        /// </summary>
        public void UpdateHarmonicsForFundamentalChange(double newFundamentalAmplitude)
        {
            try
            {
                // Only proceed if harmonics are enabled and in percentage mode
                if (_harmonicsToggle.IsChecked != true || !_isPercentageMode)
                    return;

                // Store the new fundamental amplitude
                _fundamentalAmplitude = newFundamentalAmplitude;

                // Update cached values for display
                bool[] enabledHarmonics = GetEnabledHarmonics();
                Dictionary<int, double> amplitudes = GetHarmonicAmplitudes();
                Dictionary<int, double> phases = GetHarmonicPhases();

                // Apply all settings - this will convert percentages to absolute values
                // using the new fundamental amplitude
                _harmonicsManager.ApplyHarmonicSettings(enabledHarmonics, amplitudes, phases, _isPercentageMode);

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
        /// Event handler for amplitude mode radio buttons
        /// </summary>
        // Modify the AmplitudeModeChanged method
        private void AmplitudeModeChanged(object sender, RoutedEventArgs e)
        {
            _isPercentageMode = _amplitudePercentageMode.IsChecked == true;

            // Update the amplitude header text
            if (_amplitudeHeader != null)
                _amplitudeHeader.Text = _isPercentageMode ? "Amplitude (%)" : "Amplitude (V)";

            // Update display only, don't query the device
            UpdateUIFromCachedValues();

            Log($"Harmonic amplitude mode changed to {(_isPercentageMode ? "Percentage" : "Absolute")}");
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
        private void HarmonicAmplitudeTextBox_LostFocus(object sender, RoutedEventArgs e, int harmonicNumber)
        {
            TextBox textBox = sender as TextBox;
            if (textBox == null || !double.TryParse(textBox.Text, out double amplitude))
                return;

            // Format the value for display
            textBox.Text = FormatWithMinimumDecimals(amplitude);

            try
            {
                // Get current harmonic state
                bool isEnabled = _harmonicsToggle.IsChecked == true;
                if (isEnabled)
                {
                    // Convert to absolute value if in percentage mode
                    double actualAmplitude = amplitude;
                    if (_isPercentageMode)
                    {
                        actualAmplitude = (amplitude / 100.0) * _fundamentalAmplitude;
                    }

                    // Update cached value
                    _cachedAmplitudes[harmonicNumber] = actualAmplitude;

                    // Ensure the harmonic is enabled
                    int index = harmonicNumber - 2;
                    if (index < _harmonicCheckBoxes.Count && _harmonicCheckBoxes[index].IsChecked == true)
                    {
                        // Set the amplitude on the device
                        _harmonicsManager.SetHarmonicAmplitude(harmonicNumber, actualAmplitude, false); // Send absolute value

                        // Apply the change
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
            textBox.Text = FormatWithMinimumDecimals(phase);

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
        public void RefreshHarmonicSettings()
        {
            try
            {
                // Get current settings from device
                var (isEnabled, amplitudes, phases, enabledHarmonics) =
                    _harmonicsManager.GetCurrentHarmonicSettings(_isPercentageMode);

                // Cache the retrieved values
                _harmonicsEnabled = isEnabled;
                _cachedAmplitudes = new Dictionary<int, double>(amplitudes);
                _cachedPhases = new Dictionary<int, double>(phases);
                Array.Copy(enabledHarmonics, _cachedEnabledHarmonics,
                    Math.Min(enabledHarmonics.Length, _cachedEnabledHarmonics.Length));

                // Get fundamental amplitude for percentage calculations
                _fundamentalAmplitude = _harmonicsManager.GetFundamentalAmplitude();

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
        private void UpdateUIFromCachedValues()
        {


            // Update amplitude values
            for (int i = 0; i < _harmonicAmplitudeTextBoxes.Count; i++)
            {
                int harmonicNumber = i + 2;

                if (_cachedAmplitudes.TryGetValue(harmonicNumber, out double amplitude))
                {
                    // Convert to current display mode if needed
                    double displayValue = amplitude;

                    if (_isPercentageMode)
                    {
                        // Display as percentage of fundamental
                        displayValue = (amplitude / _fundamentalAmplitude) * 100.0;
                    }

                    _harmonicAmplitudeTextBoxes[i].Text = FormatWithMinimumDecimals(displayValue);
                }
            }

            // Update phase values
            for (int i = 0; i < _harmonicPhaseTextBoxes.Count; i++)
            {
                int harmonicNumber = i + 2;

                if (_cachedPhases.TryGetValue(harmonicNumber, out double phase))
                {
                    _harmonicPhaseTextBoxes[i].Text = FormatWithMinimumDecimals(phase);
                }
            }
        }


        /// <summary>
        /// Format a double value with minimum decimals
        /// </summary>
        private string FormatWithMinimumDecimals(double value, int minDecimals = 2)
        {
            // Get the number as a string with many decimal places
            string fullPrecision = value.ToString("F12");

            // Trim trailing zeros, but ensure at least minDecimals decimal places
            string[] parts = fullPrecision.Split('.');

            if (parts.Length == 1)
            {
                // No decimal part
                return value.ToString($"F{minDecimals}");
            }

            // Trim trailing zeros but keep at least minDecimals digits
            string decimals = parts[1].TrimEnd('0');

            // If we trimmed too much, pad with zeros to meet minimum
            if (decimals.Length < minDecimals)
            {
                decimals = decimals.PadRight(minDecimals, '0');
            }

            return $"{parts[0]}.{decimals}";
        }
    }
}