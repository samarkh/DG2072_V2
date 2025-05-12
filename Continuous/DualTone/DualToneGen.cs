using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using DG2072_USB_Control.Services;

namespace DG2072_USB_Control.Continuous.DualTone
{
    public class DualToneGen : IDualToneEventHandler
    {
        // Device reference
        private readonly RigolDG2072 _device;

        // Active channel
        private int _activeChannel;

        // UI elements
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

        // Settings
        private double _frequencyRatio = 2.0; // Default frequency ratio

        // Event for logging
        public event EventHandler<string> LogEvent;

        // Constructor
        public DualToneGen(RigolDG2072 device, int channel, Window mainWindow)
        {
            _device = device;
            _activeChannel = channel;

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

            // Main frequency controls (needed for synchronization)
            _primaryFrequencyTextBox = mainWindow.FindName("ChannelFrequencyTextBox") as TextBox;
            _primaryFrequencyUnitComboBox = mainWindow.FindName("ChannelFrequencyUnitComboBox") as ComboBox;
        }

        // Property for the active channel
        public int ActiveChannel
        {
            get => _activeChannel;
            set => _activeChannel = value;
        }

        // Log helper method
        private void Log(string message)
        {
            LogEvent?.Invoke(this, message);
        }

        #region IDualToneEventHandler Implementation

        public void OnSecondaryFrequencyTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsDeviceConnected()) return;
            if (!double.TryParse(_secondaryFrequencyTextBox.Text, out double _)) return;

            // Use a timer to debounce rapid changes
            if (_secondaryFrequencyUpdateTimer == null)
            {
                _secondaryFrequencyUpdateTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                _secondaryFrequencyUpdateTimer.Tick += (s, args) =>
                {
                    _secondaryFrequencyUpdateTimer.Stop();
                    if (double.TryParse(_secondaryFrequencyTextBox.Text, out double _))
                    {
                        ApplyDualToneParameters();
                    }
                };
            }

            _secondaryFrequencyUpdateTimer.Stop();
            _secondaryFrequencyUpdateTimer.Start();
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

            if (_centerFrequencyUpdateTimer == null)
            {
                _centerFrequencyUpdateTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                _centerFrequencyUpdateTimer.Tick += (s, args) =>
                {
                    _centerFrequencyUpdateTimer.Stop();
                    UpdateFrequenciesFromCenterOffset();
                };
            }

            _centerFrequencyUpdateTimer.Stop();
            _centerFrequencyUpdateTimer.Start();
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

            if (_offsetFrequencyUpdateTimer == null)
            {
                _offsetFrequencyUpdateTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                _offsetFrequencyUpdateTimer.Tick += (s, args) =>
                {
                    _offsetFrequencyUpdateTimer.Stop();
                    UpdateFrequenciesFromCenterOffset();
                };
            }

            _offsetFrequencyUpdateTimer.Stop();
            _offsetFrequencyUpdateTimer.Start();
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

        // Check if the device is connected
        private bool IsDeviceConnected()
        {
            return _device != null && _device.IsConnected;
        }

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

                    // Get amplitude, offset, phase
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
                    _device.ApplyDualToneWaveform(_activeChannel, parameters);

                    Log($"Applied Dual Tone waveform to CH{_activeChannel} with Primary Freq={frequency} {freqUnit}, " +
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

                // Calculate center frequency (F1 + F2)/2
                double centerFreqHz = (f1Hz + f2Hz) / 2.0;

                // Calculate offset frequency F2 - F1
                double offsetFreqHz = f2Hz - f1Hz;

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
        private void UpdateFrequenciesFromCenterOffset()
        {
            try
            {
                // Get current center and offset frequencies in Hz
                double centerFreqHz = 0, offsetFreqHz = 0;

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

                // Calculate F1 and F2 from center and offset
                // Center = (F1 + F2)/2, Offset = F2 - F1
                // F1 = Center - Offset/2, F2 = Center + Offset/2
                double f1Hz = centerFreqHz - (offsetFreqHz / 2.0);
                double f2Hz = centerFreqHz + (offsetFreqHz / 2.0);

                // Update the calculated values display using UnitConversionUtility
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
                // Get amplitude, offset, phase from MainWindow
                double amplitude = GetAmplitudeFromUI();
                double offset = GetOffsetFromUI();
                double phase = GetPhaseFromUI();

                // Create parameters dictionary for the device
                Dictionary<string, object> parameters = new Dictionary<string, object>
                {
                    { "Frequency", f1Hz },
                    { "Frequency2", f2Hz },
                    { "Amplitude", amplitude },
                    { "Offset", offset },
                    { "Phase", phase }
                };

                // Apply the dual tone waveform
                _device.ApplyDualToneWaveform(_activeChannel, parameters);

                Log($"Applied Dual Tone waveform to CH{_activeChannel} with F1={f1Hz}Hz, F2={f2Hz}Hz, " +
                    $"Center={(f1Hz + f2Hz) / 2}Hz, Offset={f2Hz - f1Hz}Hz, " +
                    $"Amp={amplitude}Vpp, Offset={offset}V, Phase={phase}°");
            }
            catch (Exception ex)
            {
                Log($"Error applying dual tone with frequencies: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods

        // Helper for adjusting frequency and unit display using UnitConversionUtility
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

        // Helper methods to get values from UI
        private double GetAmplitudeFromUI()
        {
            TextBox amplitudeTextBox = FindControl("ChannelAmplitudeTextBox") as TextBox;
            ComboBox amplitudeUnitComboBox = FindControl("ChannelAmplitudeUnitComboBox") as ComboBox;

            if (amplitudeTextBox != null && amplitudeUnitComboBox != null &&
                double.TryParse(amplitudeTextBox.Text, out double amplitude))
            {
                string ampUnit = UnitConversionUtility.GetAmplitudeUnit(amplitudeUnitComboBox);
                double ampMultiplier = UnitConversionUtility.GetAmplitudeMultiplier(ampUnit);
                return amplitude * ampMultiplier;
            }

            return 1.0; // Default 1Vpp
        }

        private double GetOffsetFromUI()
        {
            TextBox offsetTextBox = FindControl("ChannelOffsetTextBox") as TextBox;

            if (offsetTextBox != null && double.TryParse(offsetTextBox.Text, out double offset))
            {
                return offset;
            }

            return 0.0; // Default 0V
        }

        private double GetPhaseFromUI()
        {
            TextBox phaseTextBox = FindControl("ChannelPhaseTextBox") as TextBox;

            if (phaseTextBox != null && double.TryParse(phaseTextBox.Text, out double phase))
            {
                return phase;
            }

            return 0.0; // Default 0°
        }

        private object FindControl(string controlName)
        {
            if (_directFrequencyPanel != null)
            {
                Window mainWindow = Window.GetWindow(_directFrequencyPanel);
                return mainWindow?.FindName(controlName);
            }
            return null;
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
                var parameters = _device.GetAllDualToneParameters(_activeChannel);

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