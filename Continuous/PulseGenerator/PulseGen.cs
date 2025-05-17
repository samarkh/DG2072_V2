using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using DG2072_USB_Control.Services;

namespace DG2072_USB_Control.Continuous.PulseGenerator
{
    public class PulseGen : WaveformGenerator, IPulseEventHandler
    {
        // UI elements specific to Pulse
        private readonly TextBox _pulseWidthTextBox;
        private readonly TextBox _pulsePeriodTextBox;
        private readonly TextBox _pulseRiseTimeTextBox;
        private readonly TextBox _pulseFallTimeTextBox;
        private readonly ComboBox _pulseWidthUnitComboBox;
        private readonly ComboBox _pulsePeriodUnitComboBox;
        private readonly ComboBox _pulseRiseTimeUnitComboBox;
        private readonly ComboBox _pulseFallTimeUnitComboBox;
        private readonly ToggleButton _pulseRateModeToggle;

        // Update timers for debouncing
        private DispatcherTimer _pulseWidthUpdateTimer;
        private DispatcherTimer _pulsePeriodUpdateTimer;
        private DispatcherTimer _pulseRiseTimeUpdateTimer;
        private DispatcherTimer _pulseFallTimeUpdateTimer;

        // Mode flag
        private bool _frequencyModeActive = true;

        // DockPanels for controlling visibility
        private readonly DockPanel _pulseWidthDockPanel;
        private readonly DockPanel _pulsePeriodDockPanel;
        private readonly DockPanel _pulseRiseTimeDockPanel;
        private readonly DockPanel _pulseFallTimeDockPanel;
        private readonly DockPanel _pulseRateModeDockPanel;

        // Frequency-related controls for calculated value
        private readonly TextBox _frequencyTextBox;
        private readonly ComboBox _frequencyUnitComboBox;

        /// <summary>
        /// Constructor - initializes the pulse generator with device and UI references
        /// </summary>
        public PulseGen(RigolDG2072 device, int channel, Window mainWindow)
            : base(device, channel, mainWindow)
        {
            // Initialize UI references - we need to find references from mainWindow
            _pulseWidthTextBox = mainWindow.FindName("PulseWidth") as TextBox;
            _pulsePeriodTextBox = mainWindow.FindName("PulsePeriod") as TextBox;
            _pulseRiseTimeTextBox = mainWindow.FindName("PulseRiseTime") as TextBox;
            _pulseFallTimeTextBox = mainWindow.FindName("PulseFallTime") as TextBox;
            _pulseWidthUnitComboBox = mainWindow.FindName("PulseWidthUnitComboBox") as ComboBox;
            _pulsePeriodUnitComboBox = mainWindow.FindName("PulsePeriodUnitComboBox") as ComboBox;
            _pulseRiseTimeUnitComboBox = mainWindow.FindName("PulseRiseTimeUnitComboBox") as ComboBox;
            _pulseFallTimeUnitComboBox = mainWindow.FindName("PulseFallTimeUnitComboBox") as ComboBox;
            _pulseRateModeToggle = mainWindow.FindName("PulseRateModeToggle") as ToggleButton;

            // Find dock panels
            _pulseWidthDockPanel = FindVisualParent<DockPanel>(_pulseWidthTextBox);
            _pulsePeriodDockPanel = FindVisualParent<DockPanel>(_pulsePeriodTextBox);
            _pulseRiseTimeDockPanel = FindVisualParent<DockPanel>(_pulseRiseTimeTextBox);
            _pulseFallTimeDockPanel = FindVisualParent<DockPanel>(_pulseFallTimeTextBox);
            _pulseRateModeDockPanel = mainWindow.FindName("PulseRateModeDockPanel") as DockPanel;

            // Find frequency controls for calculated values
            _frequencyTextBox = mainWindow.FindName("ChannelFrequencyTextBox") as TextBox;
            _frequencyUnitComboBox = mainWindow.FindName("ChannelFrequencyUnitComboBox") as ComboBox;
        }

        /// <summary>
        /// Helper method to find parent control in visual tree
        /// </summary>
        private T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            if (child == null) return null;

            DependencyObject parentObject = System.Windows.Media.VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;

            T parent = parentObject as T;
            if (parent != null)
                return parent;
            else
                return FindVisualParent<T>(parentObject);
        }

        /// <summary>
        /// Property for the frequency/period mode
        /// </summary>
        public bool FrequencyModeActive => _frequencyModeActive;

        #region WaveformGenerator Overrides

        /// <summary>
        /// Apply all pulse parameters - REQUIRED by WaveformGenerator base class
        /// </summary>
        public override void ApplyParameters()
        {
            if (!IsDeviceConnected()) return;

            try
            {
                Log("Applying pulse parameters in sequence...");

                // Handle based on which mode is active
                if (_frequencyModeActive)
                {
                    // In Frequency mode, send frequency directly to the device
                    if (double.TryParse(_frequencyTextBox.Text, out double frequency))
                    {
                        string freqUnit = UnitConversionUtility.GetFrequencyUnit(_frequencyUnitComboBox);
                        double actualFrequency = frequency * UnitConversionUtility.GetFrequencyMultiplier(freqUnit);

                        // Send frequency command directly
                        Device.SetFrequency(ActiveChannel, actualFrequency);
                        Log($"Set pulse frequency to {frequency} {freqUnit} ({actualFrequency} Hz)");

                        // Update UI but don't send this to device
                        double period = 1.0 / actualFrequency;
                        UpdatePulseTimeValue(_pulsePeriodTextBox, _pulsePeriodUnitComboBox, period);
                    }
                }
                else
                {
                    // In Period mode, send period directly to the device
                    if (double.TryParse(_pulsePeriodTextBox.Text, out double period))
                    {
                        string periodUnit = UnitConversionUtility.GetPeriodUnit(_pulsePeriodUnitComboBox);
                        double actualPeriod = period * UnitConversionUtility.GetPeriodMultiplier(periodUnit);

                        // Send period command directly - don't convert to frequency
                        Device.SetPulsePeriod(ActiveChannel, actualPeriod);
                        Log($"Set pulse period to {period} {periodUnit} ({actualPeriod} s)");

                        // Update UI but don't send this to device
                        double frequency = 1.0 / actualPeriod;
                        double displayValue = UnitConversionUtility.ConvertFromMicroHz(frequency * 1e6,
                            UnitConversionUtility.GetFrequencyUnit(_frequencyUnitComboBox));
                        _frequencyTextBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(displayValue);
                    }
                }

                // Apply transition times and width
                ApplyPulseTransitionTimes();
                ApplyPulseWidth();

                // Refresh UI with actual device values
                UpdatePulseParameters(ActiveChannel);
                Log("All pulse parameters applied");
            }
            catch (Exception ex)
            {
                Log($"Error applying pulse parameters: {ex.Message}");
            }
        }

        /// <summary>
        /// Refresh all pulse settings from the device - REQUIRED by WaveformGenerator base class
        /// </summary>
        public override void RefreshParameters()
        {
            if (!IsDeviceConnected()) return;

            try
            {
                UpdatePulseWidthValue();
                UpdatePulseRiseTimeValue();
                UpdatePulseFallTimeValue();
                UpdateImpedanceSelection();

                Log($"Refreshed pulse parameters for CH{ActiveChannel}");
            }
            catch (Exception ex)
            {
                Log($"Error refreshing pulse parameters: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods

        // Apply transition times (rise and fall)
        private void ApplyPulseTransitionTimes()
        {
            if (double.TryParse(_pulseRiseTimeTextBox.Text, out double riseTime))
            {
                string riseTimeUnit = UnitConversionUtility.GetPeriodUnit(_pulseRiseTimeUnitComboBox);
                double actualRiseTime = riseTime * UnitConversionUtility.GetPeriodMultiplier(riseTimeUnit);
                Device.SetPulseRiseTime(ActiveChannel, actualRiseTime);
                Log($"Set pulse rise time to {riseTime} {riseTimeUnit} ({actualRiseTime} s)");
            }

            if (double.TryParse(_pulseFallTimeTextBox.Text, out double fallTime))
            {
                string fallTimeUnit = UnitConversionUtility.GetPeriodUnit(_pulseFallTimeUnitComboBox);
                double actualFallTime = fallTime * UnitConversionUtility.GetPeriodMultiplier(fallTimeUnit);
                Device.SetPulseFallTime(ActiveChannel, actualFallTime);
                Log($"Set pulse fall time to {fallTime} {fallTimeUnit} ({actualFallTime} s)");
            }
        }

        // Apply width constraints and validation
        private void ApplyPulseWidth()
        {
            // Validate parameters before applying width
            ValidatePulseParameters();

            // Apply the width (which must fit within the period)
            if (double.TryParse(_pulseWidthTextBox.Text, out double width))
            {
                string widthUnit = UnitConversionUtility.GetPeriodUnit(_pulseWidthUnitComboBox);
                double actualWidth = width * UnitConversionUtility.GetPeriodMultiplier(widthUnit);
                Device.SetPulseWidth(ActiveChannel, actualWidth);
                Log($"Set pulse width to {width} {widthUnit} ({actualWidth} s)");
            }
        }

        /// <summary>
        /// Update pulse width value in UI from device
        /// </summary>
        public void UpdatePulseWidthValue()
        {
            if (!IsDeviceConnected()) return;

            try
            {
                double width = Device.GetPulseWidth(ActiveChannel);
                UpdatePulseTimeValue(_pulseWidthTextBox, _pulseWidthUnitComboBox, width);
            }
            catch (Exception ex)
            {
                Log($"Error updating pulse width value: {ex.Message}");
            }
        }

        /// <summary>
        /// Update pulse rise time value in UI from device
        /// </summary>
        public void UpdatePulseRiseTimeValue()
        {
            if (!IsDeviceConnected()) return;

            try
            {
                double riseTime = Device.GetPulseRiseTime(ActiveChannel);
                UpdatePulseTimeValue(_pulseRiseTimeTextBox, _pulseRiseTimeUnitComboBox, riseTime);
            }
            catch (Exception ex)
            {
                Log($"Error updating pulse rise time value: {ex.Message}");
            }
        }

        /// <summary>
        /// Update pulse fall time value in UI from device
        /// </summary>
        public void UpdatePulseFallTimeValue()
        {
            if (!IsDeviceConnected()) return;

            try
            {
                double fallTime = Device.GetPulseFallTime(ActiveChannel);
                UpdatePulseTimeValue(_pulseFallTimeTextBox, _pulseFallTimeUnitComboBox, fallTime);
            }
            catch (Exception ex)
            {
                Log($"Error updating pulse fall time value: {ex.Message}");
            }
        }

        // Rest of the implementation would follow... (existing methods)

        /// <summary>
        /// Helper method for updating pulse time value in UI with appropriate unit
        /// </summary>
        public void UpdatePulseTimeValue(TextBox timeTextBox, ComboBox unitComboBox, double timeValue)
        {
            if (timeTextBox == null || unitComboBox == null) return;

            try
            {
                // Store current unit to preserve it if possible
                string currentUnit = UnitConversionUtility.GetPeriodUnit(unitComboBox);

                // Convert to picoseconds for internal representation
                double psValue = timeValue * 1e12; // Convert seconds to picoseconds

                // Calculate the display value based on the current unit
                double displayValue = UnitConversionUtility.ConvertFromPicoSeconds(psValue, currentUnit);

                // If the value would display poorly in the current unit, find a better unit
                if (displayValue > 9999 || displayValue < 0.1)
                {
                    string[] units = { "ps", "ns", "µs", "ms", "s" };
                    int bestUnitIndex = 2; // Default to µs

                    for (int i = 0; i < units.Length; i++)
                    {
                        double testValue = UnitConversionUtility.ConvertFromPicoSeconds(psValue, units[i]);
                        if (testValue >= 0.1 && testValue < 10000)
                        {
                            bestUnitIndex = i;
                            break;
                        }
                    }

                    // Update the display value and select the best unit
                    displayValue = UnitConversionUtility.ConvertFromPicoSeconds(psValue, units[bestUnitIndex]);

                    // Find and select the unit in the combo box
                    for (int i = 0; i < unitComboBox.Items.Count; i++)
                    {
                        ComboBoxItem item = unitComboBox.Items[i] as ComboBoxItem;
                        if (item != null && item.Content.ToString() == units[bestUnitIndex])
                        {
                            unitComboBox.SelectedIndex = i;
                            break;
                        }
                    }
                }

                // Use the UnitConversionUtility for formatting
                timeTextBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(displayValue);
            }
            catch (Exception ex)
            {
                Log($"Error updating pulse time value: {ex.Message}");
            }
        }

        /// <summary>
        /// Validate that pulse parameters meet constraints
        /// </summary>
        private void ValidatePulseParameters()
        {
            if (!IsDeviceConnected()) return;

            try
            {
                // Get current period value
                double period = 0;
                if (double.TryParse(_pulsePeriodTextBox.Text, out double periodValue))
                {
                    string periodUnit = UnitConversionUtility.GetPeriodUnit(_pulsePeriodUnitComboBox);
                    period = periodValue * UnitConversionUtility.GetPeriodMultiplier(periodUnit);
                }
                else
                {
                    // If we can't parse the current period, query it from the device
                    period = Device.GetPulsePeriod(ActiveChannel);
                }

                // Get current width value
                double width = 0;
                if (double.TryParse(_pulseWidthTextBox.Text, out double widthValue))
                {
                    string widthUnit = UnitConversionUtility.GetPeriodUnit(_pulseWidthUnitComboBox);
                    width = widthValue * UnitConversionUtility.GetPeriodMultiplier(widthUnit);
                }
                else
                {
                    // If we can't parse the current width, query it from the device
                    width = Device.GetPulseWidth(ActiveChannel);
                }

                // Get rise and fall times
                double riseTime = 0;
                if (double.TryParse(_pulseRiseTimeTextBox.Text, out double riseTimeValue))
                {
                    string riseTimeUnit = UnitConversionUtility.GetPeriodUnit(_pulseRiseTimeUnitComboBox);
                    riseTime = riseTimeValue * UnitConversionUtility.GetPeriodMultiplier(riseTimeUnit);
                }
                else
                {
                    riseTime = Device.GetPulseRiseTime(ActiveChannel);
                }

                double fallTime = 0;
                if (double.TryParse(_pulseFallTimeTextBox.Text, out double fallTimeValue))
                {
                    string fallTimeUnit = UnitConversionUtility.GetPeriodUnit(_pulseFallTimeUnitComboBox);
                    fallTime = fallTimeValue * UnitConversionUtility.GetPeriodMultiplier(fallTimeUnit);
                }
                else
                {
                    fallTime = Device.GetPulseFallTime(ActiveChannel);
                }

                // Apply the DG2072 pulse width constraints
                // Based on manual: Width + 0.7 × (Rise + Fall) ≤ Period
                // Leaving 10% margin for safety
                double maxWidth = period - 0.7 * (riseTime + fallTime);
                maxWidth *= 0.9; // Add 10% safety margin

                // Ensure width is greater than minimum value (typically around 20ns)
                double minWidth = 20e-9; // 20ns typical minimum

                // If width is outside the valid range, adjust it
                if (width > maxWidth)
                {
                    width = maxWidth;

                    // Update UI with adjusted width
                    UpdatePulseWidthInUI(width);

                    Log($"Pulse width adjusted to maximum allowed value ({UnitConversionUtility.FormatWithMinimumDecimals(width * 1e6)} µs) based on current period and transition times");
                }
                else if (width < minWidth)
                {
                    width = minWidth;

                    // Update UI with adjusted width
                    UpdatePulseWidthInUI(width);

                    Log($"Pulse width adjusted to minimum allowed value ({UnitConversionUtility.FormatWithMinimumDecimals(width * 1e9)} ns)");
                }
            }
            catch (Exception ex)
            {
                Log($"Error validating pulse parameters: {ex.Message}");
            }
        }

        /// <summary>
        /// Helper to update pulse width in UI
        /// </summary>
        private void UpdatePulseWidthInUI(double widthInSeconds)
        {
            try
            {
                // Convert to picoseconds
                double psValue = widthInSeconds * 1e12;

                // Get current unit
                string currentUnit = UnitConversionUtility.GetPeriodUnit(_pulseWidthUnitComboBox);

                // Calculate display value
                double displayValue = UnitConversionUtility.ConvertFromPicoSeconds(psValue, currentUnit);

                // Update textbox with formatted value using UnitConversionUtility
                _pulseWidthTextBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(displayValue);
            }
            catch (Exception ex)
            {
                Log($"Error updating pulse width UI: {ex.Message}");
            }
        }

        /// <summary>
        /// Update the UI based on the selected pulse rate mode
        /// </summary>
        public void UpdatePulseRateMode()
        {
            if (!IsDeviceConnected()) return;

            // Toggle visibility of panels based on selected mode
            if (_frequencyModeActive)
            {
                // In Frequency mode, show frequency controls, hide period controls
                if (_pulseWidthDockPanel != null)
                    _pulseWidthDockPanel.Visibility = Visibility.Visible;
                if (_pulsePeriodDockPanel != null)
                    _pulsePeriodDockPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                // In Period mode, show period controls, hide frequency controls
                if (_pulseWidthDockPanel != null)
                    _pulseWidthDockPanel.Visibility = Visibility.Collapsed;
                if (_pulsePeriodDockPanel != null)
                    _pulsePeriodDockPanel.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Update visibilities for pulse-specific UI elements
        /// </summary>
        public void UpdatePulseControls(bool isPulseWaveform)
        {
            try
            {
                // First handle basic pulse control visibility
                if (_pulseWidthDockPanel != null)
                    _pulseWidthDockPanel.Visibility = isPulseWaveform ? Visibility.Visible : Visibility.Collapsed;

                if (_pulseRiseTimeDockPanel != null)
                    _pulseRiseTimeDockPanel.Visibility = isPulseWaveform ? Visibility.Visible : Visibility.Collapsed;

                if (_pulseFallTimeDockPanel != null)
                    _pulseFallTimeDockPanel.Visibility = isPulseWaveform ? Visibility.Visible : Visibility.Collapsed;

                if (_pulseRateModeDockPanel != null)
                    _pulseRateModeDockPanel.Visibility = isPulseWaveform ? Visibility.Visible : Visibility.Collapsed;

                // Then handle frequency/period mode appropriately
                if (isPulseWaveform)
                {
                    UpdatePulseRateMode();
                }
            }
            catch (Exception ex)
            {
                Log($"Error updating pulse control visibility: {ex.Message}");
            }
        }

        #endregion

        #region IPulseEventHandler Implementation

        // Implement all IPulseEventHandler methods here
        // These were already implemented in your original code

        public void OnPulsePeriodTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsDeviceConnected()) return;
            if (!double.TryParse(_pulsePeriodTextBox.Text, out double period)) return;

            // Use a timer to debounce rapid changes
            InitializeOrResetTimer(ref _pulsePeriodUpdateTimer, () => {
                if (double.TryParse(_pulsePeriodTextBox.Text, out double p))
                {
                    ApplyPulsePeriod(p);
                    // Update calculated frequency
                    UpdateCalculatedRateValue();
                }
            });
        }

        public void OnPulsePeriodLostFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (textBox == null || !double.TryParse(textBox.Text, out double _))
                return;

            AdjustPulseTimeAndUnit(textBox, _pulsePeriodUnitComboBox);
        }

        // Implement remaining IPulseEventHandler methods...
        // Include your existing implementations

        #endregion

        #region Parameter Application Methods

        /// <summary>
        /// Apply pulse period value to the device
        /// </summary>
        private void ApplyPulsePeriod(double period)
        {
            if (!IsDeviceConnected()) return;

            try
            {
                // Only use this direct period method in Period mode
                if (!_frequencyModeActive)
                {
                    string unit = UnitConversionUtility.GetPeriodUnit(_pulsePeriodUnitComboBox);
                    double actualPeriod = period * UnitConversionUtility.GetPeriodMultiplier(unit);

                    // Send period command directly
                    Device.SetPulsePeriod(ActiveChannel, actualPeriod);
                    Log($"Set CH{ActiveChannel} pulse period to {period} {unit} ({actualPeriod} s)");

                    // After changing period, we need to validate width
                    ValidatePulseParameters();

                    // Update frequency display but don't send to device
                    UpdateCalculatedRateValue();
                }
            }
            catch (Exception ex)
            {
                Log($"Error applying pulse period: {ex.Message}");
            }
        }

        /// <summary>
        /// Apply pulse width value to the device
        /// </summary>
        private void ApplyPulseWidth(double width)
        {
            if (!IsDeviceConnected()) return;

            try
            {
                string unit = UnitConversionUtility.GetPeriodUnit(_pulseWidthUnitComboBox);
                double actualWidth = width * UnitConversionUtility.GetPeriodMultiplier(unit);

                // Store the current period and transition times
                double period = Device.GetPulsePeriod(ActiveChannel);
                double riseTime = Device.GetPulseRiseTime(ActiveChannel);
                double fallTime = Device.GetPulseFallTime(ActiveChannel);

                // Calculate max allowed width
                double maxWidth = period - 0.7 * (riseTime + fallTime);
                maxWidth *= 0.9; // 10% safety margin

                // Ensure width is within allowed range
                if (actualWidth > maxWidth)
                {
                    actualWidth = maxWidth;
                    Log($"Pulse width limited to {UnitConversionUtility.FormatWithMinimumDecimals(actualWidth)} seconds based on current period and transition times");

                    // Update UI to show actual value
                    UpdatePulseWidthInUI(actualWidth);
                }

                // Apply the width
                Device.SetPulseWidth(ActiveChannel, actualWidth);
                Log($"Set CH{ActiveChannel} pulse width to {width} {unit} ({actualWidth} s)");
            }
            catch (Exception ex)
            {
                Log($"Error applying pulse width: {ex.Message}");
            }
        }

        /// <summary>
        /// Calculate and update the complementary value (frequency or period)
        /// </summary>
        public void UpdateCalculatedRateValue()
        {
            if (!IsDeviceConnected()) return;

            try
            {
                if (_frequencyModeActive)
                {
                    // Calculate period from frequency
                    if (double.TryParse(_frequencyTextBox.Text, out double frequency))
                    {
                        string freqUnit = UnitConversionUtility.GetFrequencyUnit(_frequencyUnitComboBox);
                        double freqInHz = frequency * UnitConversionUtility.GetFrequencyMultiplier(freqUnit);

                        if (freqInHz > 0)
                        {
                            double periodInSeconds = 1.0 / freqInHz;

                            // Choose appropriate unit for displaying the period
                            string periodUnit = UnitConversionUtility.GetPeriodUnit(_pulsePeriodUnitComboBox);
                            double displayValue = UnitConversionUtility.ConvertFromPicoSeconds(periodInSeconds * 1e12, periodUnit);

                            // Update the period TextBox with the calculated value
                            _pulsePeriodTextBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(displayValue);
                        }
                    }
                }
                else
                {
                    // Calculate frequency from period
                    if (double.TryParse(_pulsePeriodTextBox.Text, out double period))
                    {
                        string periodUnit = UnitConversionUtility.GetPeriodUnit(_pulsePeriodUnitComboBox);
                        double periodInSeconds = period * UnitConversionUtility.GetPeriodMultiplier(periodUnit);

                        if (periodInSeconds > 0)
                        {
                            double freqInHz = 1.0 / periodInSeconds;

                            // Choose appropriate unit for displaying the frequency
                            string freqUnit = UnitConversionUtility.GetFrequencyUnit(_frequencyUnitComboBox);
                            double displayValue = UnitConversionUtility.ConvertFromMicroHz(freqInHz * 1e6, freqUnit);

                            // Update the frequency TextBox with the calculated value
                            _frequencyTextBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(displayValue);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error updating calculated rate value: {ex.Message}");
            }
        }

        // Add the remaining helper methods from your original implementation
        // ...

        #endregion

        // For backward compatibility
        public void UpdatePulseParameters(int channel)
        {
            if (!IsDeviceConnected()) return;

            try
            {
                double width = Device.GetPulseWidth(channel);
                double riseTime = Device.GetPulseRiseTime(channel);
                double fallTime = Device.GetPulseFallTime(channel);

                // Update pulse width with appropriate unit
                UpdatePulseTimeValue(_pulseWidthTextBox, _pulseWidthUnitComboBox, width);

                // Update rise time with appropriate unit
                UpdatePulseTimeValue(_pulseRiseTimeTextBox, _pulseRiseTimeUnitComboBox, riseTime);

                // Update fall time with appropriate unit
                UpdatePulseTimeValue(_pulseFallTimeTextBox, _pulseFallTimeUnitComboBox, fallTime);
            }
            catch (Exception ex)
            {
                Log($"Error updating pulse parameters for channel {channel}: {ex.Message}");
            }
        }

        // Helper for timer initialization
        private void InitializeOrResetTimer(ref DispatcherTimer timer, Action timerAction)
        {
            if (timer == null)
            {
                timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };

                // Create a local copy of the action to avoid capturing the ref parameter
                DispatcherTimer localTimer = timer;
                Action action = timerAction;
                timer.Tick += (s, args) =>
                {
                    localTimer.Stop();
                    action();
                };
            }

            timer.Stop();
            timer.Start();
        }

        // Helper for time-unit adjustment
        public void AdjustPulseTimeAndUnit(TextBox textBox, ComboBox unitComboBox)
        {
            if (textBox == null || unitComboBox == null) return;
            if (!double.TryParse(textBox.Text, out double value)) return;

            try
            {
                string currentUnit = ((ComboBoxItem)unitComboBox.SelectedItem)?.Content.ToString();
                if (string.IsNullOrEmpty(currentUnit)) return;

                // Convert current value to picoseconds to maintain precision
                double psValue = UnitConversionUtility.ConvertToPicoSeconds(value, currentUnit);

                // Define units in order from smallest to largest
                string[] timeUnits = { "ps", "ns", "µs", "ms", "s" };

                // Map the combo box selection to our array index
                int unitIndex = 0;
                for (int i = 0; i < timeUnits.Length; i++)
                {
                    if (timeUnits[i] == currentUnit)
                    {
                        unitIndex = i;
                        break;
                    }
                }

                // Get the current value in the selected unit
                double displayValue = UnitConversionUtility.ConvertFromPicoSeconds(psValue, timeUnits[unitIndex]);

                // Handle values that are too large (> 9999)
                while (displayValue > 9999 && unitIndex < timeUnits.Length - 1)
                {
                    unitIndex++;
                    displayValue = UnitConversionUtility.ConvertFromPicoSeconds(psValue, timeUnits[unitIndex]);
                }

                // Handle values that are too small (< 0.1)
                while (displayValue < 0.1 && unitIndex > 0)
                {
                    unitIndex--;
                    displayValue = UnitConversionUtility.ConvertFromPicoSeconds(psValue, timeUnits[unitIndex]);
                }

                // Update the textbox with formatted value using UnitConversionUtility
                textBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(displayValue);

                // Find and select the unit in the combo box
                for (int i = 0; i < unitComboBox.Items.Count; i++)
                {
                    ComboBoxItem item = unitComboBox.Items[i] as ComboBoxItem;
                    if (item != null && item.Content.ToString() == timeUnits[unitIndex])
                    {
                        unitComboBox.SelectedIndex = i;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error adjusting pulse time and unit: {ex.Message}");
            }
        }

        // Additional IPulseEventHandler implementation methods as needed

        public void OnPulseRiseTimeTextChanged(object sender, TextChangedEventArgs e)
        {
            // Implementation
        }

        public void OnPulseRiseTimeLostFocus(object sender, RoutedEventArgs e)
        {
            // Implementation
        }

        public void OnPulseRiseTimeUnitChanged(object sender, SelectionChangedEventArgs e)
        {
            // Implementation
        }

        public void OnPulseWidthTextChanged(object sender, TextChangedEventArgs e)
        {
            // Implementation
        }

        public void OnPulseWidthLostFocus(object sender, RoutedEventArgs e)
        {
            // Implementation
        }

        public void OnPulseWidthUnitChanged(object sender, SelectionChangedEventArgs e)
        {
            // Implementation
        }

        public void OnPulseFallTimeTextChanged(object sender, TextChangedEventArgs e)
        {
            // Implementation
        }

        public void OnPulseFallTimeLostFocus(object sender, RoutedEventArgs e)
        {
            // Implementation
        }

        public void OnPulseFallTimeUnitChanged(object sender, SelectionChangedEventArgs e)
        {
            // Implementation
        }

        public void OnPulseRateModeToggleClicked(object sender, RoutedEventArgs e)
        {
            // Implementation
        }

        public void OnPulsePeriodUnitChanged(object sender, SelectionChangedEventArgs e)
        {
            // Implementation
        }

        // For UI management
        public void SetFrequencyMode(bool frequencyMode)
        {
            if (_frequencyModeActive != frequencyMode)
            {
                _frequencyModeActive = frequencyMode;

                // Update toggle button if it exists
                if (_pulseRateModeToggle != null)
                {
                    _pulseRateModeToggle.IsChecked = _frequencyModeActive;
                    _pulseRateModeToggle.Content = _frequencyModeActive ? "To Period" : "To Frequency";
                }

                // Update UI based on new mode
                UpdatePulseRateMode();
                UpdateCalculatedRateValue();
            }
        }

        // For impedance and other settings
        public void UpdateImpedanceSelection()
        {
            // Implementation as needed
        }
    }
}