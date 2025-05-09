using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using MathNet.Numerics;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using DG2072_USB_Control.Services;
using System.Windows.Media;
using System.Collections.Generic;
using static DG2072_USB_Control.RigolDG2072;
using DG2072_USB_Control.Continuous.Harmonics;
using System.Threading.Channels;


namespace DG2072_USB_Control
{
    public partial class MainWindow : System.Windows.Window
    {
        // VISA instrument handle
        private IntPtr instrumentHandle = IntPtr.Zero;
        private bool isConnected = false;
        private const string InstrumentAddress = "USB0::0x1AB1::0x0644::DG2P224100508::INSTR";
        private RigolDG2072 rigolDG2072;

        // Active channel tracking
        private int activeChannel = 1; // Default to Channel 1

        // Timer for auto-refresh feature
        private DispatcherTimer _autoRefreshTimer;
        private bool _autoRefreshEnabled = false;
        private CheckBox AutoRefreshCheckBox;

        // Update timers
        private DispatcherTimer _frequencyUpdateTimer;
        private DispatcherTimer _amplitudeUpdateTimer;
        private DispatcherTimer _offsetUpdateTimer;
        private DispatcherTimer _phaseUpdateTimer;
        private DispatcherTimer _symmetryUpdateTimer;
        private DockPanel SymmetryDockPanel;

        private DispatcherTimer _dutyCycleUpdateTimer;
        private DockPanel DutyCycleDockPanel;

        private DispatcherTimer _pulseWidthUpdateTimer;
        private DispatcherTimer _pulsePeriodUpdateTimer;
        private DispatcherTimer _pulseRiseTimeUpdateTimer;
        private DispatcherTimer _pulseFallTimeUpdateTimer;
        // Add this with the other timer declarations in MainWindow.xaml.cs:
        private DispatcherTimer _secondaryFrequencyUpdateTimer;

        // Add these fields to the MainWindow class
        private bool _frequencyModeActive = true; // Default to frequency mode
        private DockPanel PulsePeriodDockPanel;
        private DockPanel PhaseDockPanel;
        // Add this field to the MainWindow class
        private ChannelHarmonicController harmonicController;

        private double frequencyRatio = 2.0; // Default frequency ratio (harmonic)

        private DockPanel DCVoltageDockPanel;
        private DispatcherTimer _dcVoltageUpdateTimer;

        // Harmonics management
        private HarmonicsManager _harmonicsManager;
        private HarmonicsUIController _harmonicsUIController;

        public MainWindow()
        {
            InitializeComponent();

            // Initialize the device communication
            rigolDG2072 = new RigolDG2072();
            rigolDG2072.LogEvent += (s, message) => LogMessage(message);

            // Initialize ComboBoxes
            ChannelWaveformComboBox.SelectedIndex = 0;

            // Initialize auto-refresh feature
            InitializeAutoRefresh();
        }

        //**************** Regions

        #region Channel Toggle Methods

        private void ChannelToggleButton_Click(object sender, RoutedEventArgs e)
        {
            // Toggle between Channel 1 and Channel 2
            if (ChannelToggleButton.IsChecked == true)
            {
                // Switch to Channel 1
                activeChannel = 1;
                ChannelToggleButton.Content = "Channel 1";
                ActiveChannelTextBlock.Text = "Channel 1";
                ChannelControlsGroupBox.Header = "Channel 1 Controls";
                // Update harmonics controller with the new active channel
                //harmonicController = new ChannelHarmonicController(rigolDG2072, activeChannel);
                
                // Update harmonics manager with new active channel
                if (_harmonicsManager != null)
                    _harmonicsManager.SetActiveChannel(activeChannel);
            }
            else
            {
                // Switch to Channel 2
                activeChannel = 2;
                ChannelToggleButton.Content = "Channel 2";
                ActiveChannelTextBlock.Text = "Channel 2";
                ChannelControlsGroupBox.Header = "Channel 2 Controls";
                // Update harmonics controller with the new active channel
                //harmonicController = new ChannelHarmonicController(rigolDG2072, activeChannel);
                
                // Update harmonics manager with new active channel
                if (_harmonicsManager != null)
                    _harmonicsManager.SetActiveChannel(activeChannel);
            }



            // Refresh the UI to show current settings for the selected channel
            if (isConnected)
            {
                RefreshChannelSettings();
            }
        }

        private void ChannelPulseWidthTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(PulseWidth.Text, out _))
            {
                AdjustPulseTimeAndUnit(PulseWidth, PulseWidthUnitComboBox);
            }
        }

        private void ChannelPulsePeriodTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(PulsePeriod.Text, out _))
            {
                AdjustPulseTimeAndUnit(PulsePeriod, PulsePeriodUnitComboBox);
            }
        }

        private void ChannelPulseRiseTimeTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(PulseRiseTime.Text, out _))
            {
                AdjustPulseTimeAndUnit(PulseRiseTime, PulseRiseTimeUnitComboBox);
            }
        }

        private void ChannelPulseFallTimeTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(PulseFallTime.Text, out _))
            {
                AdjustPulseTimeAndUnit(PulseFallTime, PulseFallTimeUnitComboBox);
            }
        }

        private void ChannelPulseFallTimeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isConnected) return;
            if (!double.TryParse(PulseFallTime.Text, out double fallTime)) return;

            if (_pulseFallTimeUpdateTimer == null)
            {
                _pulseFallTimeUpdateTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                _pulseFallTimeUpdateTimer.Tick += (s, args) =>
                {
                    _pulseFallTimeUpdateTimer.Stop();
                    if (double.TryParse(PulseFallTime.Text, out double ft))
                    {
                        ApplyPulseFallTime(ft);
                    }
                };
            }

            _pulseFallTimeUpdateTimer.Stop();
            _pulseFallTimeUpdateTimer.Start();
        }

        private void RefreshChannelSettings()
        {
            try
            {
                // First update the waveform selection - this is critical to do first
                // since other settings depend on the waveform type
                UpdateWaveformSelection(ChannelWaveformComboBox, activeChannel);

                // Now get the currently selected waveform from the UI
                string waveform = ((ComboBoxItem)ChannelWaveformComboBox.SelectedItem).Content.ToString().ToUpper();
                LogMessage($"Refreshing settings for waveform: {waveform}");

                // Special handling for DC waveform
                if (waveform == "DC")
                {
                    // For DC, we only care about the DC voltage (which is the offset parameter)
                    // and the output impedance
                    double dcVoltage = rigolDG2072.GetDCVoltage(activeChannel);

                    // Update DC voltage text box
                    Dispatcher.Invoke(() =>
                    {
                        // Check current unit setting and adjust displayed value
                        string unit = ((ComboBoxItem)DCVoltageUnitComboBox.SelectedItem).Content.ToString();
                        if (unit == "mV")
                        {
                            DCVoltageTextBox.Text = FormatWithMinimumDecimals(dcVoltage * 1000);
                        }
                        else
                        {
                            DCVoltageTextBox.Text = FormatWithMinimumDecimals(dcVoltage);
                        }
                    });

                    // Get and update impedance setting
                    try
                    {
                        double impedance = rigolDG2072.GetOutputImpedance(activeChannel);

                        Dispatcher.Invoke(() =>
                        {
                            // Select the appropriate impedance in the combo box
                            if (double.IsInfinity(impedance))
                            {
                                // High-Z
                                for (int i = 0; i < DCImpedanceComboBox.Items.Count; i++)
                                {
                                    ComboBoxItem item = DCImpedanceComboBox.Items[i] as ComboBoxItem;
                                    if (item != null && item.Content.ToString() == "High-Z")
                                    {
                                        DCImpedanceComboBox.SelectedIndex = i;
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                // Find closest match
                                ComboBoxItem bestMatch = null;
                                double bestDifference = double.MaxValue;

                                foreach (ComboBoxItem item in DCImpedanceComboBox.Items)
                                {
                                    string content = item.Content.ToString();
                                    double itemImpedance = 0;

                                    if (content == "High-Z")
                                        continue;

                                    if (content.EndsWith("kΩ"))
                                    {
                                        if (double.TryParse(content.Replace("kΩ", ""), out double kOhms))
                                        {
                                            itemImpedance = kOhms * 1000;
                                        }
                                    }
                                    else if (content.EndsWith("Ω"))
                                    {
                                        if (double.TryParse(content.Replace("Ω", ""), out double ohms))
                                        {
                                            itemImpedance = ohms;
                                        }
                                    }

                                    double difference = Math.Abs(itemImpedance - impedance);
                                    if (difference < bestDifference)
                                    {
                                        bestDifference = difference;
                                        bestMatch = item;
                                    }
                                }

                                if (bestMatch != null)
                                {
                                    DCImpedanceComboBox.SelectedItem = bestMatch;
                                }
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Error refreshing impedance setting: {ex.Message}");
                    }

                    // Update output state
                    UpdateOutputState(ChannelOutputToggle, activeChannel);
                }
                else
                {
                    // Handle non-DC waveforms with existing code...

                    // Update the common parameters for all non-DC waveforms
                    // ...existing code...
                }

                LogMessage($"Refreshed Channel {activeChannel} settings");
            }
            catch (Exception ex)
            {
                LogMessage($"Error refreshing Channel {activeChannel} settings: {ex.Message}");
            }
        }

        private void UpdateDutyCycleValue(TextBox dutyCycleTextBox, int channel)
        {
            try
            {
                // Only update if the waveform is Square
                string currentWaveform = rigolDG2072.SendQuery($":SOUR{channel}:FUNC?").Trim().ToUpper();
                if (currentWaveform.Contains("SQU"))
                {
                    double dutyCycle = rigolDG2072.GetDutyCycle(channel);

                    Dispatcher.Invoke(() =>
                    {
                        dutyCycleTextBox.Text = FormatWithMinimumDecimals(dutyCycle);
                    });
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error updating duty cycle for channel {channel}: {ex.Message}");
            }
        }


        #endregion

        #region Auto-Refresh Methods

        private void SetupAutoRefresh()
        {
            // Create the auto-refresh timer
            _autoRefreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5) // Default to 5 seconds
            };

            _autoRefreshTimer.Tick += (s, e) =>
            {
                if (isConnected && _autoRefreshEnabled)
                {
                    RefreshInstrumentSettings();
                }
            };
        }

        private void InitializeAutoRefresh()
        {
            SetupAutoRefresh();

            // Create and add auto-refresh checkbox to the UI
            var autoRefreshCheckBox = new CheckBox
            {
                Content = "Auto-Refresh",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 0, 0),
                IsChecked = _autoRefreshEnabled,
                IsEnabled = false // Initially disabled until connected
            };

            autoRefreshCheckBox.Checked += (s, e) =>
            {
                _autoRefreshEnabled = true;
                _autoRefreshTimer.Start();
                LogMessage("Auto-refresh enabled");
            };

            autoRefreshCheckBox.Unchecked += (s, e) =>
            {
                _autoRefreshEnabled = false;
                _autoRefreshTimer.Stop();
                LogMessage("Auto-refresh disabled");
            };

            // Store reference to the checkbox
            AutoRefreshCheckBox = autoRefreshCheckBox;

            // Add to the connection status bar
            var connectionBar = ConnectionToggleButton.Parent as DockPanel;
            if (connectionBar != null)
            {
                connectionBar.Children.Insert(1, autoRefreshCheckBox);
            }
        }

        private void RefreshInstrumentSettings()
        {
            if (!isConnected) return;

            try
            {
                LogMessage("Refreshing all instrument settings...");

                // Refresh channel settings
                RefreshChannelSettings();

                // For waveform-specific settings
                string currentWaveform = ((ComboBoxItem)ChannelWaveformComboBox.SelectedItem).Content.ToString().ToUpper();

                // For USER waveform, refresh arbitrary settings
                if (currentWaveform == "USER")
                {
                    // Make sure the arbitrary waveform group is visible
                    ArbitraryWaveformGroupBox.Visibility = Visibility.Visible;
                }

                // For HARMONIC waveform, refresh harmonic settings
                else if (currentWaveform == "HARMONIC")
                {
                    // Initialize harmonicController if needed
                    if (harmonicController == null)
                    {
                        harmonicController = new ChannelHarmonicController(rigolDG2072, activeChannel);
                    }

                    // Refresh harmonic settings
                    RefreshHarmonicSettings();

                    // Make sure the harmonics group is visible
                    if (HarmonicsGroupBox != null)
                    {
                        HarmonicsGroupBox.Visibility = Visibility.Visible;
                    }
                }

                // For DUAL TONE waveform, refresh dual tone settings
                else if (currentWaveform == "DUAL TONE" || currentWaveform == "DUALTONE")
                {
                    // Initialize secondary frequency if needed
                    double primaryFreq = rigolDG2072.GetFrequency(activeChannel);
                    if (SecondaryFrequencyTextBox != null)
                    {
                        double secondaryFreq = primaryFreq * frequencyRatio;
                        SecondaryFrequencyTextBox.Text = FormatWithMinimumDecimals(secondaryFreq);
                    }

                    // Make sure the dual tone group is visible
                    if (DualToneGroupBox != null)
                    {
                        DualToneGroupBox.Visibility = Visibility.Visible;
                    }
                }

                // Make sure all waveform-specific controls have proper visibility
                UpdateWaveformSpecificControls(currentWaveform);

                LogMessage("Instrument settings refreshed successfully");
            }
            catch (Exception ex)
            {
                LogMessage($"Error refreshing instrument settings: {ex.Message}");
            }
        }

        private void UpdateAutoRefreshState(bool connected)
        {
            if (AutoRefreshCheckBox != null)
            {
                AutoRefreshCheckBox.IsEnabled = connected;

                if (!connected && _autoRefreshEnabled)
                {
                    _autoRefreshEnabled = false;
                    _autoRefreshTimer.Stop();
                    AutoRefreshCheckBox.IsChecked = false;
                }
            }
        }

        #endregion

        #region Instrument Settings Update Methods

        private void UpdatePeriodValue(TextBox periodTextBox, ComboBox unitComboBox, int channel)
        {
            try
            {
                // Get current waveform
                string currentWaveform = rigolDG2072.SendQuery($":SOUR{channel}:FUNC?").Trim().ToUpper();
                double period;

                // For pulse, use the specific method
                if (currentWaveform.Contains("PULS"))
                {
                    period = rigolDG2072.GetPulsePeriod(channel);
                }
                else
                {
                    // For other waveforms, query the period directly
                    string response = rigolDG2072.SendQuery($"SOURCE{channel}:PERiod?");
                    if (!double.TryParse(response, out period))
                    {
                        // If direct query fails, calculate from frequency
                        double frequency = rigolDG2072.GetFrequency(channel);
                        if (frequency > 0)
                        {
                            period = 1.0 / frequency;
                        }
                        else
                        {
                            period = 0.001; // Default 1ms
                        }
                    }
                }

                Dispatcher.Invoke(() =>
                {
                    // Store current unit to preserve it if possible
                    string currentUnit = Services.UnitConversionUtility.GetPeriodUnit(unitComboBox);

                    // Convert to picoseconds for internal representation
                    double psValue = period * 1e12; // Convert seconds to picoseconds

                    // Calculate the display value based on the current unit
                    double displayValue = Services.UnitConversionUtility.ConvertFromPicoSeconds(psValue, currentUnit);

                    // If the value would display poorly in the current unit, find a better unit
                    if (displayValue > 9999 || displayValue < 0.1)
                    {
                        string[] units = { "ps", "ns", "µs", "ms", "s" };
                        int bestUnitIndex = 2; // Default to µs

                        for (int i = 0; i < units.Length; i++)
                        {
                            double testValue = Services.UnitConversionUtility.ConvertFromPicoSeconds(psValue, units[i]);
                            if (testValue >= 0.1 && testValue < 10000)
                            {
                                bestUnitIndex = i;
                                break;
                            }
                        }

                        // Update the display value and select the best unit
                        displayValue = Services.UnitConversionUtility.ConvertFromPicoSeconds(psValue, units[bestUnitIndex]);

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

                    periodTextBox.Text = FormatWithMinimumDecimals(displayValue);
                });
            }
            catch (Exception ex)
            {
                LogMessage($"Error updating period for channel {channel}: {ex.Message}");
            }
        }

        private void UpdateOutputState(ToggleButton outputToggle, int channel)
        {
            string state = rigolDG2072.GetOutputState(channel);
            bool isOn = state.ToUpper().Contains("ON");

            Dispatcher.Invoke(() =>
            {
                outputToggle.IsChecked = isOn;
                outputToggle.Content = isOn ? "ON" : "OFF";
            });
        }

        private void UpdateSymmetryValue(TextBox symmetryTextBox, int channel)
        {
            try
            {
                // Only update if the waveform is Ramp
                string currentWaveform = rigolDG2072.SendQuery($":SOUR{channel}:FUNC?").Trim().ToUpper();
                if (currentWaveform.Contains("RAMP"))
                {
                    double symmetry = rigolDG2072.GetSymmetry(channel);

                    Dispatcher.Invoke(() =>
                    {
                        symmetryTextBox.Text = FormatWithMinimumDecimals(symmetry);
                    });
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error updating symmetry for channel {channel}: {ex.Message}");
            }
        }

        private void UpdateWaveformSelection(ComboBox waveformComboBox, int channel)
        {
            try
            {
                string currentWaveform = rigolDG2072.SendQuery($":SOUR{channel}:FUNC?").Trim().ToUpper();
                LogMessage($"Updating waveform selection - Device reports: {currentWaveform}");

                // Remove any SCPI response delimiters if present
                if (currentWaveform.StartsWith("\"") && currentWaveform.EndsWith("\""))
                {
                    currentWaveform = currentWaveform.Substring(1, currentWaveform.Length - 2);
                }

                // Map common abbreviations to full names
                if (currentWaveform == "SIN") currentWaveform = "SINE";
                if (currentWaveform == "SQU") currentWaveform = "SQUARE";
                if (currentWaveform == "PULS") currentWaveform = "PULSE";
                if (currentWaveform == "RAMP") currentWaveform = "RAMP";
                if (currentWaveform == "NOIS") currentWaveform = "NOISE";
                if (currentWaveform == "USER") currentWaveform = "USER";
                if (currentWaveform == "HARM") currentWaveform = "HARMONIC";
                if (currentWaveform == "DUAL") currentWaveform = "DUAL TONE";

                Dispatcher.Invoke(() =>
                {
                    bool found = false;

                    // First try exact match
                    foreach (ComboBoxItem item in waveformComboBox.Items)
                    {
                        if (item.Content.ToString().ToUpper() == currentWaveform)
                        {
                            waveformComboBox.SelectedItem = item;
                            found = true;
                            LogMessage($"Selected waveform: {item.Content}");
                            break;
                        }
                    }

                    // If not found, try with partial match
                    if (!found)
                    {
                        foreach (ComboBoxItem item in waveformComboBox.Items)
                        {
                            if (currentWaveform.Contains(item.Content.ToString().ToUpper()) ||
                                item.Content.ToString().ToUpper().Contains(currentWaveform))
                            {
                                waveformComboBox.SelectedItem = item;
                                LogMessage($"Selected waveform by partial match: {item.Content}");
                                found = true;
                                break;
                            }
                        }
                    }

                    // If still not found, log a warning
                    if (!found)
                    {
                        LogMessage($"Warning: Could not find matching waveform for '{currentWaveform}' in UI");
                        // Default to first item as a fallback
                        if (waveformComboBox.Items.Count > 0)
                        {
                            waveformComboBox.SelectedIndex = 0;
                            LogMessage($"Defaulted to {((ComboBoxItem)waveformComboBox.SelectedItem).Content}");
                        }
                    }

                    // Make sure to update waveform-specific controls based on selection
                    UpdateWaveformSpecificControls(((ComboBoxItem)waveformComboBox.SelectedItem).Content.ToString());
                });
            }
            catch (Exception ex)
            {
                LogMessage($"Error updating waveform selection for channel {channel}: {ex.Message}");
            }
        }

        private void UpdateFrequencyValue(TextBox freqTextBox, ComboBox unitComboBox, int channel)
        {
            try
            {
                double frequency = rigolDG2072.GetFrequency(channel);

                Dispatcher.Invoke(() =>
                {
                    // Store current unit to preserve it if possible
                    string currentUnit = UnitConversionUtility.GetFrequencyUnit(unitComboBox);

                    // Calculate the display value based on the current unit
                    double displayValue = UnitConversionUtility.ConvertFromMicroHz(frequency * 1e6, currentUnit);

                    // If the value would display poorly in the current unit, find a better unit
                    if (displayValue > 9999 || displayValue < 0.1)
                    {
                        string[] units = { "µHz", "mHz", "Hz", "kHz", "MHz" };
                        int bestUnitIndex = 2; // Default to Hz

                        for (int i = 0; i < units.Length; i++)
                        {
                            double testValue = UnitConversionUtility.ConvertFromMicroHz(frequency * 1e6, units[i]);
                            if (testValue >= 0.1 && testValue < 10000)
                            {
                                bestUnitIndex = i;
                                break;
                            }
                        }

                        // Update the display value and select the best unit
                        displayValue = UnitConversionUtility.ConvertFromMicroHz(frequency * 1e6, units[bestUnitIndex]);

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

                    freqTextBox.Text = FormatWithMinimumDecimals(displayValue);
                });
            }
            catch (Exception ex)
            {
                LogMessage($"Error updating frequency for channel {channel}: {ex.Message}");
            }
        }

        private void UpdateAmplitudeValue(TextBox ampTextBox, ComboBox unitComboBox, int channel)
        {
            try
            {
                double amplitude = rigolDG2072.GetAmplitude(channel);

                Dispatcher.Invoke(() =>
                {
                    // Store current unit to preserve it if possible
                    string currentUnit = UnitConversionUtility.GetAmplitudeUnit(unitComboBox);
                    bool isRms = currentUnit.Contains("rms");

                    // Calculate the scale factor based on the current unit
                    double scaleFactor = 1.0;
                    if (isRms)
                    {
                        // Convert Vpp to Vrms (for sine waves)
                        scaleFactor = 1.0 / (2.0 * Math.Sqrt(2.0));
                    }

                    // Apply milli prefix if needed
                    bool useMilliPrefix = currentUnit.StartsWith("m");
                    if (useMilliPrefix)
                    {
                        scaleFactor *= 1000.0;
                    }

                    double displayValue = amplitude * scaleFactor;

                    // If the value would display poorly in the current unit, find a better unit
                    if (displayValue > 9999 || displayValue < 0.1)
                    {
                        // Toggle between base unit and milli prefix
                        useMilliPrefix = !useMilliPrefix;

                        if (useMilliPrefix)
                        {
                            displayValue = amplitude * scaleFactor * 1000.0;
                        }
                        else
                        {
                            displayValue = amplitude * scaleFactor / 1000.0;
                        }

                        // Determine the new unit string
                        string newUnit;
                        if (isRms)
                        {
                            newUnit = useMilliPrefix ? "mVrms" : "Vrms";
                        }
                        else
                        {
                            newUnit = useMilliPrefix ? "mVpp" : "Vpp";
                        }

                        // Find and select the unit in the combo box
                        for (int i = 0; i < unitComboBox.Items.Count; i++)
                        {
                            ComboBoxItem item = unitComboBox.Items[i] as ComboBoxItem;
                            if (item != null && item.Content.ToString() == newUnit)
                            {
                                unitComboBox.SelectedIndex = i;
                                break;
                            }
                        }
                    }

                    ampTextBox.Text = FormatWithMinimumDecimals(displayValue);
                });
            }
            catch (Exception ex)
            {
                LogMessage($"Error updating amplitude for channel {channel}: {ex.Message}");
            }
        }

        private void UpdateOffsetValue(TextBox offsetTextBox, int channel)
        {
            try
            {
                double offset = rigolDG2072.GetOffset(channel);

                Dispatcher.Invoke(() =>
                {
                    offsetTextBox.Text = FormatWithMinimumDecimals(offset);
                });
            }
            catch (Exception ex)
            {
                LogMessage($"Error updating offset for channel {channel}: {ex.Message}");
            }
        }

        private void UpdatePhaseValue(TextBox phaseTextBox, int channel)
        {
            try
            {
                double phase = rigolDG2072.GetPhase(channel);

                Dispatcher.Invoke(() =>
                {
                    phaseTextBox.Text = FormatWithMinimumDecimals(phase);
                });
            }
            catch (Exception ex)
            {
                LogMessage($"Error updating phase for channel {channel}: {ex.Message}");
            }
        }

        #endregion

        #region Connection Methods

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (isConnected)
            {
                RefreshInstrumentSettings();
                LogMessage("Settings refreshed from instrument");
            }
            else
            {
                LogMessage("Cannot refresh settings: Instrument not connected");
            }
        }

        private bool Connect()
        {
            try
            {
                bool result = rigolDG2072.Connect();
                if (result)
                {
                    isConnected = true;
                    LogMessage("Connected to Rigol DG2072");

                    // Refresh all settings from the instrument
                    RefreshInstrumentSettings();
                }
                return result;
            }
            catch (Exception ex)
            {
                LogMessage($"Connection error: {ex.Message}");
                return false;
            }
        }

        private bool Disconnect()
        {
            try
            {
                bool result = rigolDG2072.Disconnect();
                if (result)
                {
                    isConnected = false;
                    LogMessage("Disconnected from Rigol DG2072");
                }
                return result;
            }
            catch (Exception ex)
            {
                LogMessage($"Disconnection error: {ex.Message}");
                return false;
            }
        }

        private void LogMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                CommandLogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
                CommandLogTextBox.ScrollToEnd();
            });
        }

        #endregion

        #region Event Handlers - Window and Connection

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Find and store reference to symmetry dock panel
            SymmetryDockPanel = FindVisualParent<DockPanel>(Symm);

            // Find and store reference to duty cycle dock panel
            DutyCycleDockPanel = FindVisualParent<DockPanel>(DutyCycle);

            // Find and store references to pulse panels
            PulseWidthDockPanel = FindVisualParent<DockPanel>(PulseWidth);

            // Since PulsePeriod is now inside FrequencyDockPanel, we set this to FrequencyDockPanel
            // This maintains compatibility with existing code that references PulsePeriodDockPanel
            PulsePeriodDockPanel = FindVisualParent<DockPanel>(ChannelFrequencyTextBox);

            PulseRiseTimeDockPanel = FindVisualParent<DockPanel>(PulseRiseTime);
            PulseFallTimeDockPanel = FindVisualParent<DockPanel>(PulseFallTime);

            // Get references to the main frequency panel and calculated rate panel
            FrequencyDockPanel = FindVisualParent<DockPanel>(ChannelFrequencyTextBox);

            // New: Find and store reference to phase panel
            PhaseDockPanel = FindVisualParent<DockPanel>(ChannelPhaseTextBox);

            // Initialize arbitrary waveform controls
            InitializeArbitraryWaveformControls();

            // Initialize frequency/period mode with frequency mode active by default
            _frequencyModeActive = true;
            FrequencyPeriodModeToggle.IsChecked = true;
            FrequencyPeriodModeToggle.Content = "To Period";

            // After window initialization, use a small delay before auto-connecting
            // This gives the UI time to fully render before connecting
            DispatcherTimer startupTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)  // 500ms delay
            };

            startupTimer.Tick += (s, args) =>
            {
                startupTimer.Stop();

                // Auto-connect only if not already connected
                if (!isConnected)
                {
                    LogMessage("Auto-connecting to instrument...");
                    // Call the connection method to establish connection
                    if (Connect())
                    {
                        // Update UI to reflect connected state
                        ConnectionStatusTextBlock.Text = "Connected";
                        ConnectionStatusTextBlock.Foreground = System.Windows.Media.Brushes.Green;
                        ConnectionToggleButton.Content = "Disconnect";
                        IdentifyButton.IsEnabled = true;
                        RefreshButton.IsEnabled = true;
                        UpdateAutoRefreshState(true);

                        // Refresh the UI with current device settings
                        RefreshInstrumentSettings();
                        LogMessage("Auto-connection successful");
                    }
                    else
                    {
                        LogMessage("Auto-connection failed - please connect manually");
                    }
                }
            };

            // In Window_Loaded method, add this to find and store the DC panel
            DCVoltageDockPanel = FindVisualParent<DockPanel>(DCVoltageTextBox);
            // Initialize harmonics management

            _harmonicsManager = new HarmonicsManager(rigolDG2072, activeChannel);
            _harmonicsManager.LogEvent += (s, message) => LogMessage(message);

            _harmonicsUIController = new HarmonicsUIController(_harmonicsManager, this);
            _harmonicsUIController.LogEvent += (s, message) => LogMessage(message);

            startupTimer.Start();
        }



        /// <summary>
        /// Event handler for the pulse rate mode toggle button
        /// <summary>
        private void PulseRateModeToggle_Click(object sender, RoutedEventArgs e)
        {
            _frequencyModeActive = PulseRateModeToggle.IsChecked == true;
            PulseRateModeToggle.Content = _frequencyModeActive ? "To Period" : "To Frequency";

            // Update the UI based on the selected mode
            UpdatePulseRateMode();

            // Recalculate and update the displayed values
            UpdateCalculatedRateValue();
        }

        // Rename the event handler to reflect its more general purpose
        private void FrequencyPeriodModeToggle_Click(object sender, RoutedEventArgs e)
        {
            _frequencyModeActive = FrequencyPeriodModeToggle.IsChecked == true;
            FrequencyPeriodModeToggle.Content = _frequencyModeActive ? "To Period" : "To Frequency";

            // Update the UI based on the selected mode
            UpdateFrequencyPeriodMode();

            // Recalculate and update the displayed values
            UpdateCalculatedRateValue();
        }


        /// <summary>
        /// Updates UI elements based on the selected pulse rate mode (frequency or period)
        /// </summary>
        private void UpdatePulseRateMode()
        {
            if (!isConnected) return;

            // Toggle visibility of panels based on selected mode
            if (_frequencyModeActive)
            {
                // In Frequency mode, show frequency controls, hide period controls
                FrequencyDockPanel.Visibility = Visibility.Visible;
                PeriodDockPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                // In Period mode, show period controls, hide frequency controls
                FrequencyDockPanel.Visibility = Visibility.Collapsed;
                PeriodDockPanel.Visibility = Visibility.Visible;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Disconnect on window closing
            if (isConnected)
            {
                LogMessage("Application closing - performing safe disconnect...");

                try
                {
                    // If we're in harmonic mode, disable it before disconnecting
                    // to ensure the device is left in a standard state
                    string currentWaveform = rigolDG2072.SendQuery($":SOUR{activeChannel}:FUNC?").Trim().ToUpper();
                    if (currentWaveform.Contains("HARM"))
                    {
                        LogMessage("Disabling harmonic mode before closing...");
                        rigolDG2072.SendCommand($":SOUR{activeChannel}:HARM:STAT OFF");
                        System.Threading.Thread.Sleep(50);
                    }


                    // Ensure harmonics are disabled when closing
                    if (isConnected && _harmonicsManager != null)
                    {

                        //string currentWaveform = rigolDG2072.SendQuery($":SOUR{activeChannel}:FUNC?").Trim().ToUpper();
                        string deviceWaveform = rigolDG2072.SendQuery($":SOUR{activeChannel}:FUNC?").Trim().ToUpper();
                        if (currentWaveform.Contains("HARM"))
                        {
                            LogMessage("Disabling harmonic mode before closing...");
                            _harmonicsManager.SetHarmonicState(false);
                            System.Threading.Thread.Sleep(50);
                        }
                    }


                    // Set the channel back to a standard waveform (sine) for safety
                    LogMessage("Setting device to standard sine wave state...");
                    rigolDG2072.SendCommand($":SOURCE{activeChannel}:APPLY:SIN 1000,1,0,0");
                    System.Threading.Thread.Sleep(50);

                    // Disconnect from the instrument
                    bool result = rigolDG2072.Disconnect();
                    if (result)
                    {
                        isConnected = false;
                        LogMessage("Successfully disconnected from Rigol DG2072");

                        // Send any additional commands needed to put the device back into local control
                        // Note: For most VISA devices, disconnecting naturally returns local control
                        // but if specific commands are needed, add them here
                    }
                    else
                    {
                        LogMessage("WARNING: Disconnect may not have completed successfully");
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"Error during disconnect: {ex.Message}");
                }

                // Final safety - if we have direct access to the VISA handle, ensure it's closed
                try
                {
                    if (instrumentHandle != IntPtr.Zero)
                    {
                        LogMessage("Closing VISA handle directly...");
                        // Assuming there's a close method in your VISA interface
                        // Add the appropriate code for your specific implementation
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"Error closing VISA handle: {ex.Message}");
                }
            }
        }

        // Helper method to find parent control
        private T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;

            T parent = parentObject as T;
            if (parent != null)
                return parent;
            else
                return FindVisualParent<T>(parentObject);
        }

        private void ConnectionToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isConnected)
            {
                // Try to connect
                if (Connect())
                {
                    ConnectionStatusTextBlock.Text = "Connected";
                    ConnectionStatusTextBlock.Foreground = System.Windows.Media.Brushes.Green;
                    ConnectionToggleButton.Content = "Disconnect";
                    IdentifyButton.IsEnabled = true;
                    RefreshButton.IsEnabled = true;
                    UpdateAutoRefreshState(true);
                }
            }
            else
            {
                // Try to disconnect
                if (Disconnect())
                {
                    ConnectionStatusTextBlock.Text = "Disconnected";
                    ConnectionStatusTextBlock.Foreground = System.Windows.Media.Brushes.Red;
                    ConnectionToggleButton.Content = "Connect";
                    IdentifyButton.IsEnabled = false;
                    RefreshButton.IsEnabled = false;
                    UpdateAutoRefreshState(false);
                }
            }
        }

        private void IdentifyButton_Click(object sender, RoutedEventArgs e)
        {
            string response = rigolDG2072.GetIdentification();
            if (!string.IsNullOrEmpty(response))
            {
                MessageBox.Show($"Instrument Identification:\n{response}", "Instrument Identification", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            CommandLogTextBox.Clear();
        }

        #endregion

        #region Channel Basic Controls Event Handlers

        private void ChannelOutputToggle_Click(object sender, RoutedEventArgs e)
        {
            if (ChannelOutputToggle.IsChecked == true)
            {
                rigolDG2072.SetOutput(activeChannel, true);
                ChannelOutputToggle.Content = "ON";
            }
            else
            {
                rigolDG2072.SetOutput(activeChannel, false);
                ChannelOutputToggle.Content = "OFF";
            }
        }

        // Add this method to MainWindow.xaml.cs to debug the dual tone issues
        private void DiagnoseDualToneSettings()
        {
            if (!isConnected) return;

            try
            {
                LogMessage("Starting dual tone diagnostics...");

                // Try to query the dual tone capabilities
                rigolDG2072.QueryDualToneCapabilities(activeChannel);

                // Try applying dual tone with different command patterns
                LogMessage("Testing direct apply approach...");
                rigolDG2072.SendCommand($"SOURce{activeChannel}:APPLy:SIN 1000,1.0,0.0,0.0");
                System.Threading.Thread.Sleep(100);

                rigolDG2072.SendCommand($"SOURce{activeChannel}:APPLy:DUALTone 1000,1.0,0.5,0.0");
                System.Threading.Thread.Sleep(100);

                // Verify what was applied
                rigolDG2072.QueryDualToneCapabilities(activeChannel);

                LogMessage("Dual tone diagnostics completed.");
            }
            catch (Exception ex)
            {
                LogMessage($"Error in dual tone diagnostics: {ex.Message}");
            }
        }

        private void ChannelWaveformComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isConnected) return;

            string waveform = ((ComboBoxItem)ChannelWaveformComboBox.SelectedItem).Content.ToString().ToUpper();
            string selectedArbWaveform = ((ComboBoxItem)ChannelWaveformComboBox.SelectedItem).Content.ToString();

            // Special handling for HARMONIC waveform
            if (waveform == "HARMONIC")
            {
                LogMessage("Switching to HARMONIC waveform mode...");

                try
                {
                    // First, set the waveform on the device - CRITICAL!
                    // We need to use SINE as the base waveform for harmonics
                    rigolDG2072.SendCommand($":SOURCE{activeChannel}:APPLY:SIN 1000,5,0,0");
                    System.Threading.Thread.Sleep(100);  // Give device time to process

                    // Then enable harmonic mode
                    rigolDG2072.SendCommand($":SOUR{activeChannel}:HARM:STAT ON");
                    System.Threading.Thread.Sleep(100);

                    // Initialize harmonicController if needed
                    if (harmonicController == null)
                    {
                        harmonicController = new ChannelHarmonicController(rigolDG2072, activeChannel);
                        LogMessage($"Initialized harmonic controller for Channel {activeChannel}");
                    }

                    // Get current parameters
                    double frequency = rigolDG2072.GetFrequency(activeChannel);
                    double amplitude = rigolDG2072.GetAmplitude(activeChannel);
                    double offset = rigolDG2072.GetOffset(activeChannel);
                    double phase = rigolDG2072.GetPhase(activeChannel);

                    // Reset all harmonic values to zero for a clean starting state
                    ResetHarmonicValues();

                    // Set harmonic toggle to ENABLED in UI
                    HarmonicsToggle.IsChecked = true;
                    HarmonicsToggle.Content = "ENABLED";

                    // Make sure harmonic UI elements are enabled
                    SetHarmonicUIElementsState(true);

                    // Verify the waveform was set correctly
                    string verifyWaveform = rigolDG2072.SendQuery($":SOUR{activeChannel}:FUNC?").Trim().ToUpper();
                    LogMessage($"Verification - Device waveform now: {verifyWaveform}");

                    // Verify harmonic state
                    string harmonicState = rigolDG2072.SendQuery($":SOUR{activeChannel}:HARM?").Trim().ToUpper();
                    LogMessage($"Verification - Harmonic state: {harmonicState}");

                    LogMessage("Ready for harmonic editing. Changes will be applied automatically.");
                }
                catch (Exception ex)
                {
                    LogMessage($"Error setting {waveform} mode: {ex.Message}");
                    MessageBox.Show($"Error setting {waveform} mode: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            // Special handling for DUAL TONE waveform
            else if (waveform == "DUAL TONE")
            {
                LogMessage("Switching to dual tone waveform mode...");
                try
                {
                    // Get current parameters
                    double frequency = rigolDG2072.GetFrequency(activeChannel);
                    double amplitude = rigolDG2072.GetAmplitude(activeChannel);
                    double offset = rigolDG2072.GetOffset(activeChannel);
                    double phase = rigolDG2072.GetPhase(activeChannel);

                    // Set secondary frequency based on primary frequency and ratio
                    if (SecondaryFrequencyTextBox != null)
                    {
                        double secondaryFreq = frequency * frequencyRatio;
                        SecondaryFrequencyTextBox.Text = FormatWithMinimumDecimals(secondaryFreq);
                    }

                    // Update UI for direct frequency mode by default
                    if (DirectFrequencyMode != null)
                    {
                        DirectFrequencyMode.IsChecked = true;
                    }

                    if (CenterOffsetMode != null)
                    {
                        CenterOffsetMode.IsChecked = false;
                    }

                    // First set sine wave as base, then change to dual tone
                    rigolDG2072.SendCommand($":SOURCE{activeChannel}:APPLY:SIN {frequency},{amplitude},{offset},{phase}");
                    System.Threading.Thread.Sleep(100);

                    // Set the waveform on the device
                    rigolDG2072.SendCommand($":SOURCE{activeChannel}:APPLY:DUAL {frequency},{amplitude},{offset},{phase}");
                    System.Threading.Thread.Sleep(100);

                    // Apply dual tone parameters 
                    ApplyDualToneParameters();

                    // Verify the waveform was set correctly
                    string verifyWaveform = rigolDG2072.SendQuery($":SOUR{activeChannel}:FUNC?").Trim().ToUpper();
                    LogMessage($"Verification - Device waveform now: {verifyWaveform}");

                    LogMessage("Dual tone mode ready. Adjust frequencies as needed.");
                }
                catch (Exception ex)
                {
                    LogMessage($"Error setting {waveform} mode: {ex.Message}");
                    MessageBox.Show($"Error setting {waveform} mode: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            // Special handling for USER (arbitrary) waveform
            else if (waveform == "USER")
            {
                LogMessage("Switching to arbitrary waveform mode...");
                try
                {
                    // Set the waveform on the device - make sure we send the command directly
                    rigolDG2072.SendCommand($":SOURCE{activeChannel}:APPLY:USER 1000,5,0,0");
                    System.Threading.Thread.Sleep(100);

                    // Initialize the arbitrary waveform UI
                    if (ArbitraryWaveformComboBox.SelectedItem == null)
                    {
                        ArbitraryWaveformComboBox.SelectedIndex = 0; // Select the first item (USER)
                    }

                    // Only access SelectedItem if it's not null and is the correct type
                    if (ArbitraryWaveformComboBox.SelectedItem != null && ArbitraryWaveformComboBox.SelectedItem is ComboBoxItem)
                    {
                        string selectedWaveformName = ((ComboBoxItem)ArbitraryWaveformComboBox.SelectedItem).Content.ToString();
                        LogMessage($"Arbitrary waveform mode ready. Selected: {selectedWaveformName}");
                    }
                    else
                    {
                        LogMessage("Arbitrary waveform mode ready. Select a waveform type and click Apply to set it.");
                    }

                    // Verify the waveform was set correctly
                    string verifyWaveform = rigolDG2072.SendQuery($":SOUR{activeChannel}:FUNC?").Trim().ToUpper();
                    LogMessage($"Verification - Device waveform now: {verifyWaveform}");
                }
                catch (Exception ex)
                {
                    LogMessage($"Error setting {waveform} mode: {ex.Message}");
                    MessageBox.Show($"Error setting {waveform} mode: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            // In the ChannelWaveformComboBox_SelectionChanged method, add special handling for NOISE
            // Update the existing "else" block that handles standard waveforms


            // Add this to the ChannelWaveformComboBox_SelectionChanged method right after the "USER" case
            else if (waveform == "DC")
            {
                LogMessage("Switching to DC waveform mode...");
                try
                {
                    // Get current offset as starting point for DC voltage
                    double offset = rigolDG2072.GetOffset(activeChannel);

                    // Update the DC Voltage textbox with the current offset value
                    DCVoltageTextBox.Text = FormatWithMinimumDecimals(offset);

                    // Set the waveform on the device - using APPLY:DC command
                    // According to the documentation, we need placeholders for frequency and amplitude
                    // even though they're not used in DC mode
                    rigolDG2072.SendCommand($":SOURCE{activeChannel}:APPLY:DC 1,1,{offset}");
                    System.Threading.Thread.Sleep(100);  // Give device time to process

                    // Verify the waveform was set correctly
                    string verifyWaveform = rigolDG2072.SendQuery($":SOUR{activeChannel}:FUNC?").Trim().ToUpper();
                    LogMessage($"Verification - Device waveform now: {verifyWaveform}");

                    // Get current impedance setting for display
                    string impedanceResponse = rigolDG2072.SendQuery($":OUTP{activeChannel}:IMP?").Trim();
                    double impedance = 50.0; // Default to 50 Ohms

                    // Try to parse the response, if it's "INF" or very large, it's High-Z
                    if (impedanceResponse.Contains("INF") || (double.TryParse(impedanceResponse, out double imp) && imp > 1e10))
                    {
                        // High-Z setting
                        DCImpedanceComboBox.SelectedIndex = 0; // Assuming the first index is "High-Z"
                    }
                    else if (double.TryParse(impedanceResponse, out impedance))
                    {
                        // Find closest impedance match in the combo box
                        if (impedance >= 1000)
                        {
                            // kOhm range
                            foreach (ComboBoxItem item in DCImpedanceComboBox.Items)
                            {
                                if (item.Content.ToString().Contains("k") &&
                                    double.TryParse(item.Content.ToString().Replace("kΩ", ""), out double value) &&
                                    Math.Abs(value * 1000 - impedance) < 10)
                                {
                                    DCImpedanceComboBox.SelectedItem = item;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            // Ohm range
                            foreach (ComboBoxItem item in DCImpedanceComboBox.Items)
                            {
                                if (item.Content.ToString().EndsWith("Ω") && !item.Content.ToString().Contains("k") &&
                                    double.TryParse(item.Content.ToString().Replace("Ω", ""), out double value) &&
                                    Math.Abs(value - impedance) < 5)
                                {
                                    DCImpedanceComboBox.SelectedItem = item;
                                    break;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"Error setting {waveform} mode: {ex.Message}");
                    MessageBox.Show($"Error setting {waveform} mode: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }





            else
            {
                // For all other waveforms, use the standard APPLY command to ensure it's set properly
                try
                {
                    // Get current parameters
                    double frequency = rigolDG2072.GetFrequency(activeChannel);
                    double amplitude = rigolDG2072.GetAmplitude(activeChannel);
                    double offset = rigolDG2072.GetOffset(activeChannel);
                    double phase = rigolDG2072.GetPhase(activeChannel);

                    // Map upper case waveform name to the correct apply command
                    string applyWaveform = waveform;
                    if (waveform == "SINE") applyWaveform = "SIN";
                    if (waveform == "SQUARE") applyWaveform = "SQU";
                    if (waveform == "PULSE") applyWaveform = "PULS";
                    if (waveform == "NOISE") applyWaveform = "NOIS";

                    // Special handling for NOISE waveform
                    if (waveform == "NOISE")
                    {
                        // NOISE waveform doesn't have frequency and phase parameters
                        rigolDG2072.SendCommand($":SOURCE{activeChannel}:APPLY:NOIS {amplitude},{offset}");
                        LogMessage($"Applied NOISE waveform with parameters: amp={amplitude}Vpp, offset={offset}V");
                    }
                    else
                    {
                        // For all other standard waveforms, use all parameters
                        rigolDG2072.SendCommand($":SOURCE{activeChannel}:APPLY:{applyWaveform} {frequency},{amplitude},{offset},{phase}");
                        LogMessage($"Applied {waveform} waveform with parameters: f={frequency}Hz, amp={amplitude}Vpp, offset={offset}V, phase={phase}°");
                    }

                    // Verify the waveform was set correctly
                    string verifyWaveform = rigolDG2072.SendQuery($":SOUR{activeChannel}:FUNC?").Trim().ToUpper();
                    LogMessage($"Verification - Device waveform now: {verifyWaveform}");
                }
                catch (Exception ex)
                {
                    LogMessage($"Error applying waveform {waveform}: {ex.Message}");
                    // Fallback to basic method if apply command fails
                    rigolDG2072.SetWaveform(activeChannel, waveform);
                }
            }

            // Update waveform-specific UI elements visibility
            UpdateWaveformSpecificControls(waveform);
        }

        private void PulsePeriodTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isConnected) return;
            if (!double.TryParse(PulsePeriod.Text, out double period)) return;

            // In period mode, update the device directly
            if (!_frequencyModeActive)
            {
                if (_pulsePeriodUpdateTimer == null)
                {
                    _pulsePeriodUpdateTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(500)
                    };
                    _pulsePeriodUpdateTimer.Tick += (s, args) =>
                    {
                        _pulsePeriodUpdateTimer.Stop();
                        if (double.TryParse(PulsePeriod.Text, out double p))
                        {
                            // Apply the period value based on waveform type
                            string waveform = ((ComboBoxItem)ChannelWaveformComboBox.SelectedItem).Content.ToString().ToUpper();
                            if (waveform == "PULSE")
                            {
                                ApplyPulsePeriod(p);
                            }
                            else
                            {
                                ApplyPeriod(p);
                            }

                            // Update calculated frequency
                            UpdateCalculatedRateValue();
                        }
                    };
                }

                _pulsePeriodUpdateTimer.Stop();
                _pulsePeriodUpdateTimer.Start();
            }
            // In frequency mode, just update the calculated value
            else if (_frequencyModeActive)
            {
                UpdateCalculatedRateValue();
            }
        }

        // Add event handlers for the dual tone controls
        private void SecondaryFrequencyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isConnected) return;
            if (!double.TryParse(SecondaryFrequencyTextBox.Text, out double frequency)) return;

            // Use a timer similar to primary frequency
            if (_secondaryFrequencyUpdateTimer == null)
            {
                _secondaryFrequencyUpdateTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                _secondaryFrequencyUpdateTimer.Tick += (s, args) =>
                {
                    _secondaryFrequencyUpdateTimer.Stop();
                    if (double.TryParse(SecondaryFrequencyTextBox.Text, out double freq))
                    {
                        ApplyDualToneParameters();
                    }
                };
            }

            _secondaryFrequencyUpdateTimer.Stop();
            _secondaryFrequencyUpdateTimer.Start();
        }

        private void SynchronizeFrequenciesCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!isConnected) return;

            bool isSynchronized = SynchronizeFrequenciesCheckBox.IsChecked == true;
            SecondaryFrequencyDockPanel.IsEnabled = !isSynchronized;
            FrequencyRatioComboBox.IsEnabled = isSynchronized;

            if (isSynchronized && double.TryParse(ChannelFrequencyTextBox.Text, out double primaryFreq))
            {
                // Update secondary frequency based on the ratio
                double frequencyRatio = 2.0; // Default
                if (FrequencyRatioComboBox.SelectedItem is ComboBoxItem selectedItem &&
                    double.TryParse(selectedItem.Content.ToString(), out double ratio))
                {
                    frequencyRatio = ratio;
                }

                double secondaryFreq = primaryFreq * frequencyRatio;
                SecondaryFrequencyTextBox.Text = FormatWithMinimumDecimals(secondaryFreq);

                // Apply the changes immediately
                ApplyDualToneParameters();
            }
        }

        private void FrequencyRatioComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isConnected) return;

            ComboBox ratioComboBox = sender as ComboBox;
            if (ratioComboBox != null && ratioComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string ratioText = selectedItem.Content.ToString();
                if (double.TryParse(ratioText, out double ratio))
                {
                    frequencyRatio = ratio;

                    // If synchronized is checked, update the secondary frequency
                    if (SynchronizeFrequenciesCheckBox.IsChecked == true &&
                        double.TryParse(ChannelFrequencyTextBox.Text, out double primaryFreq))
                    {
                        double secondaryFreq = primaryFreq * frequencyRatio;
                        SecondaryFrequencyTextBox.Text = FormatWithMinimumDecimals(secondaryFreq);
                    }
                }
            }
        }

        // Modify these methods to update the calculated value too
        private void ChannelFrequencyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isConnected) return;
            if (!double.TryParse(ChannelFrequencyTextBox.Text, out double frequency)) return;

            // Don't update the instrument for every keystroke
            // Instead, use a timer to delay the update
            if (_frequencyUpdateTimer == null)
            {
                _frequencyUpdateTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                _frequencyUpdateTimer.Tick += (s, args) =>
                {
                    _frequencyUpdateTimer.Stop();
                    if (double.TryParse(ChannelFrequencyTextBox.Text, out double freq))
                    {
                        ApplyFrequency(freq);

                        // Add this line to update the period when frequency changes
                        if (_frequencyModeActive)
                        {
                            string currentWaveform = ((ComboBoxItem)ChannelWaveformComboBox.SelectedItem).Content.ToString().ToUpper();
                            if (currentWaveform == "PULSE")
                                UpdateCalculatedRateValue();
                        }
                    }
                };
            }

            _frequencyUpdateTimer.Stop();
            _frequencyUpdateTimer.Start();

            // PART 7: Add to ChannelFrequencyTextBox_TextChanged handler (to keep dual tone frequencies in sync)
            // Add this at the end of the existing method

            if (isConnected &&
                ((ComboBoxItem)ChannelWaveformComboBox.SelectedItem).Content.ToString().ToUpper() == "DUAL TONE" &&
                SynchronizeFrequenciesCheckBox.IsChecked == true &&
                double.TryParse(ChannelFrequencyTextBox.Text, out double primaryFreq))
            {
                // Update secondary frequency to maintain the ratio
                double secondaryFreq = primaryFreq * frequencyRatio;
                SecondaryFrequencyTextBox.Text = FormatWithMinimumDecimals(secondaryFreq);
            }


        }

        private void ChannelPulsePeriodTextBox_TextChanged(object sender, TextChangedEventArgs e)

        {
            if (!isConnected) return;
            if (!double.TryParse(PulsePeriod.Text, out double period)) return;

            if (_pulsePeriodUpdateTimer == null)
            {
                _pulsePeriodUpdateTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                _pulsePeriodUpdateTimer.Tick += (s, args) =>
                {
                    _pulsePeriodUpdateTimer.Stop();
                    if (double.TryParse(PulsePeriod.Text, out double p))
                    {
                        ApplyPulsePeriod(p);
                        // Add this line to update the frequency when period changes
                        if (!_frequencyModeActive)
                            UpdateCalculatedRateValue();
                    }
                };
            }

            _pulsePeriodUpdateTimer.Stop();
            _pulsePeriodUpdateTimer.Start();
        }

        private void ChannelAmplitudeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isConnected) return;
            if (!double.TryParse(ChannelAmplitudeTextBox.Text, out double amplitude)) return;

            if (_amplitudeUpdateTimer == null)
            {
                _amplitudeUpdateTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                _amplitudeUpdateTimer.Tick += (s, args) =>
                {
                    _amplitudeUpdateTimer.Stop();
                    if (double.TryParse(ChannelAmplitudeTextBox.Text, out double amp))
                    {
                        ApplyAmplitude(amp);
                    }
                };
            }

            _amplitudeUpdateTimer.Stop();
            _amplitudeUpdateTimer.Start();
        }

        private void ChannelOffsetTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isConnected) return;
            if (!double.TryParse(ChannelOffsetTextBox.Text, out double offset)) return;

            if (_offsetUpdateTimer == null)
            {
                _offsetUpdateTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                _offsetUpdateTimer.Tick += (s, args) =>
                {
                    _offsetUpdateTimer.Stop();
                    if (double.TryParse(ChannelOffsetTextBox.Text, out double off))
                    {
                        rigolDG2072.SetOffset(activeChannel, off);
                    }
                };
            }

            _offsetUpdateTimer.Stop();
            _offsetUpdateTimer.Start();
        }

        private void ChannelPhaseTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isConnected) return;
            if (!double.TryParse(ChannelPhaseTextBox.Text, out double phase)) return;

            if (_phaseUpdateTimer == null)
            {
                _phaseUpdateTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                _phaseUpdateTimer.Tick += (s, args) =>
                {
                    _phaseUpdateTimer.Stop();
                    if (double.TryParse(ChannelPhaseTextBox.Text, out double ph))
                    {
                        rigolDG2072.SetPhase(activeChannel, ph);
                    }
                };
            }

            _phaseUpdateTimer.Stop();
            _phaseUpdateTimer.Start();
        }

        private void ChannelSymmetryTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isConnected) return;
            if (!double.TryParse(Symm.Text, out double symmetry)) return;

            if (_symmetryUpdateTimer == null)
            {
                _symmetryUpdateTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                _symmetryUpdateTimer.Tick += (s, args) =>
                {
                    _symmetryUpdateTimer.Stop();
                    if (double.TryParse(Symm.Text, out double sym))
                    {
                        rigolDG2072.SetSymmetry(activeChannel, sym);
                    }
                };
            }

            _symmetryUpdateTimer.Stop();
            _symmetryUpdateTimer.Start();
        }

        private void ChannelSymmetryTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(Symm.Text, out double symmetry))
            {
                // Ensure value is in valid range
                symmetry = Math.Max(0, Math.Min(100, symmetry));
                Symm.Text = FormatWithMinimumDecimals(symmetry);
            }
        }

        private void ChannelApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isConnected) return;

            try
            {
                // First adjust the values and units for better display
                if (_frequencyModeActive)
                {
                    AdjustFrequencyAndUnit(ChannelFrequencyTextBox, ChannelFrequencyUnitComboBox);
                }
                else
                {
                    AdjustPulseTimeAndUnit(PulsePeriod, PulsePeriodUnitComboBox);
                }

                AdjustAmplitudeAndUnit(ChannelAmplitudeTextBox, ChannelAmplitudeUnitComboBox);

                // Get current waveform type
                string waveform = ((ComboBoxItem)ChannelWaveformComboBox.SelectedItem).Content.ToString().ToUpper();

                // For pulse waveform parameters, adjust if needed
                if (waveform == "PULSE")
                {
                    AdjustPulseTimeAndUnit(PulseWidth, PulseWidthUnitComboBox);
                    AdjustPulseTimeAndUnit(PulseRiseTime, PulseRiseTimeUnitComboBox);
                    AdjustPulseTimeAndUnit(PulseFallTime, PulseFallTimeUnitComboBox);
                }

                // Apply based on mode
                if (_frequencyModeActive)
                {
                    // Check if frequency value is valid
                    if (!double.TryParse(ChannelFrequencyTextBox.Text, out double frequency))
                    {
                        LogMessage($"Invalid frequency value for CH{activeChannel}");
                        return;
                    }

                    // Convert frequency unit
                    string freqUnit = Services.UnitConversionUtility.GetFrequencyUnit(ChannelFrequencyUnitComboBox);
                    double freqMultiplier = Services.UnitConversionUtility.GetFrequencyMultiplier(freqUnit);
                    double actualFrequency = frequency * freqMultiplier;

                    if (waveform == "PULSE")
                    {
                        // For pulse, use special handling
                        rigolDG2072.SetFrequency(activeChannel, actualFrequency);
                        ApplyPulseParameters();
                    }
                    else
                    {
                        // For other waveforms, apply directly with frequency
                        rigolDG2072.SetFrequency(activeChannel, actualFrequency);
                    }
                }
                else
                {
                    // Check if period value is valid
                    if (!double.TryParse(PulsePeriod.Text, out double period))
                    {
                        LogMessage($"Invalid period value for CH{activeChannel}");
                        return;
                    }

                    // Convert period unit
                    string periodUnit = Services.UnitConversionUtility.GetPeriodUnit(PulsePeriodUnitComboBox);
                    double periodMultiplier = Services.UnitConversionUtility.GetPeriodMultiplier(periodUnit);
                    double actualPeriod = period * periodMultiplier;

                    if (waveform == "PULSE")
                    {
                        // For pulse, use special handling
                        rigolDG2072.SetPulsePeriod(activeChannel, actualPeriod);
                        ApplyPulseParameters();
                    }
                    else
                    {
                        // For other waveforms, apply directly with period
                        rigolDG2072.SendCommand($"SOURCE{activeChannel}:PERiod {actualPeriod}");
                    }
                }

                // Apply other parameters (amplitude, offset, phase)
                if (double.TryParse(ChannelAmplitudeTextBox.Text, out double amplitude) &&
                    double.TryParse(ChannelOffsetTextBox.Text, out double offset) &&
                    double.TryParse(ChannelPhaseTextBox.Text, out double phase))
                {
                    string ampUnit = Services.UnitConversionUtility.GetAmplitudeUnit(ChannelAmplitudeUnitComboBox);
                    double ampMultiplier = Services.UnitConversionUtility.GetAmplitudeMultiplier(ampUnit);
                    double actualAmplitude = amplitude * ampMultiplier;

                    rigolDG2072.SetAmplitude(activeChannel, actualAmplitude);
                    rigolDG2072.SetOffset(activeChannel, offset);
                    rigolDG2072.SetPhase(activeChannel, phase);
                }

                // Apply waveform-specific parameters
                if (waveform == "RAMP" && double.TryParse(Symm.Text, out double symmetry))
                {
                    rigolDG2072.SetSymmetry(activeChannel, symmetry);
                    LogMessage($"Applied symmetry {symmetry}% to CH{activeChannel}");
                }
                else if (waveform == "SQUARE" && double.TryParse(DutyCycle.Text, out double dutyCycle))
                {
                    rigolDG2072.SetDutyCycle(activeChannel, dutyCycle);
                    LogMessage($"Applied duty cycle {dutyCycle}% to CH{activeChannel}");
                }

                // Refresh the UI to show the actual values from the device
                RefreshChannelSettings();
            }
            catch (Exception ex)
            {
                LogMessage($"Error applying settings: {ex.Message}");
                MessageBox.Show($"Error applying settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Pulse Parameter Handling


        private void ChannelPulseWidthTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isConnected) return;
            if (!double.TryParse(PulseWidth.Text, out double width)) return;

            if (_pulseWidthUpdateTimer == null)
            {
                _pulseWidthUpdateTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                _pulseWidthUpdateTimer.Tick += (s, args) =>
                {
                    _pulseWidthUpdateTimer.Stop();
                    if (double.TryParse(PulseWidth.Text, out double w))
                    {
                        ApplyPulseWidth(w);
                    }
                };
            }

            _pulseWidthUpdateTimer.Stop();
            _pulseWidthUpdateTimer.Start();
        }

        private void ChannelPulseRiseTimeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isConnected) return;
            if (!double.TryParse(PulseRiseTime.Text, out double riseTime)) return;

            if (_pulseRiseTimeUpdateTimer == null)
            {
                _pulseRiseTimeUpdateTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                _pulseRiseTimeUpdateTimer.Tick += (s, args) =>
                {
                    _pulseRiseTimeUpdateTimer.Stop();
                    if (double.TryParse(PulseRiseTime.Text, out double rt))
                    {
                        ApplyPulseRiseTime(rt);
                    }
                };
            }

            _pulseRiseTimeUpdateTimer.Stop();
            _pulseRiseTimeUpdateTimer.Start();
        }


        private void PulseWidthUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isConnected) return;

            if (double.TryParse(PulseWidth.Text, out double width))
            {
                ApplyPulseWidth(width);
            }
        }

        private void PulsePeriodUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isConnected) return;

            if (double.TryParse(PulsePeriod.Text, out double period))
            {
                ApplyPulsePeriod(period);
                // Update the calculated frequency when period unit changes
                if (!_frequencyModeActive)
                    UpdateCalculatedRateValue();
            }
        }

        private void PulseRiseTimeUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isConnected) return;

            if (double.TryParse(PulseRiseTime.Text, out double riseTime))
            {
                ApplyPulseRiseTime(riseTime);
            }
        }

        private void PulseFallTimeUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isConnected) return;

            if (double.TryParse(PulseFallTime.Text, out double fallTime))
            {
                ApplyPulseFallTime(fallTime);
            }
        }

        /// <summary>
        /// Validates that the pulse width is within the allowed range based on other parameters
        /// </summary>
        private void ValidatePulseParameters()
        {
            if (!isConnected) return;

            try
            {
                // Get current period value
                double period = 0;
                if (double.TryParse(PulsePeriod.Text, out double periodValue))
                {
                    string periodUnit = Services.UnitConversionUtility.GetPeriodUnit(PulsePeriodUnitComboBox);
                    period = periodValue * Services.UnitConversionUtility.GetPeriodMultiplier(periodUnit);
                }
                else
                {
                    // If we can't parse the current period, query it from the device
                    period = rigolDG2072.GetPulsePeriod(activeChannel);
                }

                // Get current width value
                double width = 0;
                if (double.TryParse(PulseWidth.Text, out double widthValue))
                {
                    string widthUnit = Services.UnitConversionUtility.GetPeriodUnit(PulseWidthUnitComboBox);
                    width = widthValue * Services.UnitConversionUtility.GetPeriodMultiplier(widthUnit);
                }
                else
                {
                    // If we can't parse the current width, query it from the device
                    width = rigolDG2072.GetPulseWidth(activeChannel);
                }

                // Get rise and fall times
                double riseTime = 0;
                if (double.TryParse(PulseRiseTime.Text, out double riseTimeValue))
                {
                    string riseTimeUnit = Services.UnitConversionUtility.GetPeriodUnit(PulseRiseTimeUnitComboBox);
                    riseTime = riseTimeValue * Services.UnitConversionUtility.GetPeriodMultiplier(riseTimeUnit);
                }
                else
                {
                    riseTime = rigolDG2072.GetPulseRiseTime(activeChannel);
                }

                double fallTime = 0;
                if (double.TryParse(PulseFallTime.Text, out double fallTimeValue))
                {
                    string fallTimeUnit = Services.UnitConversionUtility.GetPeriodUnit(PulseFallTimeUnitComboBox);
                    fallTime = fallTimeValue * Services.UnitConversionUtility.GetPeriodMultiplier(fallTimeUnit);
                }
                else
                {
                    fallTime = rigolDG2072.GetPulseFallTime(activeChannel);
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
                    Dispatcher.Invoke(() =>
                    {
                        UpdatePulseWidthInUI(width);
                    });

                    LogMessage($"Pulse width adjusted to maximum allowed value ({Services.UnitConversionUtility.FormatWithMinimumDecimals(width * 1e6)} µs) based on current period and transition times");
                }
                else if (width < minWidth)
                {
                    width = minWidth;

                    // Update UI with adjusted width
                    Dispatcher.Invoke(() =>
                    {
                        UpdatePulseWidthInUI(width);
                    });

                    LogMessage($"Pulse width adjusted to minimum allowed value ({Services.UnitConversionUtility.FormatWithMinimumDecimals(width * 1e9)} ns)");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error validating pulse parameters: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the pulse width display in the UI with the appropriate units
        /// </summary>
        private void UpdatePulseWidthInUI(double widthInSeconds)
        {
            try
            {
                // Convert to picoseconds
                double psValue = widthInSeconds * 1e12;

                // Get current unit
                string currentUnit = Services.UnitConversionUtility.GetPeriodUnit(PulseWidthUnitComboBox);

                // Calculate display value
                double displayValue = Services.UnitConversionUtility.ConvertFromPicoSeconds(psValue, currentUnit);

                // Update textbox
                PulseWidth.Text = FormatWithMinimumDecimals(displayValue);
            }
            catch (Exception ex)
            {
                LogMessage($"Error updating pulse width UI: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies pulse parameters to the device in the correct order to ensure constraints are met
        /// </summary>
        // Method to apply pulse settings based on mode
        private void ApplyPulseParameters()
        {
            if (!isConnected) return;

            try
            {
                LogMessage("Applying pulse parameters in sequence...");

                // Handle based on which mode is active
                if (_frequencyModeActive)
                {
                    // In Frequency mode, send frequency directly to the device
                    if (double.TryParse(ChannelFrequencyTextBox.Text, out double frequency))
                    {
                        string freqUnit = Services.UnitConversionUtility.GetFrequencyUnit(ChannelFrequencyUnitComboBox);
                        double actualFrequency = frequency * Services.UnitConversionUtility.GetFrequencyMultiplier(freqUnit);

                        // Send frequency command directly
                        rigolDG2072.SetFrequency(activeChannel, actualFrequency);
                        LogMessage($"Set pulse frequency to {frequency} {freqUnit} ({actualFrequency} Hz)");

                        // Update UI but don't send this to device
                        double period = 1.0 / actualFrequency;
                        UpdatePulseTimeValue(PulsePeriod, PulsePeriodUnitComboBox, period);
                    }
                }
                else
                {
                    // In Period mode, send period directly to the device
                    if (double.TryParse(PulsePeriod.Text, out double period))
                    {
                        string periodUnit = Services.UnitConversionUtility.GetPeriodUnit(PulsePeriodUnitComboBox);
                        double actualPeriod = period * Services.UnitConversionUtility.GetPeriodMultiplier(periodUnit);

                        // Send period command directly - don't convert to frequency
                        rigolDG2072.SetPulsePeriod(activeChannel, actualPeriod);
                        LogMessage($"Set pulse period to {period} {periodUnit} ({actualPeriod} s)");

                        // Update UI but don't send this to device
                        double frequency = 1.0 / actualPeriod;
                        double displayValue = Services.UnitConversionUtility.ConvertFromMicroHz(frequency * 1e6,
                            Services.UnitConversionUtility.GetFrequencyUnit(ChannelFrequencyUnitComboBox));
                        ChannelFrequencyTextBox.Text = FormatWithMinimumDecimals(displayValue);
                    }
                }

                // Apply transition times and width
                ApplyPulseTransitionTimes();
                ApplyPulseWidth();

                // Refresh UI with actual device values
                UpdatePulseParameters(activeChannel);
                LogMessage("All pulse parameters applied");
            }
            catch (Exception ex)
            {
                LogMessage($"Error applying pulse parameters: {ex.Message}");
            }
        }

        // Separate method for applying transition times
        private void ApplyPulseTransitionTimes()
        {
            if (double.TryParse(PulseRiseTime.Text, out double riseTime))
            {
                string riseTimeUnit = Services.UnitConversionUtility.GetPeriodUnit(PulseRiseTimeUnitComboBox);
                double actualRiseTime = riseTime * Services.UnitConversionUtility.GetPeriodMultiplier(riseTimeUnit);
                rigolDG2072.SetPulseRiseTime(activeChannel, actualRiseTime);
                LogMessage($"Set pulse rise time to {riseTime} {riseTimeUnit} ({actualRiseTime} s)");
            }

            if (double.TryParse(PulseFallTime.Text, out double fallTime))
            {
                string fallTimeUnit = Services.UnitConversionUtility.GetPeriodUnit(PulseFallTimeUnitComboBox);
                double actualFallTime = fallTime * Services.UnitConversionUtility.GetPeriodMultiplier(fallTimeUnit);
                rigolDG2072.SetPulseFallTime(activeChannel, actualFallTime);
                LogMessage($"Set pulse fall time to {fallTime} {fallTimeUnit} ({actualFallTime} s)");
            }
        }

        // Separate method for applying pulse width
        private void ApplyPulseWidth()
        {
            // Validate parameters before applying width
            ValidatePulseParameters();

            // Apply the width (which must fit within the period)
            if (double.TryParse(PulseWidth.Text, out double width))
            {
                string widthUnit = Services.UnitConversionUtility.GetPeriodUnit(PulseWidthUnitComboBox);
                double actualWidth = width * Services.UnitConversionUtility.GetPeriodMultiplier(widthUnit);
                rigolDG2072.SetPulseWidth(activeChannel, actualWidth);
                LogMessage($"Set pulse width to {width} {widthUnit} ({actualWidth} s)");
            }
        }

        /// <summary>
        /// Updates all pulse parameters in the UI with values from the device
        /// </summary>
        private void UpdatePulseParameters(int channel)
        {
            try
            {
                // Only update if the waveform is Pulse
                string currentWaveform = rigolDG2072.SendQuery($":SOUR{channel}:FUNC?").Trim().ToUpper();
                if (currentWaveform.Contains("PULS"))
                {
                    double width = rigolDG2072.GetPulseWidth(channel);
                    double period = rigolDG2072.GetPulsePeriod(channel);
                    double riseTime = rigolDG2072.GetPulseRiseTime(channel);
                    double fallTime = rigolDG2072.GetPulseFallTime(channel);

                    Dispatcher.Invoke(() =>
                    {
                        // Update pulse width with appropriate unit
                        UpdatePulseTimeValue(PulseWidth, PulseWidthUnitComboBox, width);

                        // Update pulse period with appropriate unit
                        UpdatePulseTimeValue(PulsePeriod, PulsePeriodUnitComboBox, period);

                        // Update rise time with appropriate unit
                        UpdatePulseTimeValue(PulseRiseTime, PulseRiseTimeUnitComboBox, riseTime);

                        // Update fall time with appropriate unit
                        UpdatePulseTimeValue(PulseFallTime, PulseFallTimeUnitComboBox, fallTime);

                        // Log the retrieved values for debugging
                        LogMessage($"Retrieved pulse parameters - Width: {FormatWithMinimumDecimals(width)}s, Period: {FormatWithMinimumDecimals(period)}s, " +
                                  $"Rise: {FormatWithMinimumDecimals(riseTime)}s, Fall: {FormatWithMinimumDecimals(fallTime)}s");
                    });
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error updating pulse parameters for channel {channel}: {ex.Message}");
            }
        }

        // Helper method to update time values in TextBoxes with appropriate units
        private void UpdatePulseTimeValue(TextBox timeTextBox, ComboBox unitComboBox, double timeValue)
        {
            // Store current unit to preserve it if possible
            string currentUnit = Services.UnitConversionUtility.GetPeriodUnit(unitComboBox);

            // Convert to picoseconds for internal representation
            double psValue = timeValue * 1e12; // Convert seconds to picoseconds

            // Calculate the display value based on the current unit
            double displayValue = Services.UnitConversionUtility.ConvertFromPicoSeconds(psValue, currentUnit);

            // If the value would display poorly in the current unit, find a better unit
            if (displayValue > 9999 || displayValue < 0.1)
            {
                string[] units = { "ps", "ns", "µs", "ms", "s" };
                int bestUnitIndex = 2; // Default to µs

                for (int i = 0; i < units.Length; i++)
                {
                    double testValue = Services.UnitConversionUtility.ConvertFromPicoSeconds(psValue, units[i]);
                    if (testValue >= 0.1 && testValue < 10000)
                    {
                        bestUnitIndex = i;
                        break;
                    }
                }

                // Update the display value and select the best unit
                displayValue = Services.UnitConversionUtility.ConvertFromPicoSeconds(psValue, units[bestUnitIndex]);

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

            timeTextBox.Text = FormatWithMinimumDecimals(displayValue);
        }

        // Methods for applying individual pulse parameters with unit conversion
        private void ApplyPulseWidth(double width)
        {
            if (!isConnected) return;

            try
            {
                string unit = Services.UnitConversionUtility.GetPeriodUnit(PulseWidthUnitComboBox);
                double actualWidth = width * Services.UnitConversionUtility.GetPeriodMultiplier(unit);

                // Store the current period and transition times
                double period = rigolDG2072.GetPulsePeriod(activeChannel);
                double riseTime = rigolDG2072.GetPulseRiseTime(activeChannel);
                double fallTime = rigolDG2072.GetPulseFallTime(activeChannel);

                // Calculate max allowed width
                double maxWidth = period - 0.7 * (riseTime + fallTime);
                maxWidth *= 0.9; // 10% safety margin

                // Ensure width is within allowed range
                if (actualWidth > maxWidth)
                {
                    actualWidth = maxWidth;
                    LogMessage($"Pulse width limited to {FormatWithMinimumDecimals(actualWidth)} seconds based on current period and transition times");

                    // Update UI to show actual value
                    Dispatcher.Invoke(() =>
                    {
                        UpdatePulseWidthInUI(actualWidth);
                    });
                }

                // Apply the width
                rigolDG2072.SetPulseWidth(activeChannel, actualWidth);
                LogMessage($"Set CH{activeChannel} pulse width to {width} {unit} ({actualWidth} s)");
            }
            catch (Exception ex)
            {
                LogMessage($"Error applying pulse width: {ex.Message}");
            }
        }

        // Update the individual parameter methods to follow the same approach
        private void ApplyPulsePeriod(double period)
        {
            if (!isConnected) return;

            try
            {
                // Only use this direct period method in Period mode
                if (!_frequencyModeActive)
                {
                    string unit = Services.UnitConversionUtility.GetPeriodUnit(PulsePeriodUnitComboBox);
                    double actualPeriod = period * Services.UnitConversionUtility.GetPeriodMultiplier(unit);

                    // Send period command directly
                    rigolDG2072.SetPulsePeriod(activeChannel, actualPeriod);
                    LogMessage($"Set CH{activeChannel} pulse period to {period} {unit} ({actualPeriod} s)");

                    // After changing period, we need to validate width
                    ValidatePulseParameters();

                    // Update frequency display but don't send to device
                    UpdateCalculatedRateValue();
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error applying pulse period: {ex.Message}");
            }
        }

        private void ApplyPulseRiseTime(double riseTime)
        {
            if (!isConnected) return;

            try
            {
                string unit = Services.UnitConversionUtility.GetPeriodUnit(PulseRiseTimeUnitComboBox);
                double actualRiseTime = riseTime * Services.UnitConversionUtility.GetPeriodMultiplier(unit);

                // Set the rise time
                rigolDG2072.SetPulseRiseTime(activeChannel, actualRiseTime);
                LogMessage($"Set CH{activeChannel} pulse rise time to {riseTime} {unit} ({actualRiseTime} s)");

                // After changing rise time, we may need to validate width
                ValidatePulseParameters();
            }
            catch (Exception ex)
            {
                LogMessage($"Error applying pulse rise time: {ex.Message}");
            }
        }

        private void ApplyPulseFallTime(double fallTime)
        {
            if (!isConnected) return;

            try
            {
                string unit = Services.UnitConversionUtility.GetPeriodUnit(PulseFallTimeUnitComboBox);
                double actualFallTime = fallTime * Services.UnitConversionUtility.GetPeriodMultiplier(unit);

                // Set the fall time
                rigolDG2072.SetPulseFallTime(activeChannel, actualFallTime);
                LogMessage($"Set CH{activeChannel} pulse fall time to {fallTime} {unit} ({actualFallTime} s)");

                // After changing fall time, we may need to validate width
                ValidatePulseParameters();
            }
            catch (Exception ex)
            {
                LogMessage($"Error applying pulse fall time: {ex.Message}");
            }
        }

        // Helper method to adjust time values and units automatically
        private void AdjustPulseTimeAndUnit(TextBox textBox, ComboBox unitComboBox)
        {
            if (!double.TryParse(textBox.Text, out double value))
                return;

            string currentUnit = ((ComboBoxItem)unitComboBox.SelectedItem).Content.ToString();

            // Convert current value to picoseconds to maintain precision
            double psValue = Services.UnitConversionUtility.ConvertToPicoSeconds(value, currentUnit);

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
            double displayValue = Services.UnitConversionUtility.ConvertFromPicoSeconds(psValue, timeUnits[unitIndex]);

            // Handle values that are too large (> 9999)
            while (displayValue > 9999 && unitIndex < timeUnits.Length - 1)
            {
                unitIndex++;
                displayValue = Services.UnitConversionUtility.ConvertFromPicoSeconds(psValue, timeUnits[unitIndex]);
            }

            // Handle values that are too small (< 0.1)
            while (displayValue < 0.1 && unitIndex > 0)
            {
                unitIndex--;
                displayValue = Services.UnitConversionUtility.ConvertFromPicoSeconds(psValue, timeUnits[unitIndex]);
            }

            // Update the textbox with formatted value
            textBox.Text = FormatWithMinimumDecimals(displayValue);

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

        #endregion

        #region Unit Selection Handlers

        // Update these SelectionChanged handlers to also update calculated values
        private void ChannelFrequencyUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isConnected) return;

            if (double.TryParse(ChannelFrequencyTextBox.Text, out double frequency))
            {
                ApplyFrequency(frequency);
                // Update the calculated period when frequency unit changes
                if (_frequencyModeActive && ((ComboBoxItem)ChannelWaveformComboBox.SelectedItem).Content.ToString().ToUpper() == "PULSE")
                    UpdateCalculatedRateValue();
            }
        }

        private void ChannelAmplitudeUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isConnected) return;

            if (double.TryParse(ChannelAmplitudeTextBox.Text, out double amplitude))
            {
                ApplyAmplitude(amplitude);
            }
        }

        #endregion

        #region Apply Value Methods

        private void ApplyPeriod(double period)
        {
            if (!isConnected) return;

            try
            {
                string unit = Services.UnitConversionUtility.GetPeriodUnit(PulsePeriodUnitComboBox);
                double actualPeriod = period * Services.UnitConversionUtility.GetPeriodMultiplier(unit);

                // Send period command directly
                rigolDG2072.SendCommand($"SOURCE{activeChannel}:PERiod {actualPeriod}");
                LogMessage($"Set CH{activeChannel} period to {period} {unit} ({actualPeriod} s)");

                // Update frequency display but don't send to device
                UpdateCalculatedRateValue();
            }
            catch (Exception ex)
            {
                LogMessage($"Error applying period: {ex.Message}");
            }
        }


        // Make sure frequency changes use the direct frequency command
        private void ApplyFrequency(double frequency)
        {
            if (!isConnected) return;

            try
            {
                // Only use this direct frequency method in Frequency mode
                if (_frequencyModeActive)
                {
                    string unit = UnitConversionUtility.GetFrequencyUnit(ChannelFrequencyUnitComboBox);
                    double actualFrequency = frequency * UnitConversionUtility.GetFrequencyMultiplier(unit);

                    // Send frequency command directly
                    rigolDG2072.SetFrequency(activeChannel, actualFrequency);
                    LogMessage($"Set CH{activeChannel} frequency to {frequency} {unit} ({actualFrequency} Hz)");

                    // Update period display but don't send to device
                    if (((ComboBoxItem)ChannelWaveformComboBox.SelectedItem).Content.ToString().ToUpper() == "PULSE")
                        UpdateCalculatedRateValue();
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error applying frequency: {ex.Message}");
            }
        }





        private void ApplyAmplitude(double amplitude)
        {
            string unit = UnitConversionUtility.GetAmplitudeUnit(ChannelAmplitudeUnitComboBox);
            double multiplier = UnitConversionUtility.GetAmplitudeMultiplier(unit);
            double actualAmplitude = amplitude * multiplier;

            rigolDG2072.SetAmplitude(activeChannel, actualAmplitude);
            LogMessage($"Set CH{activeChannel} amplitude to {amplitude} {unit} ({actualAmplitude} Vpp)");

            // If harmonics are enabled and we're on the harmonic waveform, update harmonics for the new fundamental amplitude
            if (isConnected && _harmonicsUIController != null &&
                ChannelWaveformComboBox.SelectedItem != null &&
                ((ComboBoxItem)ChannelWaveformComboBox.SelectedItem).Content.ToString().ToUpper() == "HARMONIC")
            {
                // Update harmonics for the new amplitude
                _harmonicsUIController.UpdateHarmonicsForFundamentalChange(actualAmplitude);
            }
        }


        // Update the Apply method to handle both frequency and period modes
        private void ApplySettings()
        {
            if (!isConnected) return;

            try
            {
                string waveform = ((ComboBoxItem)ChannelWaveformComboBox.SelectedItem).Content.ToString().ToUpper();

                // For noise waveform, skip frequency/period settings
                if (waveform != "NOISE")
                {
                    // Handle based on which mode is active
                    if (_frequencyModeActive)
                    {
                        // In Frequency mode, send frequency directly to the device
                        if (double.TryParse(ChannelFrequencyTextBox.Text, out double frequency))
                        {
                            string freqUnit = Services.UnitConversionUtility.GetFrequencyUnit(ChannelFrequencyUnitComboBox);
                            double actualFrequency = frequency * Services.UnitConversionUtility.GetFrequencyMultiplier(freqUnit);

                            // Send frequency command directly
                            rigolDG2072.SetFrequency(activeChannel, actualFrequency);
                            LogMessage($"Set {waveform} frequency to {frequency} {freqUnit} ({actualFrequency} Hz)");
                        }
                    }
                    else
                    {
                        // In Period mode, send period directly to the device
                        if (double.TryParse(PulsePeriod.Text, out double period))
                        {
                            string periodUnit = Services.UnitConversionUtility.GetPeriodUnit(PulsePeriodUnitComboBox);
                            double actualPeriod = period * Services.UnitConversionUtility.GetPeriodMultiplier(periodUnit);

                            // Send period command directly
                            if (waveform == "PULSE")
                            {
                                // For pulse waveform, use the specific pulse period method
                                rigolDG2072.SetPulsePeriod(activeChannel, actualPeriod);
                            }
                            else
                            {
                                // For other waveforms, use general period command
                                rigolDG2072.SendCommand($"SOURCE{activeChannel}:PERiod {actualPeriod}");
                            }

                            LogMessage($"Set {waveform} period to {period} {periodUnit} ({actualPeriod} s)");
                        }
                    }
                }

                // Apply other settings...
            }
            catch (Exception ex)
            {
                LogMessage($"Error applying settings: {ex.Message}");
            }
        }

        #endregion

        #region UI Formatting and Adjustment Methods

        private void UpdateWaveformSpecificControls(string waveformType) //v5
        {
            string waveform = waveformType.ToUpper();
            bool isPulse = (waveform == "PULSE");
            bool isNoise = (waveform == "NOISE");
            bool isDualTone = (waveform == "DUAL TONE");
            bool isHarmonic = (waveform == "HARMONIC");
            bool isDC = (waveform == "DC");  // Add DC type check

            // Handle symmetry control visibility (for Ramp waveform)
            if (SymmetryDockPanel != null)
            {
                SymmetryDockPanel.Visibility = (waveform == "RAMP") ? Visibility.Visible : Visibility.Collapsed;
            }

            // Handle duty cycle control visibility (for Square waveform only)
            if (DutyCycleDockPanel != null)
            {
                DutyCycleDockPanel.Visibility = (waveform == "SQUARE") ? Visibility.Visible : Visibility.Collapsed;
            }

            // Handle frequency/period control visibility (hide for Noise)
            if (FrequencyDockPanel != null && PeriodDockPanel != null)
            {
                bool showFrequency = !isNoise;

                if (isPulse)
                {
                    // For pulse, respect the frequency/period mode toggle
                    if (_frequencyModeActive)
                    {
                        FrequencyDockPanel.Visibility = showFrequency ? Visibility.Visible : Visibility.Collapsed;
                        PeriodDockPanel.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        FrequencyDockPanel.Visibility = Visibility.Collapsed;
                        PeriodDockPanel.Visibility = showFrequency ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
                else
                {
                    // For non-pulse waveforms, apply the current frequency/period mode
                    if (_frequencyModeActive)
                    {
                        FrequencyDockPanel.Visibility = showFrequency ? Visibility.Visible : Visibility.Collapsed;
                        PeriodDockPanel.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        FrequencyDockPanel.Visibility = Visibility.Collapsed;
                        PeriodDockPanel.Visibility = showFrequency ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
            }

            // Handle pulse-specific controls
            if (PulseWidthDockPanel != null &&
                PulseRiseTimeDockPanel != null &&
                PulseFallTimeDockPanel != null)
            {
                Visibility pulseVisibility = isPulse ? Visibility.Visible : Visibility.Collapsed;

                // Show/hide pulse-specific controls
                PulseWidthDockPanel.Visibility = pulseVisibility;
                PulseRiseTimeDockPanel.Visibility = pulseVisibility;
                //PulseFallTimeDockPanel.Visibility = pulseVisibility; UpdateWaveformSpecificControls

                // Show/hide the mode toggle
                if (PulseRateModeDockPanel != null)
                    PulseRateModeDockPanel.Visibility = pulseVisibility;
            }

            // Handle "To Period" button visibility - hide for noise waveform
            // Handle "To Period" button visibility - hide for noise waveform
            // Handle "To Period" button visibility - disable for noise waveform
            // Handle "To Period" button visibility - hide for noise waveform
            // Handle "To Period" button visibility - hide for noise waveform
            if (FrequencyPeriodModeToggle != null)
            {
                FrequencyPeriodModeToggle.Visibility = isNoise ? Visibility.Collapsed : Visibility.Visible;
                FrequencyPeriodModeToggle.IsEnabled = !isNoise;
                LogMessage($"Setting FrequencyPeriodModeToggle visibility to {(isNoise ? "Collapsed" : "Visible")} and IsEnabled to {(!isNoise)} for waveform {waveform}");
            }

            // Also handle the PulseRateModeToggle button visibility
            if (PulseRateModeToggle != null)
            {
                PulseRateModeToggle.Visibility = isNoise ? Visibility.Collapsed : Visibility.Visible;
                PulseRateModeToggle.IsEnabled = !isNoise;
                LogMessage($"Setting PulseRateModeToggle visibility to {(isNoise ? "Collapsed" : "Visible")} and IsEnabled to {(!isNoise)} for waveform {waveform}");
            }

            // Handle phase control visibility - hide for noise waveform
            if (PhaseDockPanel != null)
            {
                PhaseDockPanel.Visibility = isNoise ? Visibility.Collapsed : Visibility.Visible;
                LogMessage($"Setting PhaseDockPanel visibility to {(isNoise ? "Collapsed" : "Visible")} for waveform {waveform}");
            }

            // Handle fall time control visibility - hide for noise waveform (already handled for non-pulse waveforms)
            if (PulseFallTimeDockPanel != null)
            {
                // For noise waveform, always hide
                if (isNoise)
                {
                    PulseFallTimeDockPanel.Visibility = Visibility.Collapsed;
                    LogMessage($"Setting PulseFallTimeDockPanel visibility to Collapsed for noise waveform");
                }
                // For non-noise, show only if it's pulse waveform
                else
                {
                    PulseFallTimeDockPanel.Visibility = isPulse ? Visibility.Visible : Visibility.Collapsed;
                }
            }

            // Hide Apply Settings button for dual tone waveform
            if (ChannelApplyButton != null)
            {
                // Hide button for dual tone
                if (isDualTone)
                {
                    ChannelApplyButton.Visibility = Visibility.Collapsed;
                    LogMessage($"Hiding Apply Settings button for dual tone waveform");
                }
                else
                {
                    ChannelApplyButton.Visibility = Visibility.Visible;
                }
            }

            // After the code that sets the frequency combo box visibility
            // After the code that sets the frequency panel visibility
            if (FrequencyDockPanel != null && FrequencyPeriodModeToggle != null && PulseRateModeToggle != null)
            {
                // If frequency panel is hidden, also hide the toggle buttons
                if (FrequencyDockPanel.Visibility == Visibility.Collapsed)
                {
                    FrequencyPeriodModeToggle.Visibility = Visibility.Collapsed;
                    PulseRateModeToggle.Visibility = Visibility.Collapsed;
                    LogMessage($"Setting toggle buttons visibility to Collapsed because frequency panel is hidden");
                }
                else
                {
                    FrequencyPeriodModeToggle.Visibility = Visibility.Visible;
                    PulseRateModeToggle.Visibility = Visibility.Visible;
                    LogMessage($"Setting toggle buttons visibility to Visible because frequency panel is visible");
                }
            }

            // Handle arbitrary waveform controls visibility
            if (ArbitraryWaveformGroupBox != null)
            {
                ArbitraryWaveformGroupBox.Visibility = (waveform == "USER") ? Visibility.Visible : Visibility.Collapsed;
            }

            // Handle harmonic-specific controls
            if (HarmonicsGroupBox != null)
            {
                HarmonicsGroupBox.Visibility = isHarmonic ? Visibility.Visible : Visibility.Collapsed;
            }

            // Handle dual tone-specific controls
            if (DualToneGroupBox != null)
            {
                DualToneGroupBox.Visibility = isDualTone ? Visibility.Visible : Visibility.Collapsed;

                // Also manage the secondary frequency controls' visibility
                if (SecondaryFrequencyDockPanel != null)
                {
                    SecondaryFrequencyDockPanel.Visibility = isDualTone ? Visibility.Visible : Visibility.Collapsed;
                }

                // Reference to FrequencyRatioComboBox instead of FrequencyRatioDockPanel
                if (FrequencyRatioComboBox != null)
                {
                    FrequencyRatioComboBox.Visibility = isDualTone ? Visibility.Visible : Visibility.Collapsed;
                }

                if (CenterOffsetPanel != null && DirectFrequencyPanel != null)
                {
                    // Respect the current dual tone mode
                    if (CenterOffsetMode != null && DirectFrequencyMode != null)
                    {
                        bool isDirectMode = DirectFrequencyMode.IsChecked == true;
                        DirectFrequencyPanel.Visibility = (isDualTone && isDirectMode) ? Visibility.Visible : Visibility.Collapsed;
                        CenterOffsetPanel.Visibility = (isDualTone && !isDirectMode) ? Visibility.Visible : Visibility.Collapsed;
                    }
                }

                if (SynchronizeFrequenciesCheckBox != null)
                {
                    SynchronizeFrequenciesCheckBox.Visibility = isDualTone ? Visibility.Visible : Visibility.Collapsed;
                }
            }

            // DC group box visibility
            // DC group box visibility
            // DC group box visibility
            if (DCGroupBox != null)
            {
                DCGroupBox.Visibility = isDC ? Visibility.Visible : Visibility.Collapsed;
            }

            // Hide all standard controls and show only DC controls for DC waveform
            if (isDC)
            {
                // Hide frequency/period, amplitude, phase controls
                if (FrequencyDockPanel != null) FrequencyDockPanel.Visibility = Visibility.Collapsed;
                if (PeriodDockPanel != null) PeriodDockPanel.Visibility = Visibility.Collapsed;

                // Toggle buttons should be hidden for DC
                if (FrequencyPeriodModeToggle != null) FrequencyPeriodModeToggle.Visibility = Visibility.Collapsed;
                if (PulseRateModeToggle != null) PulseRateModeToggle.Visibility = Visibility.Collapsed;

                // Amplitude should be hidden for DC
                if (FindVisualParent<DockPanel>(ChannelAmplitudeTextBox) != null)
                    FindVisualParent<DockPanel>(ChannelAmplitudeTextBox).Visibility = Visibility.Collapsed;

                // Phase should be hidden for DC
                if (PhaseDockPanel != null) PhaseDockPanel.Visibility = Visibility.Collapsed;

                // Offset control is redundant with DC voltage - hide it
                if (FindVisualParent<DockPanel>(ChannelOffsetTextBox) != null)
                    FindVisualParent<DockPanel>(ChannelOffsetTextBox).Visibility = Visibility.Collapsed;
            }
            else
            {
                // Show amplitude control for non-DC waveforms
                if (FindVisualParent<DockPanel>(ChannelAmplitudeTextBox) != null)
                    FindVisualParent<DockPanel>(ChannelAmplitudeTextBox).Visibility = Visibility.Visible;

                // Show offset control for non-DC waveforms
                if (FindVisualParent<DockPanel>(ChannelOffsetTextBox) != null)
                    FindVisualParent<DockPanel>(ChannelOffsetTextBox).Visibility = Visibility.Visible;
            }



        }

        private void ChannelDutyCycleTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isConnected) return;
            if (!double.TryParse(DutyCycle.Text, out double dutyCycle)) return;

            if (_dutyCycleUpdateTimer == null)
            {
                _dutyCycleUpdateTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                _dutyCycleUpdateTimer.Tick += (s, args) =>
                {
                    _dutyCycleUpdateTimer.Stop();
                    if (double.TryParse(DutyCycle.Text, out double duty))
                    {
                        rigolDG2072.SetDutyCycle(activeChannel, duty);
                    }
                };
            }

            _dutyCycleUpdateTimer.Stop();
            _dutyCycleUpdateTimer.Start();
        }

        private void ChannelDutyCycleTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(DutyCycle.Text, out double dutyCycle))
            {
                // Ensure value is in valid range
                dutyCycle = Math.Max(0, Math.Min(100, dutyCycle));
                DutyCycle.Text = FormatWithMinimumDecimals(dutyCycle);
            }
        }


        private void SecondaryFrequencyTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!isConnected) return;
            if (double.TryParse(SecondaryFrequencyTextBox.Text, out double frequency))
            {
                ApplyDualToneParameters();
            }
        }

        private void CenterFrequencyTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!isConnected) return;
            if (double.TryParse(CenterFrequencyTextBox.Text, out double frequency))
            {
                UpdateFrequenciesFromCenterOffset();
            }
        }

        private void OffsetFrequencyTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!isConnected) return;
            if (double.TryParse(OffsetFrequencyTextBox.Text, out double frequency))
            {
                UpdateFrequenciesFromCenterOffset();
            }
        }



        /// <summary>
        /// Updates UI elements based on the selected frequency/period mode
        /// </summary>
        private void UpdateFrequencyPeriodMode()
        {
            if (!isConnected) return;

            string currentWaveform = ((ComboBoxItem)ChannelWaveformComboBox.SelectedItem).Content.ToString().ToUpper();
            bool isNoise = (currentWaveform == "NOISE"); // Noise doesn't use frequency or period

            // Toggle visibility of panels based on selected mode
            if (_frequencyModeActive)
            {
                // In Frequency mode, show frequency controls, hide period controls
                FrequencyDockPanel.Visibility = isNoise ? Visibility.Collapsed : Visibility.Visible;
                PeriodDockPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                // In Period mode, show period controls, hide frequency controls
                FrequencyDockPanel.Visibility = Visibility.Collapsed;
                PeriodDockPanel.Visibility = isNoise ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        /// <summary>
        /// Calculates and updates the complementary value (frequency or period) based on current mode
        /// </summary>
        private void UpdateCalculatedRateValue()
        {
            if (!isConnected) return;

            try
            {
                string currentWaveform = ((ComboBoxItem)ChannelWaveformComboBox.SelectedItem).Content.ToString().ToUpper();

                if (_frequencyModeActive)
                {
                    // Calculate period from frequency
                    if (double.TryParse(ChannelFrequencyTextBox.Text, out double frequency))
                    {
                        string freqUnit = Services.UnitConversionUtility.GetFrequencyUnit(ChannelFrequencyUnitComboBox);
                        double freqInHz = frequency * Services.UnitConversionUtility.GetFrequencyMultiplier(freqUnit);

                        if (freqInHz > 0)
                        {
                            double periodInSeconds = 1.0 / freqInHz;

                            // Choose appropriate unit for displaying the period
                            string periodUnit = Services.UnitConversionUtility.GetPeriodUnit(PulsePeriodUnitComboBox);
                            double displayValue = Services.UnitConversionUtility.ConvertFromPicoSeconds(periodInSeconds * 1e12, periodUnit);

                            // Update the period TextBox with the calculated value
                            PulsePeriod.Text = FormatWithMinimumDecimals(displayValue);
                        }
                    }
                }
                else
                {
                    // Calculate frequency from period
                    if (double.TryParse(PulsePeriod.Text, out double period))
                    {
                        string periodUnit = Services.UnitConversionUtility.GetPeriodUnit(PulsePeriodUnitComboBox);
                        double periodInSeconds = period * Services.UnitConversionUtility.GetPeriodMultiplier(periodUnit);

                        if (periodInSeconds > 0)
                        {
                            double freqInHz = 1.0 / periodInSeconds;

                            // Choose appropriate unit for displaying the frequency
                            string freqUnit = Services.UnitConversionUtility.GetFrequencyUnit(ChannelFrequencyUnitComboBox);
                            double displayValue = Services.UnitConversionUtility.ConvertFromMicroHz(freqInHz * 1e6, freqUnit);

                            // Update the frequency TextBox with the calculated value
                            ChannelFrequencyTextBox.Text = FormatWithMinimumDecimals(displayValue);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error updating calculated rate value: {ex.Message}");
            }
        }

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

        private void AdjustFrequencyAndUnit(TextBox textBox, ComboBox unitComboBox)
        {
            if (!double.TryParse(textBox.Text, out double value))
                return;

            string currentUnit = ((ComboBoxItem)unitComboBox.SelectedItem).Content.ToString();

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

            // Handle values that are too large (> 9999)
            while (displayValue > 9999 && unitIndex < frequencyUnits.Length - 1)
            {
                unitIndex++;
                displayValue = UnitConversionUtility.ConvertFromMicroHz(microHzValue, frequencyUnits[unitIndex]);
            }

            // Handle values that are too small (< 0.1)
            while (displayValue < 0.1 && unitIndex > 0)
            {
                unitIndex--;
                displayValue = UnitConversionUtility.ConvertFromMicroHz(microHzValue, frequencyUnits[unitIndex]);
            }

            // Update the textbox with formatted value
            textBox.Text = FormatWithMinimumDecimals(displayValue);

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

        private void AdjustAmplitudeAndUnit(TextBox textBox, ComboBox unitComboBox)
        {
            if (!double.TryParse(textBox.Text, out double value))
                return;

            string currentUnit = ((ComboBoxItem)unitComboBox.SelectedItem).Content.ToString();
            bool isRms = currentUnit.Contains("rms");

            string[] amplitudeUnits;
            if (isRms)
            {
                amplitudeUnits = new[] { "mVrms", "Vrms" };
            }
            else
            {
                amplitudeUnits = new[] { "mVpp", "Vpp" };
            }

            // Map the current unit to our array index
            int unitIndex = currentUnit.StartsWith("m") ? 0 : 1;

            // Handle values that are too large (> 9999)
            if (value > 9999 && unitIndex == 0)
            {
                value /= 1000;
                unitIndex = 1;
            }

            // Handle values that are too small (< 0.1)
            if (value < 0.1 && unitIndex == 1)
            {
                value *= 1000;
                unitIndex = 0;
            }

            // Find and select the unit in the combo box
            for (int i = 0; i < unitComboBox.Items.Count; i++)
            {
                ComboBoxItem item = unitComboBox.Items[i] as ComboBoxItem;
                if (item != null && item.Content.ToString() == amplitudeUnits[unitIndex])
                {
                    unitComboBox.SelectedIndex = i;
                    break;
                }
            }
            textBox.Text = FormatWithMinimumDecimals(value);
        }

        #endregion

        #region TextBox LostFocus Handlers

        private void ChannelFrequencyTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(ChannelFrequencyTextBox.Text, out double frequency))
            {
                string currentUnit = UnitConversionUtility.GetFrequencyUnit(ChannelFrequencyUnitComboBox);
                double microHzValue = UnitConversionUtility.ConvertToMicroHz(frequency, currentUnit);
                double displayValue = UnitConversionUtility.ConvertFromMicroHz(microHzValue, currentUnit);
                ChannelFrequencyTextBox.Text = FormatWithMinimumDecimals(displayValue);
            }
        }

        private void ChannelAmplitudeTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(ChannelAmplitudeTextBox.Text, out double amplitude))
            {
                ChannelAmplitudeTextBox.Text = FormatWithMinimumDecimals(amplitude);
            }
        }

        private void ChannelOffsetTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(ChannelOffsetTextBox.Text, out double offset))
            {
                ChannelOffsetTextBox.Text = FormatWithMinimumDecimals(offset);
            }
        }

        private void ChannelPhaseTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(ChannelPhaseTextBox.Text, out double phase))
            {
                ChannelPhaseTextBox.Text = FormatWithMinimumDecimals(phase);
            }
        }

        #endregion

        #region DualTone

        // Handler for mode selection
        private void DualToneModeChanged(object sender, RoutedEventArgs e)
        {
            if (!isConnected) return;

            bool isDirectMode = DirectFrequencyMode.IsChecked == true;

            // Toggle visibility of panels
            DirectFrequencyPanel.Visibility = isDirectMode ? Visibility.Visible : Visibility.Collapsed;
            CenterOffsetPanel.Visibility = isDirectMode ? Visibility.Collapsed : Visibility.Visible;

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

        private void UpdateSecondaryFrequencyForDualTone()
        {
            if (!isConnected) return;

            try
            {
                if (double.TryParse(ChannelFrequencyTextBox.Text, out double primaryFreq))
                {
                    // Get the current ratio
                    double currentRatio = 2.0; // Default
                    if (FrequencyRatioComboBox.SelectedItem is ComboBoxItem selectedItem &&
                        double.TryParse(selectedItem.Content.ToString(), out double ratio))
                    {
                        currentRatio = ratio;
                    }

                    // Calculate secondary frequency
                    double secondaryFreq = primaryFreq * currentRatio;

                    // Update secondary frequency display
                    SecondaryFrequencyTextBox.Text = FormatWithMinimumDecimals(secondaryFreq);

                    // If in dual tone mode, apply the changes
                    if (((ComboBoxItem)ChannelWaveformComboBox.SelectedItem).Content.ToString().ToUpper() == "DUAL TONE")
                    {
                        ApplyDualToneParameters();
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error updating secondary frequency: {ex.Message}");
            }
        }

        private void ApplyDualToneParameters()
        {
            if (!isConnected) return;

            try
            {
                // Handle based on mode
                if (CenterOffsetMode != null && CenterOffsetMode.IsChecked == true)
                {
                    // Already using center/offset mode, just update calculations
                    UpdateFrequenciesFromCenterOffset();
                }
                else
                {
                    // Direct mode - similar to original function

                    // Get primary frequency
                    if (!double.TryParse(ChannelFrequencyTextBox.Text, out double frequency))
                        return;

                    string freqUnit = UnitConversionUtility.GetFrequencyUnit(ChannelFrequencyUnitComboBox);
                    double freqMultiplier = UnitConversionUtility.GetFrequencyMultiplier(freqUnit);
                    double actualPrimaryFrequency = frequency * freqMultiplier;

                    // Get secondary frequency
                    double actualSecondaryFrequency = actualPrimaryFrequency * 2.0; // Default
                    if (SecondaryFrequencyTextBox != null && double.TryParse(SecondaryFrequencyTextBox.Text, out double secondaryFreq))
                    {
                        string secondaryFreqUnit = UnitConversionUtility.GetFrequencyUnit(SecondaryFrequencyUnitComboBox);
                        double secondaryFreqMultiplier = UnitConversionUtility.GetFrequencyMultiplier(secondaryFreqUnit);
                        actualSecondaryFrequency = secondaryFreq * secondaryFreqMultiplier;
                    }

                    // Get amplitude, offset, phase
                    if (!double.TryParse(ChannelAmplitudeTextBox.Text, out double amplitude) ||
                        !double.TryParse(ChannelOffsetTextBox.Text, out double offset) ||
                        !double.TryParse(ChannelPhaseTextBox.Text, out double phase))
                        return;

                    string ampUnit = UnitConversionUtility.GetAmplitudeUnit(ChannelAmplitudeUnitComboBox);
                    double ampMultiplier = UnitConversionUtility.GetAmplitudeMultiplier(ampUnit);
                    double actualAmplitude = amplitude * ampMultiplier;

                    // Create parameters dictionary for our improved implementation
                    Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "Frequency", actualPrimaryFrequency },
                { "Frequency2", actualSecondaryFrequency },
                { "Amplitude", actualAmplitude },
                { "Offset", offset },
                { "Phase", phase }
            };

                    // Apply the dual tone waveform using our improved method
                    rigolDG2072.ApplyDualToneWaveform(activeChannel, parameters);

                    LogMessage($"Applied Dual Tone waveform to CH{activeChannel} with Primary Freq={frequency} {freqUnit}, " +
                             $"Secondary Freq={SecondaryFrequencyTextBox.Text} {UnitConversionUtility.GetFrequencyUnit(SecondaryFrequencyUnitComboBox)}, " +
                             $"Amp={amplitude} {ampUnit}, Offset={offset}V, Phase={phase}°");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error applying dual tone settings: {ex.Message}");
            }
        }

        // Center frequency changed
        private void CenterFrequencyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isConnected) return;
            if (!double.TryParse(CenterFrequencyTextBox.Text, out double centerFrequency)) return;

            if (CenterOffsetMode.IsChecked != true) return;

            // Use a timer similar to primary frequency
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

        // Offset frequency changed
        private void OffsetFrequencyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isConnected) return;
            if (!double.TryParse(OffsetFrequencyTextBox.Text, out double offsetFrequency)) return;

            if (CenterOffsetMode.IsChecked != true) return;

            // Use a timer similar to primary frequency
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

        // Unit combobox selection changed
        private void CenterFrequencyUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isConnected) return;
            if (CenterOffsetMode.IsChecked != true) return;

            if (double.TryParse(CenterFrequencyTextBox.Text, out _))
            {
                UpdateFrequenciesFromCenterOffset();
            }
        }

        private void OffsetFrequencyUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isConnected) return;
            if (CenterOffsetMode.IsChecked != true) return;

            if (double.TryParse(OffsetFrequencyTextBox.Text, out _))
            {
                UpdateFrequenciesFromCenterOffset();
            }
        }

        private void SecondaryFrequencyUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isConnected) return;
            if (DirectFrequencyMode.IsChecked != true) return;

            if (double.TryParse(SecondaryFrequencyTextBox.Text, out _))
            {
                ApplyDualToneParameters();
            }
        }

        // Calculate center and offset from F1 and F2
        private void UpdateCenterOffsetFromFrequencies()
        {
            try
            {
                // Get current F1 (primary) and F2 (secondary) in Hz
                double f1Hz = 0, f2Hz = 0;

                if (double.TryParse(ChannelFrequencyTextBox.Text, out double f1))
                {
                    string f1Unit = UnitConversionUtility.GetFrequencyUnit(ChannelFrequencyUnitComboBox);
                    f1Hz = f1 * UnitConversionUtility.GetFrequencyMultiplier(f1Unit);
                }

                if (double.TryParse(SecondaryFrequencyTextBox.Text, out double f2))
                {
                    string f2Unit = UnitConversionUtility.GetFrequencyUnit(SecondaryFrequencyUnitComboBox);
                    f2Hz = f2 * UnitConversionUtility.GetFrequencyMultiplier(f2Unit);
                }

                // Calculate center frequency (F1 + F2)/2
                double centerFreqHz = (f1Hz + f2Hz) / 2.0;

                // Calculate offset frequency F2 - F1
                double offsetFreqHz = f2Hz - f1Hz;

                // Update UI with calculated values
                string centerUnit = UnitConversionUtility.GetFrequencyUnit(CenterFrequencyUnitComboBox);
                double displayCenterFreq = UnitConversionUtility.ConvertFromMicroHz(centerFreqHz * 1e6, centerUnit);
                CenterFrequencyTextBox.Text = FormatWithMinimumDecimals(displayCenterFreq);

                string offsetUnit = UnitConversionUtility.GetFrequencyUnit(OffsetFrequencyUnitComboBox);
                double displayOffsetFreq = UnitConversionUtility.ConvertFromMicroHz(offsetFreqHz * 1e6, offsetUnit);
                OffsetFrequencyTextBox.Text = FormatWithMinimumDecimals(displayOffsetFreq);
            }
            catch (Exception ex)
            {
                LogMessage($"Error updating center/offset values: {ex.Message}");
            }
        }

        // Calculate F1 and F2 from center and offset
        private void UpdateFrequenciesFromCenterOffset()
        {
            try
            {
                // Get current center and offset frequencies in Hz
                double centerFreqHz = 0, offsetFreqHz = 0;

                if (double.TryParse(CenterFrequencyTextBox.Text, out double center))
                {
                    string centerUnit = UnitConversionUtility.GetFrequencyUnit(CenterFrequencyUnitComboBox);
                    centerFreqHz = center * UnitConversionUtility.GetFrequencyMultiplier(centerUnit);
                }

                if (double.TryParse(OffsetFrequencyTextBox.Text, out double offset))
                {
                    string offsetUnit = UnitConversionUtility.GetFrequencyUnit(OffsetFrequencyUnitComboBox);
                    offsetFreqHz = offset * UnitConversionUtility.GetFrequencyMultiplier(offsetUnit);
                }

                // Calculate F1 and F2
                // Center = (F1 + F2)/2 => F1 + F2 = 2 * Center
                // Offset = F2 - F1
                // Solving: F1 = Center - Offset/2, F2 = Center + Offset/2
                double f1Hz = centerFreqHz - (offsetFreqHz / 2.0);
                double f2Hz = centerFreqHz + (offsetFreqHz / 2.0);

                // Update the calculated values display
                CalculatedF1Display.Text = $"{FormatWithMinimumDecimals(f1Hz)} Hz";
                CalculatedF2Display.Text = $"{FormatWithMinimumDecimals(f2Hz)} Hz";

                // Apply to device with f1Hz and f2Hz
                ApplyDualToneWithFrequencies(f1Hz, f2Hz);
            }
            catch (Exception ex)
            {
                LogMessage($"Error updating frequencies from center/offset: {ex.Message}");
            }
        }

        // Apply dual tone with specific frequencies
        private void ApplyDualToneWithFrequencies(double f1Hz, double f2Hz)
        {
            try
            {
                // Get amplitude, offset, phase
                if (!double.TryParse(ChannelAmplitudeTextBox.Text, out double amplitude) ||
                    !double.TryParse(ChannelOffsetTextBox.Text, out double offset) ||
                    !double.TryParse(ChannelPhaseTextBox.Text, out double phase))
                    return;

                string ampUnit = UnitConversionUtility.GetAmplitudeUnit(ChannelAmplitudeUnitComboBox);
                double ampMultiplier = UnitConversionUtility.GetAmplitudeMultiplier(ampUnit);
                double actualAmplitude = amplitude * ampMultiplier;

                // Log the parameters we're about to apply
                LogMessage($"Applying dual tone with: Freq1={f1Hz}Hz, Freq2={f2Hz}Hz, " +
                          $"Amp={actualAmplitude}Vpp, Offset={offset}V, Phase={phase}°");

                // Create parameters dictionary for our improved implementation
                Dictionary<string, object> parameters = new Dictionary<string, object>
        {
            { "Frequency", f1Hz },
            { "Frequency2", f2Hz },
            { "Amplitude", actualAmplitude },
            { "Offset", offset },
            { "Phase", phase }
        };

                // Apply the dual tone waveform using our improved method
                rigolDG2072.ApplyDualToneWaveform(activeChannel, parameters);

                LogMessage($"Applied Dual Tone waveform to CH{activeChannel} with F1={f1Hz}Hz, F2={f2Hz}Hz, " +
                         $"Center={(f1Hz + f2Hz) / 2}Hz, Offset={f2Hz - f1Hz}Hz, " +
                         $"Amp={amplitude} {ampUnit}, Offset={offset}V, Phase={phase}°");
            }
            catch (Exception ex)
            {
                LogMessage($"Error applying dual tone with frequencies: {ex.Message}");
            }
        }

        // Add these timer fields near your other timer declarations
        private DispatcherTimer _centerFrequencyUpdateTimer;
        private DispatcherTimer _offsetFrequencyUpdateTimer;


        #endregion

        #region Harmonics Event Handlers
        // regon for harmonics events
        // Add these methods to MainWindow.xaml.cs

        private void AmplitudeModeChanged(object sender, RoutedEventArgs e)
        {
            // The event is defined in the XAML file to be handled by MainWindow
            // Forward it to the HarmonicsUIController
            if (_harmonicsUIController != null)
            {
                // Use reflection to call the method since it's private in HarmonicsUIController
                typeof(HarmonicsUIController).GetMethod("AmplitudeModeChanged",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(_harmonicsUIController, new object[] { sender, e });
            }
        }

        private void HarmonicAmplitudeUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_harmonicsUIController != null)
            {
                ComboBox comboBox = sender as ComboBox;
                if (comboBox != null && int.TryParse(comboBox.Tag.ToString(), out int harmonicNumber))
                {
                    // Forward the event to the harmonics controller
                    _harmonicsUIController.GetType().GetMethod("HarmonicAmplitudeUnitComboBox_SelectionChanged",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        ?.Invoke(_harmonicsUIController, new object[] { sender, e, harmonicNumber });
                }
            }
        }


        private void HarmonicCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_harmonicsUIController != null)
            {
                // Extract the harmonic number from the Tag property
                CheckBox checkBox = sender as CheckBox;
                if (checkBox != null && int.TryParse(checkBox.Tag.ToString(), out int harmonicNumber))
                {
                    // Use reflection to call the method
                    typeof(HarmonicsUIController).GetMethod("HarmonicCheckBox_Changed",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        .Invoke(_harmonicsUIController, new object[] { sender, e, harmonicNumber });
                }
            }
        }

        private void HarmonicAmplitudeTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_harmonicsUIController != null)
            {
                // Extract the harmonic number from the Tag property
                TextBox textBox = sender as TextBox;
                if (textBox != null && int.TryParse(textBox.Tag.ToString(), out int harmonicNumber))
                {
                    // Use reflection to call the method
                    typeof(HarmonicsUIController).GetMethod("HarmonicAmplitudeTextBox_LostFocus",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        .Invoke(_harmonicsUIController, new object[] { sender, e, harmonicNumber });
                }
            }
        }

        private void HarmonicPhaseTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_harmonicsUIController != null)
            {
                // Extract the harmonic number from the Tag property
                TextBox textBox = sender as TextBox;
                if (textBox != null && int.TryParse(textBox.Tag.ToString(), out int harmonicNumber))
                {
                    // Use reflection to call the method
                    typeof(HarmonicsUIController).GetMethod("HarmonicPhaseTextBox_LostFocus",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        .Invoke(_harmonicsUIController, new object[] { sender, e, harmonicNumber });
                }
            }
        }

        
        // These methods can call the public methods directly
        private void RefreshHarmonicSettings()
        {
            _harmonicsUIController?.RefreshHarmonicSettings();
        }

        private void ResetHarmonicValues()
        {
            _harmonicsUIController?.ResetHarmonicValues();
        }

        private void SetHarmonicUIElementsState(bool enabled)
        {
            _harmonicsUIController?.SetHarmonicUIElementsState(enabled);
        }

        #endregion

        #region Arbitrary Waveform Handlers

        // Fields for arbitrary waveform parameters update timers
        private DispatcherTimer _arbitraryParam1UpdateTimer;
        private DispatcherTimer _arbitraryParam2UpdateTimer;


        // Method to update parameter display based on waveform type
        private void UpdateArbitraryWaveformParameters(string waveformName)
        {
            // This would be customized based on which parameters are relevant for each waveform
            // For this example, we'll keep it simple with generic parameters
            // In a full implementation, you might show/hide specific controls for each waveform type

            // Reset parameters to defaults
            ArbitraryParam1TextBox.Text = "1.0";
            ArbitraryParam2TextBox.Text = "1.0";

            // Set the parameter labels and units based on waveform type
            switch (waveformName.ToUpper())
            {
                case "SINC":
                    WaveformParametersGroup.Visibility = Visibility.Visible;
                    ArbitraryParam1DockPanel.Visibility = Visibility.Visible;
                    ArbitraryParam2DockPanel.Visibility = Visibility.Collapsed;
                    ((Label)ArbitraryParam1DockPanel.Children[0]).Content = "Zero Crossings:";
                    ArbitraryParam1UnitTextBlock.Text = "";
                    break;

                case "GAUSSIAN":
                case "LORENTZ":
                    WaveformParametersGroup.Visibility = Visibility.Visible;
                    ArbitraryParam1DockPanel.Visibility = Visibility.Visible;
                    ArbitraryParam2DockPanel.Visibility = Visibility.Visible;
                    ((Label)ArbitraryParam1DockPanel.Children[0]).Content = "Width:";
                    ((Label)ArbitraryParam2DockPanel.Children[0]).Content = "Center:";
                    ArbitraryParam1UnitTextBlock.Text = "%";
                    ArbitraryParam2UnitTextBlock.Text = "%";
                    break;

                case "EXPONENTIAL RISE":
                case "EXPONENTIAL FALL":
                    WaveformParametersGroup.Visibility = Visibility.Visible;
                    ArbitraryParam1DockPanel.Visibility = Visibility.Visible;
                    ArbitraryParam2DockPanel.Visibility = Visibility.Collapsed;
                    ((Label)ArbitraryParam1DockPanel.Children[0]).Content = "Time Constant:";
                    ArbitraryParam1UnitTextBlock.Text = "%";
                    break;

                case "CHIRP":
                    WaveformParametersGroup.Visibility = Visibility.Visible;
                    ArbitraryParam1DockPanel.Visibility = Visibility.Visible;
                    ArbitraryParam2DockPanel.Visibility = Visibility.Visible;
                    ((Label)ArbitraryParam1DockPanel.Children[0]).Content = "Start Freq:";
                    ((Label)ArbitraryParam2DockPanel.Children[0]).Content = "End Freq:";
                    ArbitraryParam1UnitTextBlock.Text = "Hz";
                    ArbitraryParam2UnitTextBlock.Text = "Hz";
                    break;

                default:
                    // For other waveforms, hide the parameter controls
                    WaveformParametersGroup.Visibility = Visibility.Collapsed;
                    ArbitraryParam1DockPanel.Visibility = Visibility.Collapsed;
                    ArbitraryParam2DockPanel.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        // Event handler for parameter text changes
        private void ArbitraryParamTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isConnected) return;

            TextBox textBox = sender as TextBox;
            if (textBox == null || !double.TryParse(textBox.Text, out double value)) return;

            // Get parameter number from Tag
            if (!int.TryParse(textBox.Tag.ToString(), out int paramNumber)) return;

            // Use a timer to delay the update until user stops typing
            DispatcherTimer timer = null;

            switch (paramNumber)
            {
                case 1:
                    if (_arbitraryParam1UpdateTimer == null)
                    {
                        _arbitraryParam1UpdateTimer = new DispatcherTimer
                        {
                            Interval = TimeSpan.FromMilliseconds(500)
                        };
                        _arbitraryParam1UpdateTimer.Tick += (s, args) =>
                        {
                            _arbitraryParam1UpdateTimer.Stop();
                            if (double.TryParse(ArbitraryParam1TextBox.Text, out double param))
                            {
                                // Update will happen when Apply button is clicked
                                LogMessage($"Parameter 1 set to {param}");
                            }
                        };
                    }
                    timer = _arbitraryParam1UpdateTimer;
                    break;

                case 2:
                    if (_arbitraryParam2UpdateTimer == null)
                    {
                        _arbitraryParam2UpdateTimer = new DispatcherTimer
                        {
                            Interval = TimeSpan.FromMilliseconds(500)
                        };
                        _arbitraryParam2UpdateTimer.Tick += (s, args) =>
                        {
                            _arbitraryParam2UpdateTimer.Stop();
                            if (double.TryParse(ArbitraryParam2TextBox.Text, out double param))
                            {
                                // Update will happen when Apply button is clicked
                                LogMessage($"Parameter 2 set to {param}");
                            }
                        };
                    }
                    timer = _arbitraryParam2UpdateTimer;
                    break;
            }

            if (timer != null)
            {
                timer.Stop();
                timer.Start();
            }
        }

        // Event handler for parameter text box lost focus
        private void ArbitraryParamTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (textBox == null || !double.TryParse(textBox.Text, out double value)) return;

            // Format the value with appropriate number of decimal places
            textBox.Text = FormatWithMinimumDecimals(value);
        }


        // Event handler for when the arbitrary waveform category changes
        private void ArbitraryWaveformCategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Update the available waveforms for the selected category
            LoadArbitraryWaveformsForCategory();
        }

        // Load the ArbitraryWaveformCategory enum values into the category ComboBox
        private void InitializeArbitraryWaveformControls()
        {
            // Clear existing items
            ArbitraryWaveformCategoryComboBox.Items.Clear();

            // Get all categories from the RigolDG2072 instance
            var categories = rigolDG2072.GetArbitraryWaveformCategories();

            // Add each category to the ComboBox
            foreach (var category in categories)
            {
                ArbitraryWaveformCategoryComboBox.Items.Add(category.ToString());
            }

            // Select the first category by default
            if (ArbitraryWaveformCategoryComboBox.Items.Count > 0)
            {
                ArbitraryWaveformCategoryComboBox.SelectedIndex = 0;
            }
        }

        // Load waveforms for the currently selected category
        private void LoadArbitraryWaveformsForCategory()
        {
            // Clear existing items
            ArbitraryWaveformComboBox.Items.Clear();

            // Get the selected category
            if (ArbitraryWaveformCategoryComboBox.SelectedItem == null)
                return;

            // Parse the selected category string back to the enum value
            if (Enum.TryParse(ArbitraryWaveformCategoryComboBox.SelectedItem.ToString(), out RigolDG2072.ArbitraryWaveformCategory selectedCategory))
            {
                // Get waveforms for the selected category
                var waveforms = rigolDG2072.GetArbitraryWaveformNames(selectedCategory);

                // Add each waveform to the ComboBox
                foreach (var waveform in waveforms)
                {
                    ArbitraryWaveformComboBox.Items.Add(waveform);
                }

                // Select the first waveform by default
                if (ArbitraryWaveformComboBox.Items.Count > 0)
                {
                    ArbitraryWaveformComboBox.SelectedIndex = 0;
                }
            }
        }

        // Update the arbitrary waveform info text when a waveform is selected
        private void ArbitraryWaveformComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ArbitraryWaveformComboBox.SelectedItem != null)
            {
                string selectedWaveform = ArbitraryWaveformComboBox.SelectedItem.ToString();
                UpdateArbitraryWaveformParameters(selectedWaveform);
            }
        }

        // Apply the selected arbitrary waveform
        // Apply the selected arbitrary waveform
        // Apply the selected arbitrary waveform
        private void ApplyArbitraryWaveformButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ArbitraryWaveformCategoryComboBox.SelectedItem == null ||
                    ArbitraryWaveformComboBox.SelectedItem == null)
                    return;

                // Get the selected category and waveform
                if (Enum.TryParse(ArbitraryWaveformCategoryComboBox.SelectedItem.ToString(),
                                  out RigolDG2072.ArbitraryWaveformCategory selectedCategory))
                {
                    string selectedArbWaveform = ArbitraryWaveformComboBox.SelectedItem.ToString();

                    // Get current parameters from UI
                    double frequency = GetFrequencyFromUI();
                    double amplitude = GetAmplitudeFromUI();
                    double offset = GetOffsetFromUI();
                    double phase = GetPhaseFromUI();

                    // Apply the arbitrary waveform
                    rigolDG2072.SetArbitraryWaveform(activeChannel, selectedCategory, selectedArbWaveform);

                    // Apply basic parameters
                    rigolDG2072.SetFrequency(activeChannel, frequency);
                    rigolDG2072.SetAmplitude(activeChannel, amplitude);
                    rigolDG2072.SetOffset(activeChannel, offset);
                    rigolDG2072.SetPhase(activeChannel, phase);

                    // Log the operation
                    LogMessage($"Applied {selectedArbWaveform} arbitrary waveform from {selectedCategory} category to Channel {activeChannel}");
                }
            }
            catch (Exception ex)
            {
                // Handle any errors
                LogMessage($"Error applying arbitrary waveform: {ex.Message}");
            }
        }

        private double GetFrequencyFromUI()
        {
            if (double.TryParse(ChannelFrequencyTextBox.Text, out double frequency))
            {
                string freqUnit = Services.UnitConversionUtility.GetFrequencyUnit(ChannelFrequencyUnitComboBox);
                double multiplier = Services.UnitConversionUtility.GetFrequencyMultiplier(freqUnit);
                return frequency * multiplier;
            }
            return 1000.0; // Default 1kHz
        }

        private double GetAmplitudeFromUI()
        {
            if (double.TryParse(ChannelAmplitudeTextBox.Text, out double amplitude))
            {
                string ampUnit = Services.UnitConversionUtility.GetAmplitudeUnit(ChannelAmplitudeUnitComboBox);
                double multiplier = Services.UnitConversionUtility.GetAmplitudeMultiplier(ampUnit);
                return amplitude * multiplier;
            }
            return 1.0; // Default 1Vpp
        }

        private double GetOffsetFromUI()
        {
            if (double.TryParse(ChannelOffsetTextBox.Text, out double offset))
            {
                return offset;
            }
            return 0.0; // Default 0V
        }

        private double GetPhaseFromUI()
        {
            if (double.TryParse(ChannelPhaseTextBox.Text, out double phase))
            {
                return phase;
            }
            return 0.0; // Default 0°
        }

        // Helper method for formatting values with appropriate decimal places
        private string FormatWithMinimumDecimals(double value)
        {
            // Format with at least one decimal place for clarity
            if (Math.Abs(value) < 0.0001)
                return value.ToString("0.0000");
            else if (Math.Abs(value) < 0.001)
                return value.ToString("0.000");
            else if (Math.Abs(value) < 0.01)
                return value.ToString("0.00");
            else if (Math.Abs(value) < 10)
                return value.ToString("0.0");
            else
                return value.ToString("0");
        }

        #endregion


        #region DC Mode Controls

        // All DC mode-specific methods and handlers
        // Add this method to handle DC voltage changes
        private void DCVoltageTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isConnected) return;
            if (!double.TryParse(DCVoltageTextBox.Text, out double voltage)) return;

            if (_dcVoltageUpdateTimer == null)
            {
                _dcVoltageUpdateTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                _dcVoltageUpdateTimer.Tick += (s, args) =>
                {
                    _dcVoltageUpdateTimer.Stop();
                    if (double.TryParse(DCVoltageTextBox.Text, out double volt))
                    {
                        ApplyDCVoltage(volt);
                    }
                };
            }

            _dcVoltageUpdateTimer.Stop();
            _dcVoltageUpdateTimer.Start();
        }

        // Add this method to handle unit changes for DC voltage
        private void DCVoltageUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isConnected) return;
            if (!double.TryParse(DCVoltageTextBox.Text, out double voltage)) return;

            string unitStr = ((ComboBoxItem)DCVoltageUnitComboBox.SelectedItem).Content.ToString();
            double multiplier = unitStr == "mV" ? 0.001 : 1.0;  // Convert mV to V if needed

            ApplyDCVoltage(voltage * multiplier);
        }

        // Add this method to handle impedance changes
        private void DCImpedanceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isConnected) return;

            string impedanceStr = ((ComboBoxItem)DCImpedanceComboBox.SelectedItem).Content.ToString();
            double impedance = 50.0; // Default value

            if (impedanceStr == "High-Z")
            {
                // Set to high impedance
                rigolDG2072.SendCommand($":OUTP{activeChannel}:IMP INF");
            }
            else if (impedanceStr.EndsWith("Ω"))
            {
                // Parse the value from the string
                if (impedanceStr.Contains("k"))
                {
                    // kOhm value
                    if (double.TryParse(impedanceStr.Replace("kΩ", ""), out double kOhms))
                    {
                        impedance = kOhms * 1000;
                    }
                }
                else
                {
                    // Ohm value
                    if (double.TryParse(impedanceStr.Replace("Ω", ""), out double ohms))
                    {
                        impedance = ohms;
                    }
                }

                // Apply the impedance setting
                rigolDG2072.SendCommand($":OUTP{activeChannel}:IMP {impedance}");
            }

            LogMessage($"Set output impedance for channel {activeChannel} to {impedanceStr}");

            // After changing impedance, we need to reapply the DC voltage
            // since the voltage displayed may change based on load impedance
            if (double.TryParse(DCVoltageTextBox.Text, out double voltage))
            {
                string unitStr = ((ComboBoxItem)DCVoltageUnitComboBox.SelectedItem).Content.ToString();
                double multiplier = unitStr == "mV" ? 0.001 : 1.0;
                ApplyDCVoltage(voltage * multiplier);
            }
        }

        // Method to apply DC voltage to the device
        private void ApplyDCVoltage(double voltage)
        {
            if (!isConnected) return;

            try
            {
                // For DC, we use the APPLY:DC command with placeholders for frequency and amplitude
                rigolDG2072.SendCommand($":SOURCE{activeChannel}:APPLY:DC 1,1,{voltage}");
                LogMessage($"Set DC voltage for channel {activeChannel} to {voltage} V");
            }
            catch (Exception ex)
            {
                LogMessage($"Error applying DC voltage: {ex.Message}");
            }
        }

        private void UpdateDCControls()
        {
            // Method to update DC controls based on device settings
        }
        private void HarmonicsToggle_Click(object sender, RoutedEventArgs e)
        {
            if (!isConnected) return;

            bool isEnabled = HarmonicsToggle.IsChecked == true;
            HarmonicsToggle.Content = isEnabled ? "ENABLED" : "DISABLED";

            try
            {
                if (isEnabled)
                {
                    // Enable harmonics on the device
                    rigolDG2072.SetHarmonicState(activeChannel, true);
                    LogMessage($"Harmonics enabled for Channel {activeChannel}");
                }
                else
                {
                    // Disable harmonics on the device
                    rigolDG2072.SetHarmonicState(activeChannel, false);
                    LogMessage($"Harmonics disabled for Channel {activeChannel}");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error toggling harmonics: {ex.Message}");
            }
        }

        #endregion

    }
}


