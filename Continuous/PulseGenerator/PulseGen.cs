using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using DG2072_USB_Control.Services;

namespace DG2072_USB_Control.Continuous.PulseGenerator
{
    /// <summary>
    /// Manages pulse waveform functionality for the DG2072 device
    /// </summary>
    public class PulseGen : IPulseEventHandler
    {
        // Device reference
        private readonly RigolDG2072 _device;

        // Active channel
        private int _activeChannel;

        // UI elements
        private readonly TextBox _pulseWidthTextBox;
        private readonly TextBox _pulsePeriodTextBox;
        private readonly TextBox _pulseRiseTimeTextBox;
        private readonly TextBox _pulseFallTimeTextBox;
        private readonly ComboBox _pulseWidthUnitComboBox;
        private readonly ComboBox _pulsePeriodUnitComboBox;
        private readonly ComboBox _pulseRiseTimeUnitComboBox;
        private readonly ComboBox _pulseFallTimeUnitComboBox;
        private readonly ToggleButton _pulseRateModeToggle;

        // DockPanels for controlling visibility
        private readonly DockPanel _pulseWidthDockPanel;
        private readonly DockPanel _pulsePeriodDockPanel;
        private readonly DockPanel _pulseRiseTimeDockPanel;
        private readonly DockPanel _pulseFallTimeDockPanel;
        private readonly DockPanel _pulseRateModeDockPanel;

        // Frequency-related controls for calculated value
        private readonly TextBox _frequencyTextBox;
        private readonly ComboBox _frequencyUnitComboBox;

        // Update timers for debouncing
        private DispatcherTimer _pulseWidthUpdateTimer;
        private DispatcherTimer _pulsePeriodUpdateTimer;
        private DispatcherTimer _pulseRiseTimeUpdateTimer;
        private DispatcherTimer _pulseFallTimeUpdateTimer;

        // Mode flag
        private bool _frequencyModeActive = true;

        // Event for logging
        public event EventHandler<string> LogEvent;

        // Implement interface methods
        public void OnPulsePeriodTextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (textBox == null || !double.TryParse(textBox.Text, out double period))
                return;

            // Use your existing timer-based logic inside PulseGen
            if (!_frequencyModeActive)
            {
                InitializeOrResetTimer(ref _pulsePeriodUpdateTimer, () => {
                    if (double.TryParse(textBox.Text, out double p))
                    {
                        ApplyPulsePeriod(p);
                        // Update calculated frequency
                        UpdateCalculatedRateValue();
                    }
                });
            }
            // In frequency mode, just update the calculated value
            else if (_frequencyModeActive)
            {
                UpdateCalculatedRateValue();
            }
        }

        public void OnPulsePeriodLostFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (textBox == null || !double.TryParse(textBox.Text, out double _))
                return;

            AdjustPulseTimeAndUnit(textBox, _pulsePeriodUnitComboBox);
        }


        /// <summary>
        /// Constructor - initializes the pulse generator with device and UI references
        /// </summary>
        /// <param name="device">RigolDG2072 device</param>
        /// <param name="channel">Active channel (1 or 2)</param>
        /// <param name="mainWindow">Main window reference to find UI elements</param>
        public PulseGen(RigolDG2072 device, int channel, Window mainWindow)
        {
            _device = device;
            _activeChannel = channel;

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
        /// Property for the active channel
        /// </summary>
        public int ActiveChannel
        {
            get => _activeChannel;
            set => _activeChannel = value;
        }

        /// <summary>
        /// Get the frequency/period mode
        /// </summary>
        public bool FrequencyModeActive => _frequencyModeActive;

        /// <summary>
        /// Log helper method
        /// </summary>
        private void Log(string message)
        {
            LogEvent?.Invoke(this, message);
        }

        #region Event Handlers

        // Consolidated text changed event handlers using a common timer setup pattern
        private void PulseWidthTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsDeviceConnected()) return;
            if (!double.TryParse(_pulseWidthTextBox.Text, out double width)) return;

            InitializeOrResetTimer(ref _pulseWidthUpdateTimer, () => {
                if (double.TryParse(_pulseWidthTextBox.Text, out double w))
                {
                    ApplyPulseWidth(w);
                }
            });
        }

        private void PulsePeriodTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsDeviceConnected()) return;
            if (!double.TryParse(_pulsePeriodTextBox.Text, out double period)) return;

            // In period mode, update the device directly
            if (!_frequencyModeActive)
            {
                InitializeOrResetTimer(ref _pulsePeriodUpdateTimer, () => {
                    if (double.TryParse(_pulsePeriodTextBox.Text, out double p))
                    {
                        ApplyPulsePeriod(p);
                        // Update calculated frequency
                        UpdateCalculatedRateValue();
                    }
                });
            }
            // In frequency mode, just update the calculated value
            else if (_frequencyModeActive)
            {
                UpdateCalculatedRateValue();
            }
        }

        private void PulseRiseTimeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsDeviceConnected()) return;
            if (!double.TryParse(_pulseRiseTimeTextBox.Text, out double riseTime)) return;

            InitializeOrResetTimer(ref _pulseRiseTimeUpdateTimer, () => {
                if (double.TryParse(_pulseRiseTimeTextBox.Text, out double rt))
                {
                    ApplyPulseRiseTime(rt);
                }
            });
        }

        private void PulseFallTimeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsDeviceConnected()) return;
            if (!double.TryParse(_pulseFallTimeTextBox.Text, out double fallTime)) return;

            InitializeOrResetTimer(ref _pulseFallTimeUpdateTimer, () => {
                if (double.TryParse(_pulseFallTimeTextBox.Text, out double ft))
                {
                    ApplyPulseFallTime(ft);
                }
            });
        }

        // Helper method to consolidate timer initialization and reset
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

        // LostFocus event handlers
        private void PulseWidthTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(_pulseWidthTextBox.Text, out _))
            {
                AdjustPulseTimeAndUnit(_pulseWidthTextBox, _pulseWidthUnitComboBox);
            }
        }

        private void PulsePeriodTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(_pulsePeriodTextBox.Text, out _))
            {
                AdjustPulseTimeAndUnit(_pulsePeriodTextBox, _pulsePeriodUnitComboBox);
            }
        }

        private void PulseRiseTimeTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(_pulseRiseTimeTextBox.Text, out _))
            {
                AdjustPulseTimeAndUnit(_pulseRiseTimeTextBox, _pulseRiseTimeUnitComboBox);
            }
        }

        private void PulseFallTimeTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(_pulseFallTimeTextBox.Text, out _))
            {
                AdjustPulseTimeAndUnit(_pulseFallTimeTextBox, _pulseFallTimeUnitComboBox);
            }
        }

        // ComboBox SelectionChanged event handlers
        private void PulseWidthUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsDeviceConnected()) return;

            if (double.TryParse(_pulseWidthTextBox.Text, out double width))
            {
                ApplyPulseWidth(width);
            }
        }

        private void PulsePeriodUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsDeviceConnected()) return;

            if (double.TryParse(_pulsePeriodTextBox.Text, out double period))
            {
                ApplyPulsePeriod(period);
                // Update the calculated frequency when period unit changes
                if (!_frequencyModeActive)
                    UpdateCalculatedRateValue();
            }
        }

        private void PulseRiseTimeUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsDeviceConnected()) return;

            if (double.TryParse(_pulseRiseTimeTextBox.Text, out double riseTime))
            {
                ApplyPulseRiseTime(riseTime);
            }
        }

        private void PulseFallTimeUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsDeviceConnected()) return;

            if (double.TryParse(_pulseFallTimeTextBox.Text, out double fallTime))
            {
                ApplyPulseFallTime(fallTime);
            }
        }

        // Mode toggle event handler
        private void PulseRateModeToggle_Click(object sender, RoutedEventArgs e)
        {
            _frequencyModeActive = _pulseRateModeToggle.IsChecked == true;
            _pulseRateModeToggle.Content = _frequencyModeActive ? "To Period" : "To Frequency";

            // Update the UI based on the selected mode
            UpdatePulseRateMode();

            // Recalculate and update the displayed values
            UpdateCalculatedRateValue();
        }

        /// <summary>
        /// Check if the device is connected
        /// </summary>
        private bool IsDeviceConnected()
        {
            return _device != null && _device.IsConnected;
        }

        #endregion

        #region Parameter Application Methods

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
                double period = _device.GetPulsePeriod(_activeChannel);
                double riseTime = _device.GetPulseRiseTime(_activeChannel);
                double fallTime = _device.GetPulseFallTime(_activeChannel);

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
                _device.SetPulseWidth(_activeChannel, actualWidth);
                Log($"Set CH{_activeChannel} pulse width to {width} {unit} ({actualWidth} s)");
            }
            catch (Exception ex)
            {
                Log($"Error applying pulse width: {ex.Message}");
            }
        }

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
                    _device.SetPulsePeriod(_activeChannel, actualPeriod);
                    Log($"Set CH{_activeChannel} pulse period to {period} {unit} ({actualPeriod} s)");

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
        /// Apply pulse rise time value to the device
        /// </summary>
        private void ApplyPulseRiseTime(double riseTime)
        {
            if (!IsDeviceConnected()) return;

            try
            {
                string unit = UnitConversionUtility.GetPeriodUnit(_pulseRiseTimeUnitComboBox);
                double actualRiseTime = riseTime * UnitConversionUtility.GetPeriodMultiplier(unit);

                // Set the rise time
                _device.SetPulseRiseTime(_activeChannel, actualRiseTime);
                Log($"Set CH{_activeChannel} pulse rise time to {riseTime} {unit} ({actualRiseTime} s)");

                // After changing rise time, we may need to validate width
                ValidatePulseParameters();
            }
            catch (Exception ex)
            {
                Log($"Error applying pulse rise time: {ex.Message}");
            }
        }

        /// <summary>
        /// Apply pulse fall time value to the device
        /// </summary>
        private void ApplyPulseFallTime(double fallTime)
        {
            if (!IsDeviceConnected()) return;

            try
            {
                string unit = UnitConversionUtility.GetPeriodUnit(_pulseFallTimeUnitComboBox);
                double actualFallTime = fallTime * UnitConversionUtility.GetPeriodMultiplier(unit);

                // Set the fall time
                _device.SetPulseFallTime(_activeChannel, actualFallTime);
                Log($"Set CH{_activeChannel} pulse fall time to {fallTime} {unit} ({actualFallTime} s)");

                // After changing fall time, we may need to validate width
                ValidatePulseParameters();
            }
            catch (Exception ex)
            {
                Log($"Error applying pulse fall time: {ex.Message}");
            }
        }

        #endregion

        #region Validation and Calculation Methods

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
                    period = _device.GetPulsePeriod(_activeChannel);
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
                    width = _device.GetPulseWidth(_activeChannel);
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
                    riseTime = _device.GetPulseRiseTime(_activeChannel);
                }

                double fallTime = 0;
                if (double.TryParse(_pulseFallTimeTextBox.Text, out double fallTimeValue))
                {
                    string fallTimeUnit = UnitConversionUtility.GetPeriodUnit(_pulseFallTimeUnitComboBox);
                    fallTime = fallTimeValue * UnitConversionUtility.GetPeriodMultiplier(fallTimeUnit);
                }
                else
                {
                    fallTime = _device.GetPulseFallTime(_activeChannel);
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

        #endregion

        #region UI Mode & Calculation Methods

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

        #endregion

        #region Public Methods

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
        /// Helper method to adjust time values and units automatically
        /// </summary>
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

        /// <summary>
        /// Master method to apply all pulse parameters at once
        /// </summary>
        public void ApplyPulseParameters()
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
                        _device.SetFrequency(_activeChannel, actualFrequency);
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
                        _device.SetPulsePeriod(_activeChannel, actualPeriod);
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
                UpdatePulseParameters(_activeChannel);
                Log("All pulse parameters applied");
            }
            catch (Exception ex)
            {
                Log($"Error applying pulse parameters: {ex.Message}");
            }
        }

        /// <summary>
        /// Apply transition times (rise and fall)
        /// </summary>
        private void ApplyPulseTransitionTimes()
        {
            if (double.TryParse(_pulseRiseTimeTextBox.Text, out double riseTime))
            {
                string riseTimeUnit = UnitConversionUtility.GetPeriodUnit(_pulseRiseTimeUnitComboBox);
                double actualRiseTime = riseTime * UnitConversionUtility.GetPeriodMultiplier(riseTimeUnit);
                _device.SetPulseRiseTime(_activeChannel, actualRiseTime);
                Log($"Set pulse rise time to {riseTime} {riseTimeUnit} ({actualRiseTime} s)");
            }

            if (double.TryParse(_pulseFallTimeTextBox.Text, out double fallTime))
            {
                string fallTimeUnit = UnitConversionUtility.GetPeriodUnit(_pulseFallTimeUnitComboBox);
                double actualFallTime = fallTime * UnitConversionUtility.GetPeriodMultiplier(fallTimeUnit);
                _device.SetPulseFallTime(_activeChannel, actualFallTime);
                Log($"Set pulse fall time to {fallTime} {fallTimeUnit} ({actualFallTime} s)");
            }
        }

        /// <summary>
        /// Apply width constraints and validation
        /// </summary>
        private void ApplyPulseWidth()
        {
            // Validate parameters before applying width
            ValidatePulseParameters();

            // Apply the width (which must fit within the period)
            if (double.TryParse(_pulseWidthTextBox.Text, out double width))
            {
                string widthUnit = UnitConversionUtility.GetPeriodUnit(_pulseWidthUnitComboBox);
                double actualWidth = width * UnitConversionUtility.GetPeriodMultiplier(widthUnit);
                _device.SetPulseWidth(_activeChannel, actualWidth);
                Log($"Set pulse width to {width} {widthUnit} ({actualWidth} s)");
            }
        }

        /// <summary>
        /// Update all pulse parameters from device values
        /// </summary>
        public void UpdatePulseParameters(int channel)
        {
            try
            {
                double width = _device.GetPulseWidth(channel);
                double riseTime = _device.GetPulseRiseTime(channel);
                double fallTime = _device.GetPulseFallTime(channel);

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

        /// <summary>
        /// Set the frequency/period mode externally
        /// </summary>
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

        #endregion

        #region Period Methods



        public void OnPulsePeriodUnitChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsDeviceConnected()) return;

            if (double.TryParse(_pulsePeriodTextBox.Text, out double period))
            {
                ApplyPulsePeriod(period);
                // Update calculated frequency
                UpdateCalculatedRateValue();
            }
        }

        // Rate mode toggle
        public void OnPulseRateModeToggleClicked(object sender, RoutedEventArgs e)
        {
            _frequencyModeActive = _pulseRateModeToggle.IsChecked == true;
            _pulseRateModeToggle.Content = _frequencyModeActive ? "To Period" : "To Frequency";

            // Update the UI based on the selected mode
            UpdatePulseRateMode();

            // Recalculate and update the displayed values
            UpdateCalculatedRateValue();
        }

        // Rise Time methods
        public void OnPulseRiseTimeTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsDeviceConnected()) return;
            TextBox textBox = sender as TextBox;
            if (textBox == null || !double.TryParse(textBox.Text, out double riseTime)) return;

            InitializeOrResetTimer(ref _pulseRiseTimeUpdateTimer, () => {
                if (double.TryParse(textBox.Text, out double rt))
                {
                    ApplyPulseRiseTime(rt);
                }
            });
        }

        public void OnPulseRiseTimeLostFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (textBox != null && double.TryParse(textBox.Text, out double _))
            {
                AdjustPulseTimeAndUnit(textBox, _pulseRiseTimeUnitComboBox);
            }
        }

        public void OnPulseRiseTimeUnitChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsDeviceConnected()) return;

            if (double.TryParse(_pulseRiseTimeTextBox.Text, out double riseTime))
            {
                ApplyPulseRiseTime(riseTime);
            }
        }

        // Width methods
        public void OnPulseWidthTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsDeviceConnected()) return;
            TextBox textBox = sender as TextBox;
            if (textBox == null || !double.TryParse(textBox.Text, out double width)) return;

            InitializeOrResetTimer(ref _pulseWidthUpdateTimer, () => {
                if (double.TryParse(textBox.Text, out double w))
                {
                    ApplyPulseWidth(w);
                }
            });
        }

        public void OnPulseWidthLostFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (textBox != null && double.TryParse(textBox.Text, out double _))
            {
                AdjustPulseTimeAndUnit(textBox, _pulseWidthUnitComboBox);
            }
        }

        public void OnPulseWidthUnitChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsDeviceConnected()) return;

            if (double.TryParse(_pulseWidthTextBox.Text, out double width))
            {
                ApplyPulseWidth(width);
            }
        }

        // Fall Time methods
        public void OnPulseFallTimeTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsDeviceConnected()) return;
            TextBox textBox = sender as TextBox;
            if (textBox == null || !double.TryParse(textBox.Text, out double fallTime)) return;

            InitializeOrResetTimer(ref _pulseFallTimeUpdateTimer, () => {
                if (double.TryParse(textBox.Text, out double ft))
                {
                    ApplyPulseFallTime(ft);
                }
            });
        }

        public void OnPulseFallTimeLostFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (textBox != null && double.TryParse(textBox.Text, out double _))
            {
                AdjustPulseTimeAndUnit(textBox, _pulseFallTimeUnitComboBox);
            }
        }

        public void OnPulseFallTimeUnitChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsDeviceConnected()) return;

            if (double.TryParse(_pulseFallTimeTextBox.Text, out double fallTime))
            {
                ApplyPulseFallTime(fallTime);
            }
        }


        #endregion

    }
}
