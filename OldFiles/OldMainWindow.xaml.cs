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
using System.Threading.Channels;

using DG2072_USB_Control.Continuous.Harmonics;
using DG2072_USB_Control.Continuous.PulseGenerator;
using DG2072_USB_Control.Continuous.DualTone;
using DG2072_USB_Control.Continuous.Ramp;
using DG2072_USB_Control.Continuous.Square;
using DG2072_USB_Control.Continuous.Sinusoid;
using DG2072_USB_Control.Continuous.DC;
using DG2072_USB_Control.Continuous.Noise;
using DG2072_USB_Control.Continuous.ArbitraryWaveform;

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
        private DispatcherTimer _primaryFrequencyUpdateTimer;
        private DockPanel SymmetryDockPanel;

        // private DispatcherTimer _dutyCycleUpdateTimer;
        private DockPanel DutyCycleDockPanel;

        // Add this with the other timer declarations in MainWindow.xaml.cs:
       // private DispatcherTimer _secondaryFrequencyUpdateTimer;
        private bool _frequencyModeActive = true; // Default to frequency mode
        private DockPanel PulsePeriodDockPanel;
        private DockPanel PhaseDockPanel;
        private ChannelHarmonicController harmonicController;

        private double frequencyRatio = 2.0; // Default frequency ratio (harmonic)

        private DockPanel DCVoltageDockPanel;
        

        // Harmonics management
        private HarmonicsManager _harmonicsManager;
        private HarmonicsUIController _harmonicsUIController;

        // pulse generator management
        private PulseGen pulseGenerator;

        // Dual Tone management
        private DualToneGen dualToneGen;

        // Ramp generator management
        private RampGen rampGenerator;

        //Square Generator management
        private SquareGen squareGenerator;

        //Sinusoid generator management
        private SinGen sineGenerator;

        //dc generator management
        private DCGen dcGenerator;

        // Noise generator management
        private NoiseGen noiseGenerator;

        // Arbitrary Waveform Generator Management
        private ArbitraryWaveformGen arbitraryWaveformGen;

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

        // Update the channel toggle method to update the pulse generator's active channel
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

                // Update pulse generator with the new active channel
                if (pulseGenerator != null)
                    pulseGenerator.ActiveChannel = activeChannel;

                // update ramp generator with the new active channel
                if (rampGenerator != null)
                    rampGenerator.ActiveChannel = activeChannel;
                // Other component updates...

                // update square generator with the new active channel
                if (squareGenerator != null)
                    squareGenerator.ActiveChannel = activeChannel;

                // update sine generator with the new active channel
                if (sineGenerator != null)
                    sineGenerator.ActiveChannel = activeChannel;

                // Update DC generator with the new active channel
                if (dcGenerator != null)
                    dcGenerator.ActiveChannel = activeChannel;

                //update noise generator with the new active channel
                if (noiseGenerator != null)
                    noiseGenerator.ActiveChannel = activeChannel;

                // Update arbitrary waveform generator with the new active channel
                if (arbitraryWaveformGen != null)
                    arbitraryWaveformGen.ActiveChannel = activeChannel;

            }
            else
            {
                // Switch to Channel 2
                activeChannel = 2;
                ChannelToggleButton.Content = "Channel 2";
                ActiveChannelTextBlock.Text = "Channel 2";
                ChannelControlsGroupBox.Header = "Channel 2 Controls";

                // Update pulse generator with the new active channel
                if (pulseGenerator != null)
                    pulseGenerator.ActiveChannel = activeChannel;

                // update ramp generator with the new active channel
                if (rampGenerator != null)
                    rampGenerator.ActiveChannel = activeChannel;
                // Other component updates...

                // Update square generator with the new active channel
                if (squareGenerator != null)
                    squareGenerator.ActiveChannel = activeChannel;

            }

            // Refresh the UI to show current settings for the selected channel
            if (isConnected)
            {
                RefreshChannelSettings();
            }
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
                if (waveform == "SINE" && sineGenerator != null)
                {
                    sineGenerator.RefreshParameters(); // New way using base class method
                }

                // Special handling for DC waveform
                if (waveform == "DC" && dcGenerator != null)
                {
                    // Delegate to DC generator for handling DC-specific settings
                    dcGenerator.RefreshParameters(); // Updated to use base class method

                    // Update output state
                    UpdateOutputState(ChannelOutputToggle, activeChannel);
                }

                // Special handling for PULSE waveform - delegate to pulseGenerator
                else if (waveform == "PULSE" && pulseGenerator != null)
                {
                    // Update common parameters
                    if (_frequencyModeActive)
                    {
                        UpdateFrequencyValue(ChannelFrequencyTextBox, ChannelFrequencyUnitComboBox, activeChannel);
                    }
                    else
                    {
                        UpdatePeriodValue(PulsePeriod, PulsePeriodUnitComboBox, activeChannel);
                    }

                    // Update amplitude, offset, phase and output state
                    UpdateAmplitudeValue(ChannelAmplitudeTextBox, ChannelAmplitudeUnitComboBox, activeChannel);
                    UpdateOffsetValue(ChannelOffsetTextBox, activeChannel);
                    UpdatePhaseValue(ChannelPhaseTextBox, activeChannel);
                    UpdateOutputState(ChannelOutputToggle, activeChannel);

                    // Delegate pulse-specific parameter updates to the pulse generator
                    pulseGenerator.UpdatePulseParameters(activeChannel);

                    // Ensure pulse-specific controls are visible
                    pulseGenerator.UpdatePulseControls(true);
                }

                // Handle other non-DC waveforms
                else
                {


                    // Update frequency/period based on current mode
                    if (_frequencyModeActive)
                    {
                        UpdateFrequencyValue(ChannelFrequencyTextBox, ChannelFrequencyUnitComboBox, activeChannel);
                    }
                    else
                    {
                        UpdatePeriodValue(PulsePeriod, PulsePeriodUnitComboBox, activeChannel);
                    }

                    // Update common parameters for all non-DC waveforms
                    UpdateAmplitudeValue(ChannelAmplitudeTextBox, ChannelAmplitudeUnitComboBox, activeChannel);
                    UpdateOffsetValue(ChannelOffsetTextBox, activeChannel);
                    UpdatePhaseValue(ChannelPhaseTextBox, activeChannel);
                    UpdateOutputState(ChannelOutputToggle, activeChannel);

                    // Add handling for RAMP waveform
                    if (waveform == "RAMP" && rampGenerator != null)
                    {
                        // Delegate to the ramp generator
                        rampGenerator.ApplyParameters();
                    }

                    else if (waveform == "SQUARE" && squareGenerator != null)
                    {
                        squareGenerator.UpdateDutyCycleValue();
                    }
                    else if (waveform == "HARMONIC" && _harmonicsUIController != null)
                    {
                        _harmonicsUIController.RefreshHarmonicSettings();
                    }
                    else if (waveform == "NOISE" && noiseGenerator != null)
                    {
                        // Delegate to the noise generator for handling NOISE-specific settings
                        noiseGenerator.RefreshParameters();

                        // Update output state
                        UpdateOutputState(ChannelOutputToggle, activeChannel);
                    }
                    else if (waveform == "DUAL TONE")
                    {
                        RefreshDualToneSettings(activeChannel);
                    }
                    else if (waveform == "USER" || waveform == "ARBITRARY WAVEFORM")
                    {
                        RefreshArbitraryWaveformSettings(activeChannel);
                    }
                }

                // Update UI controls visibility based on the selected waveform
                UpdateWaveformSpecificControls(waveform);

                LogMessage($"Refreshed Channel {activeChannel} settings");
            }
            catch (Exception ex)
            {
                LogMessage($"Error refreshing Channel {activeChannel} settings: {ex.Message}");
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

                // First refresh basic channel settings - this updates waveform type first
                RefreshChannelSettings();

                // Now get the currently selected waveform from the UI
                string currentWaveform = ((ComboBoxItem)ChannelWaveformComboBox.SelectedItem).Content.ToString().ToUpper();

                // For USER/ARBITRARY WAVEFORM, refresh arbitrary settings
                if (currentWaveform == "USER" || currentWaveform == "ARBITRARY WAVEFORM")
                {
                    // Make sure the arbitrary waveform group is visible
                    ArbitraryWaveformGroupBox.Visibility = Visibility.Visible;

                    // Refresh arbitrary waveform specific settings
                    RefreshArbitraryWaveformSettings(activeChannel);
                }
                // For HARMONIC waveform, refresh harmonic settings
                else if (currentWaveform == "HARMONIC")
                {
                    // Initialize harmonicController if needed
                    if (harmonicController == null)
                    {
                        harmonicController = new ChannelHarmonicController(rigolDG2072, activeChannel);
                    }

                    // Make sure the harmonics group is visible
                    if (HarmonicsGroupBox != null)
                    {
                        HarmonicsGroupBox.Visibility = Visibility.Visible;
                    }

                    // Refresh harmonic settings
                    RefreshHarmonicSettings();
                }
                // For DUAL TONE waveform, refresh dual tone settings
                else if (currentWaveform == "DUAL TONE" || currentWaveform == "DUALTONE")
                {
                    // Make sure the dual tone group is visible
                    if (DualToneGroupBox != null)
                    {
                        DualToneGroupBox.Visibility = Visibility.Visible;
                    }

                    // Refresh dual tone specific settings
                    RefreshDualToneSettings(activeChannel);
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

                    periodTextBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(displayValue);
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
                if (currentWaveform == "USER") currentWaveform = "ARBITRARY WAVEFORM";
                if (currentWaveform == "HARM") currentWaveform = "HARMONIC";
                if (currentWaveform == "DUAL") currentWaveform = "DUAL TONE";

                // Check if this is an arbitrary waveform
                bool isArbitraryWaveform = false;
                var waveformInfo = rigolDG2072.FindArbitraryWaveformByScpiCommand(currentWaveform);

                if (waveformInfo.HasValue)
                {
                    isArbitraryWaveform = true;
                    LogMessage($"Detected arbitrary waveform: {waveformInfo.Value.FriendlyName} from category {waveformInfo.Value.Category}");

                    // Store the detected waveform info for later use in ArbitraryWaveformGen
                    rigolDG2072.LastDetectedArbitraryWaveform = waveformInfo.Value;
                }

                // If it's an arbitrary waveform but not already recognized as USER/ARBITRARY WAVEFORM, 
                // treat it as ARBITRARY WAVEFORM for UI selection
                if (isArbitraryWaveform && currentWaveform != "ARBITRARY WAVEFORM" && currentWaveform != "USER")
                {
                    currentWaveform = "ARBITRARY WAVEFORM";
                }

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

                    // Special handling for USER/ARBITRARY WAVEFORM
                    // If currentWaveform is an arbitrary waveform and we haven't found it yet,
                    // try to select the "Arbitrary Waveform" option
                    if (!found && isArbitraryWaveform)
                    {
                        foreach (ComboBoxItem item in waveformComboBox.Items)
                        {
                            if (item.Content.ToString().ToUpper() == "ARBITRARY WAVEFORM")
                            {
                                waveformComboBox.SelectedItem = item;
                                found = true;
                                LogMessage($"Selected Arbitrary Waveform for {currentWaveform}");
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

                    // If it's an arbitrary waveform, refresh the arbitrary waveform settings
                    // This must happen AFTER the main waveform type is selected in the UI
                    if (currentWaveform == "ARBITRARY WAVEFORM" || isArbitraryWaveform)
                    {
                        LogMessage("Refreshing arbitrary waveform settings based on detected waveform");
                        RefreshArbitraryWaveformSettings(channel);
                    }
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

                    freqTextBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(displayValue);
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

                    ampTextBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(displayValue);
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
                    offsetTextBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(offset);
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
                    phaseTextBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(phase);
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
            PulsePeriodDockPanel = FindVisualParent<DockPanel>(PulsePeriod);

            PulseRiseTimeDockPanel = FindVisualParent<DockPanel>(PulseRiseTime);
            PulseFallTimeDockPanel = FindVisualParent<DockPanel>(PulseFallTime);

            // Get references to the main frequency panel and calculated rate panel
            FrequencyDockPanel = FindVisualParent<DockPanel>(ChannelFrequencyTextBox);

            // Find and store reference to phase panel
            PhaseDockPanel = FindVisualParent<DockPanel>(ChannelPhaseTextBox);

            // Initialize frequency/period mode with frequency mode active by default
            _frequencyModeActive = true;
            FrequencyPeriodModeToggle.IsChecked = true;
            FrequencyPeriodModeToggle.Content = "To Period";

            // Initialize the pulse generator after UI references are set up
            pulseGenerator = new PulseGen(rigolDG2072, activeChannel, this);
            pulseGenerator.LogEvent += (s, message) => LogMessage(message);

            // Initialize the dual tone generator after UI references are set up
            dualToneGen = new DualToneGen(rigolDG2072, activeChannel, this);
            dualToneGen.LogEvent += (s, message) => LogMessage(message);

            // Initialize the ramp generator after UI references are set up
            rampGenerator = new RampGen(rigolDG2072, activeChannel, this);
            rampGenerator.LogEvent += (s, message) => LogMessage(message);

            // Initialize the square generator after UI references are set up
            squareGenerator = new SquareGen(rigolDG2072, activeChannel, this);
            squareGenerator.LogEvent += (s, message) => LogMessage(message);

            // Initialize the sine generator after UI references are set up
            sineGenerator = new SinGen(rigolDG2072, activeChannel, this);
            sineGenerator.LogEvent += (s, message) => LogMessage(message);

            // Initialize the DC generator after UI references are set up
            dcGenerator = new DCGen(rigolDG2072, activeChannel, this);
            dcGenerator.LogEvent += (s, message) => LogMessage(message);

            // initialize the noise generator after UI references are set up
            noiseGenerator = new NoiseGen(rigolDG2072, activeChannel, this);
            noiseGenerator.LogEvent += (s, message) => LogMessage(message);

            // Initialize the arbitrary waveform generator
            arbitraryWaveformGen = new ArbitraryWaveformGen(rigolDG2072, activeChannel, this);
            arbitraryWaveformGen.LogEvent += (s, message) => LogMessage(message);

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

                    // Add this line to refresh settings on manual connection
                    RefreshInstrumentSettings();
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



        private void PrimaryFrequencyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isConnected) return;
            if (!double.TryParse(PrimaryFrequencyTextBox.Text, out double frequency)) return;

            // Use a timer to debounce rapid changes
            if (_primaryFrequencyUpdateTimer == null)
            {
                _primaryFrequencyUpdateTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                _primaryFrequencyUpdateTimer.Tick += (s, args) =>
                {
                    _primaryFrequencyUpdateTimer.Stop();
                    if (double.TryParse(PrimaryFrequencyTextBox.Text, out double freq))
                    {
                        // Only update if in dual tone mode
                        if (((ComboBoxItem)ChannelWaveformComboBox.SelectedItem).Content.ToString().ToUpper() == "DUAL TONE")
                        {
                            // Update SecondaryFrequencyTextBox if auto-sync is enabled
                            if (SynchronizeFrequenciesCheckBox.IsChecked == true)
                            {
                                double secondaryFreq = freq * frequencyRatio;
                                SecondaryFrequencyTextBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(secondaryFreq);
                            }

                            // Apply the dual tone settings
                            if (dualToneGen != null)
                                dualToneGen.ApplyDualToneParameters();
                        }
                    }
                };
            }

            _primaryFrequencyUpdateTimer.Stop();
            _primaryFrequencyUpdateTimer.Start();
        }

        private void PrimaryFrequencyUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isConnected) return;

            if (double.TryParse(PrimaryFrequencyTextBox.Text, out double frequency))
            {
                // Only update if in dual tone mode
                if (((ComboBoxItem)ChannelWaveformComboBox.SelectedItem).Content.ToString().ToUpper() == "DUAL TONE")
                {
                    if (dualToneGen != null)
                        dualToneGen.ApplyDualToneParameters();
                }
            }
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

        private void ChannelOffsetUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isConnected) return;

            if (double.TryParse(ChannelOffsetTextBox.Text, out double offset))
            {
                ApplyOffset(offset);
            }
        }

        private void ApplyOffset(double offset)
        {
            if (!isConnected) return;

            try
            {
                string unit = UnitConversionUtility.GetOffsetUnit(ChannelOffsetUnitComboBox);
                double multiplier = UnitConversionUtility.GetOffsetMultiplier(unit);
                double actualOffset = offset * multiplier;

                rigolDG2072.SetOffset(activeChannel, actualOffset);
                LogMessage($"Set CH{activeChannel} offset to {offset} {unit} ({actualOffset} V)");
            }
            catch (Exception ex)
            {
                LogMessage($"Error applying offset: {ex.Message}");
            }
        }

        private void AdjustOffsetAndUnit(TextBox textBox, ComboBox unitComboBox)
        {
            if (!double.TryParse(textBox.Text, out double value))
                return;

            string currentUnit = ((ComboBoxItem)unitComboBox.SelectedItem).Content.ToString();

            // Convert to appropriate unit
            if (Math.Abs(value) < 0.1 && currentUnit == "V")
            {
                // Switch to mV for small values
                value *= 1000.0;
                for (int i = 0; i < unitComboBox.Items.Count; i++)
                {
                    ComboBoxItem item = unitComboBox.Items[i] as ComboBoxItem;
                    if (item != null && item.Content.ToString() == "mV")
                    {
                        unitComboBox.SelectedIndex = i;
                        break;
                    }
                }
            }
            else if (Math.Abs(value) > 1000.0 && currentUnit == "mV")
            {
                // Switch to V for large values
                value /= 1000.0;
                for (int i = 0; i < unitComboBox.Items.Count; i++)
                {
                    ComboBoxItem item = unitComboBox.Items[i] as ComboBoxItem;
                    if (item != null && item.Content.ToString() == "V")
                    {
                        unitComboBox.SelectedIndex = i;
                        break;
                    }
                }
            }

            // Format with minimum decimals
            textBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(value, 1); // 1 decimal for frequency
        }

        private void ChannelWaveformComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isConnected) return;

            // Get previous waveform type (if available)
            string previousWaveform = string.Empty;
            if (e.RemovedItems.Count > 0 && e.RemovedItems[0] is ComboBoxItem)
            {
                previousWaveform = ((ComboBoxItem)e.RemovedItems[0]).Content.ToString().ToUpper();
            }

            string waveform = ((ComboBoxItem)ChannelWaveformComboBox.SelectedItem).Content.ToString().ToUpper();
            string selectedArbWaveform = ((ComboBoxItem)ChannelWaveformComboBox.SelectedItem).Content.ToString();

            // If leaving Dual Tone mode, set frequency to F1 instead of center
            if (previousWaveform == "DUAL TONE" && waveform != "DUAL TONE")
            {
                try
                {
                    // If we can get center and offset, we can calculate F1
                    if (CenterFrequencyTextBox != null && OffsetFrequencyTextBox != null &&
                        double.TryParse(CenterFrequencyTextBox.Text, out double center) &&
                        double.TryParse(OffsetFrequencyTextBox.Text, out double offset))
                    {
                        // Calculate F1 using: F1 = Center - Offset
                        double f1 = center - offset;

                        // Update the frequency setting
                        ChannelFrequencyTextBox.Text = f1.ToString();

                        // Log for debugging
                        LogMessage($"Switching from Dual Tone: Set frequency to F1 ({f1} Hz) instead of center ({center} Hz)");
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"Error setting F1 frequency when switching from Dual Tone: {ex.Message}");
                }
            }

            // Special handling for HARMONIC waveform
            if (waveform == "HARMONIC")
            {
                LogMessage("Switching to HARMONIC waveform mode...");

                try
                {
                    // Then enable harmonic mode
                    rigolDG2072.SendCommand($":SOUR{activeChannel}:HARM:STAT ON");
                    System.Threading.Thread.Sleep(100);

                    // Get current parameters
                    double frequency = rigolDG2072.GetFrequency(activeChannel);
                    double amplitude = rigolDG2072.GetAmplitude(activeChannel);

                    // Use the current SINE settings
                    rigolDG2072.SendCommand($":SOURCE{activeChannel}:APPLY:SIN {frequency},{amplitude},{0},{0}");
                    System.Threading.Thread.Sleep(100);  // Give device time to process

                    // Initialize harmonicController if needed
                    if (harmonicController == null)
                    {
                        harmonicController = new ChannelHarmonicController(rigolDG2072, activeChannel);
                        LogMessage($"Initialized harmonic controller for Channel {activeChannel}");
                    }

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
                        SecondaryFrequencyTextBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(secondaryFreq);
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
            else if (waveform == "ARBITRARY WAVEFORM")
            {
                LogMessage("Switching to arbitrary waveform mode...");
                try
                {
                    // Get current parameters - just like the sine wave implementation
                    double frequency = rigolDG2072.GetFrequency(activeChannel);
                    double amplitude = rigolDG2072.GetAmplitude(activeChannel);
                    double offset = rigolDG2072.GetOffset(activeChannel);
                    double phase = rigolDG2072.GetPhase(activeChannel);

                    // Set the waveform on the device using current parameters instead of hardcoded values
                    rigolDG2072.SendCommand($":SOURCE{activeChannel}:APPLY:USER {frequency},{amplitude},{offset},{phase}");
                    System.Threading.Thread.Sleep(100);

                    // Initialize the arbitrary waveform UI
                    if (ArbitraryWaveformComboBox.SelectedItem == null)
                    {
                        // Refresh the arbitrary waveform settings 
                        RefreshArbitraryWaveformSettings(activeChannel);
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
            else if (waveform == "DC" && dcGenerator != null)
            {
                LogMessage("Switching to DC waveform mode...");
                try
                {
                    // Delegate to the DC generator
                    dcGenerator.ApplyParameters();

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

            // Add the sine generator code here
            else if (waveform == "SINE" && sineGenerator != null)
            {
                sineGenerator.ApplyParameters(); // New way using base class method
            }

            // Add the Noise generator code here
            else if (waveform == "NOISE" && noiseGenerator != null)
            {
                // Delegate to the noise generator
                noiseGenerator.ApplyParameters();
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


        // Modify the ChannelFrequencyTextBox_TextChanged method to update 
        // dual tone center frequency when in Center/Offset mode

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

            // Check if this is in DUAL TONE mode with Center/Offset mode active
            if (isConnected &&
                ((ComboBoxItem)ChannelWaveformComboBox.SelectedItem).Content.ToString().ToUpper() == "DUAL TONE")
            {
                if (CenterOffsetMode.IsChecked == true)
                {
                    // In Center/Offset mode, update offset calculations directly
                    // This will trigger the UpdateFrequenciesFromCenterOffset method
                    if (dualToneGen != null)
                    {
                        dualToneGen.UpdateFrequenciesFromCenterOffset();
                    }
                }
                else if (SynchronizeFrequenciesCheckBox.IsChecked == true &&
                        double.TryParse(ChannelFrequencyTextBox.Text, out double primaryFreq))
                {
                    // Update secondary frequency to maintain the ratio
                    double secondaryFreq = primaryFreq * frequencyRatio;
                    SecondaryFrequencyTextBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(secondaryFreq);
                }
            }
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
                        ApplyOffset(off);
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
            if (rampGenerator != null)
                rampGenerator.OnSymmetryTextChanged(sender, e);
        }

        private void ChannelSymmetryTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (rampGenerator != null)
                rampGenerator.OnSymmetryLostFocus(sender, e);
        }

        private void ChannelApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isConnected) return;

            try
            {
                // Get current waveform type
                string waveform = ((ComboBoxItem)ChannelWaveformComboBox.SelectedItem).Content.ToString().ToUpper();

                // Handle waveform-specific parameters first
                if (waveform == "PULSE" && pulseGenerator != null)
                {
                    pulseGenerator.ApplyParameters();
                }
                else
                {
                    // Apply common parameters
                    ApplyCommonParameters();

                    // Apply waveform-specific parameters
                    ApplyWaveformSpecificParameters(waveform);
                }

                // Refresh the UI to show the actual values from the device
                RefreshChannelSettings();
                LogMessage($"Applied settings to CH{activeChannel}");
            }
            catch (Exception ex)
            {
                LogMessage($"Error applying settings: {ex.Message}");
                MessageBox.Show($"Error applying settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyCommonParameters()
        {
            // First adjust the values and units for better display
            if (_frequencyModeActive)
            {
                AdjustFrequencyAndUnit(ChannelFrequencyTextBox, ChannelFrequencyUnitComboBox);
            }
            else if (pulseGenerator != null)
            {
                pulseGenerator.AdjustPulseTimeAndUnit(PulsePeriod, PulsePeriodUnitComboBox);
            }

            AdjustAmplitudeAndUnit(ChannelAmplitudeTextBox, ChannelAmplitudeUnitComboBox);

            // Apply frequency or period
            ApplyFrequencyOrPeriod();

            // Apply amplitude, offset, phase
            ApplyAmplitudeOffsetPhase();
        }

        private void ApplyFrequencyOrPeriod()
        {
            if (_frequencyModeActive)
            {
                if (!double.TryParse(ChannelFrequencyTextBox.Text, out double frequency))
                {
                    LogMessage($"Invalid frequency value for CH{activeChannel}");
                    return;
                }

                string freqUnit = UnitConversionUtility.GetFrequencyUnit(ChannelFrequencyUnitComboBox);
                double freqMultiplier = UnitConversionUtility.GetFrequencyMultiplier(freqUnit);
                double actualFrequency = frequency * freqMultiplier;

                rigolDG2072.SetFrequency(activeChannel, actualFrequency);
            }
            else
            {
                if (!double.TryParse(PulsePeriod.Text, out double period))
                {
                    LogMessage($"Invalid period value for CH{activeChannel}");
                    return;
                }

                string periodUnit = UnitConversionUtility.GetPeriodUnit(PulsePeriodUnitComboBox);
                double periodMultiplier = UnitConversionUtility.GetPeriodMultiplier(periodUnit);
                double actualPeriod = period * periodMultiplier;

                rigolDG2072.SendCommand($"SOURCE{activeChannel}:PERiod {actualPeriod}");
            }
        }

        private void ApplyAmplitudeOffsetPhase()
        {
            if (!double.TryParse(ChannelAmplitudeTextBox.Text, out double amplitude) ||
                !double.TryParse(ChannelOffsetTextBox.Text, out double offset) ||
                !double.TryParse(ChannelPhaseTextBox.Text, out double phase))
            {
                LogMessage("Invalid amplitude, offset, or phase values");
                return;
            }

            string ampUnit = UnitConversionUtility.GetAmplitudeUnit(ChannelAmplitudeUnitComboBox);
            double ampMultiplier = UnitConversionUtility.GetAmplitudeMultiplier(ampUnit);
            double actualAmplitude = amplitude * ampMultiplier;

            rigolDG2072.SetAmplitude(activeChannel, actualAmplitude);
            rigolDG2072.SetOffset(activeChannel, offset);
            rigolDG2072.SetPhase(activeChannel, phase);
        }

        private void ApplyWaveformSpecificParameters(string waveform)
        {
            switch (waveform)
            {
                case "RAMP":
                    if (rampGenerator != null)
                        rampGenerator.ApplyParameters();
                    break;

                case "SQUARE":
                    if (squareGenerator != null)
                        squareGenerator.ApplySquareParameters();
                    break;
            }
        }



        #endregion

        #region Pulse Parameter Handling

        private void ChannelPulsePeriodTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (pulseGenerator != null)
                pulseGenerator.OnPulsePeriodTextChanged(sender, e);
        }

        private void ChannelPulsePeriodTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (pulseGenerator != null)
                pulseGenerator.OnPulsePeriodLostFocus(sender, e);
        }

        private void PulsePeriodUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (pulseGenerator != null)
                pulseGenerator.OnPulsePeriodUnitChanged(sender, e);
        }

        private void PulseRateModeToggle_Click(object sender, RoutedEventArgs e)
        {
            if (pulseGenerator != null)
                pulseGenerator.OnPulseRateModeToggleClicked(sender, e);
        }

        private void ChannelPulseRiseTimeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (pulseGenerator != null)
                pulseGenerator.OnPulseRiseTimeTextChanged(sender, e);
        }

        private void ChannelPulseRiseTimeTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (pulseGenerator != null)
                pulseGenerator.OnPulseRiseTimeLostFocus(sender, e);
        }

        private void PulseRiseTimeUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (pulseGenerator != null)
                pulseGenerator.OnPulseRiseTimeUnitChanged(sender, e);
        }

        private void ChannelPulseWidthTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (pulseGenerator != null)
                pulseGenerator.OnPulseWidthTextChanged(sender, e);
        }

        private void ChannelPulseWidthTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (pulseGenerator != null)
                pulseGenerator.OnPulseWidthLostFocus(sender, e);
        }

        private void PulseWidthUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (pulseGenerator != null)
                pulseGenerator.OnPulseWidthUnitChanged(sender, e);
        }

        private void ChannelPulseFallTimeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (pulseGenerator != null)
                pulseGenerator.OnPulseFallTimeTextChanged(sender, e);
        }

        private void ChannelPulseFallTimeTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (pulseGenerator != null)
                pulseGenerator.OnPulseFallTimeLostFocus(sender, e);
        }

        private void PulseFallTimeUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (pulseGenerator != null)
                pulseGenerator.OnPulseFallTimeUnitChanged(sender, e);
        }

        #endregion

        #region Unit Selection Handlers

        private void ChannelFrequencyUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isConnected) return;

            if (double.TryParse(ChannelFrequencyTextBox.Text, out double frequency))
            {
                ApplyFrequency(frequency);
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

        #endregion

        #region UI Formatting and Adjustment Methods

        // Update the UpdateWaveformSpecificControls method to use the pulse generator
        private void UpdateWaveformSpecificControls(string waveformType)
        {
            // Convert to uppercase for case-insensitive comparison
            string waveform = waveformType.ToUpper();

            // Handle both "USER" and "ARBITRARY WAVEFORM" as the same thing
            if (waveform == "USER" || waveform == "ARBITRARY WAVEFORM")
                waveform = "ARBITRARY WAVEFORMS";

            bool isPulse = (waveform == "PULSE");
            bool isNoise = (waveform == "NOISE");
            bool isDualTone = (waveform == "DUAL TONE");
            bool isHarmonic = (waveform == "HARMONIC");
            bool isDC = (waveform == "DC");
            bool isArbitraryWaveform = (waveform == "ARBITRARY WAVEFORMS");

            // Use the pulse generator to update pulse controls
            if (pulseGenerator != null)
            {
                pulseGenerator.UpdatePulseControls(isPulse);
            }

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
                PulseFallTimeDockPanel.Visibility = pulseVisibility;

                // Show/hide the mode toggle
                if (PulseRateModeDockPanel != null)
                    PulseRateModeDockPanel.Visibility = pulseVisibility;
            }

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
                // In the UpdateWaveformSpecificControls method
                if (isDualTone)
                {
                    // Hide main frequency controls when in dual tone mode
                    if (FrequencyDockPanel != null)
                        FrequencyDockPanel.Visibility = Visibility.Collapsed;

                    if (PeriodDockPanel != null)
                        PeriodDockPanel.Visibility = Visibility.Collapsed;

                    // Make sure dual tone controls are visible
                    if (DualToneGroupBox != null)
                        DualToneGroupBox.Visibility = Visibility.Visible;
                }
            }                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   
                if (FrequencyDockPanel != null && FrequencyPeriodModeToggle != null && PulseRateModeToggle != null)
            {
                if (FrequencyDockPanel.Visibility == Visibility.Collapsed &&
                    PeriodDockPanel.Visibility == Visibility.Collapsed)
                {
                    FrequencyPeriodModeToggle.Visibility = Visibility.Collapsed;
                    PulseRateModeToggle.Visibility = Visibility.Collapsed;
                    LogMessage($"Setting toggle buttons visibility to Collapsed because both panels are hidden");
                }
                else
                {
                    FrequencyPeriodModeToggle.Visibility = Visibility.Visible;
                    PulseRateModeToggle.Visibility = Visibility.Visible;
                    LogMessage($"Setting toggle buttons visibility to Visible because at least one panel is visible");
                }
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

            // Handle harmonic-specific controls
            if (HarmonicsGroupBox != null)
            {
                HarmonicsGroupBox.Visibility = isHarmonic ? Visibility.Visible : Visibility.Collapsed;
            }

            // Handle arbitrary waveform controls visibility
            if (ArbitraryWaveformGroupBox != null)
            {
                ArbitraryWaveformGroupBox.Visibility = isArbitraryWaveform ? Visibility.Visible : Visibility.Collapsed;

                // Hide the Apply button since changes are auto-applied
                if (ApplyArbitraryWaveformButton != null)
                {
                    ApplyArbitraryWaveformButton.Visibility = Visibility.Collapsed;
                }
            }

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
            if (squareGenerator != null)
                squareGenerator.OnDutyCycleTextChanged(sender, e);
        }

        private void ChannelDutyCycleTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (squareGenerator != null)
                squareGenerator.OnDutyCycleLostFocus(sender, e);
        }

        /// <summary>
        /// Updates UI elements based on the selected frequency/period mode
        /// </summary>
        private void UpdateFrequencyPeriodMode()
        {
            if (!isConnected) return;

            string currentWaveform = ((ComboBoxItem)ChannelWaveformComboBox.SelectedItem).Content.ToString().ToUpper();
            bool isNoise = (currentWaveform == "NOISE"); // Noise doesn't use frequency or period
            bool isDualTone = (currentWaveform == "DUAL TONE"); // Dual tone uses its own controls

            // Hide main frequency controls if in dual tone mode
            if (isDualTone)
            {
                FrequencyDockPanel.Visibility = Visibility.Collapsed;
                PeriodDockPanel.Visibility = Visibility.Collapsed;
                return;
            }

            // Toggle visibility of panels based on selected mode for other waveforms
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
                            PulsePeriod.Text = UnitConversionUtility.FormatWithMinimumDecimals(displayValue);
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
                            ChannelFrequencyTextBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(displayValue);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error updating calculated rate value: {ex.Message}");
            }
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
            textBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(value, 1); // 1 decimal for frequency
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
                ChannelFrequencyTextBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(displayValue);
            }
        }

        private void ChannelAmplitudeTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(ChannelAmplitudeTextBox.Text, out double amplitude))
            {
                ChannelAmplitudeTextBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(amplitude);
            }
        }

        private void ChannelOffsetTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(ChannelOffsetTextBox.Text, out double offset))
            {
                AdjustOffsetAndUnit(ChannelOffsetTextBox, ChannelOffsetUnitComboBox);
            }
        }

        private void ChannelPhaseTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(ChannelPhaseTextBox.Text, out double phase))
            {
                ChannelPhaseTextBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(phase);
            }
        }

        #endregion

        #region DualTone Event Handlers

        private void DualToneModeChanged(object sender, RoutedEventArgs e)
        {
            if (dualToneGen != null)
                dualToneGen.OnDualToneModeChanged(sender, e);
        }

        private void SynchronizeFrequenciesCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (dualToneGen != null)
                dualToneGen.OnSynchronizeFrequenciesCheckChanged(sender, e);
        }

        private void FrequencyRatioComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dualToneGen != null)
                dualToneGen.OnFrequencyRatioSelectionChanged(sender, e);
        }

        private void SecondaryFrequencyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (dualToneGen != null)
                dualToneGen.OnSecondaryFrequencyTextChanged(sender, e);
        }

        private void SecondaryFrequencyTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (dualToneGen != null)
                dualToneGen.OnSecondaryFrequencyLostFocus(sender, e);
        }

        private void SecondaryFrequencyUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dualToneGen != null)
                dualToneGen.OnSecondaryFrequencyUnitChanged(sender, e);
        }

        private void CenterFrequencyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (dualToneGen != null)
                dualToneGen.OnCenterFrequencyTextChanged(sender, e);
        }

        private void CenterFrequencyTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (dualToneGen != null)
                dualToneGen.OnCenterFrequencyLostFocus(sender, e);
        }

        private void CenterFrequencyUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dualToneGen != null)
                dualToneGen.OnCenterFrequencyUnitChanged(sender, e);
        }

        private void OffsetFrequencyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (dualToneGen != null)
                dualToneGen.OnOffsetFrequencyTextChanged(sender, e);
        }

        private void OffsetFrequencyTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (dualToneGen != null)
                dualToneGen.OnOffsetFrequencyLostFocus(sender, e);
        }

        private void OffsetFrequencyUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dualToneGen != null)
                dualToneGen.OnOffsetFrequencyUnitChanged(sender, e);
        }


        private void RefreshDualToneSettings(int channel)
        {
            if (dualToneGen != null)
            {
                dualToneGen.ActiveChannel = channel;
                dualToneGen.RefreshDualToneSettings();
            }
        }

        // This method delegates to the DualToneGen instance
        private void ApplyDualToneParameters()
        {
            if (dualToneGen != null)
                dualToneGen.ApplyDualToneParameters();
        }

        #endregion

        #region Harmonics Event Handlers
        
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

        private void RefreshHarmonicSettings()
        {
            if (_harmonicsUIController != null)
            {
                _harmonicsUIController.RefreshHarmonicSettings();
            }
        }

        private void ResetHarmonicValues()
        {
            _harmonicsUIController?.ResetHarmonicValues();
        }

        private void SetHarmonicUIElementsState(bool enabled)
        {
            _harmonicsUIController?.SetHarmonicUIElementsState(enabled);
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

        #region Arbitrary Waveform Handlers

        // Event handler for parameter text changes
        private void ArbitraryParamTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (arbitraryWaveformGen != null)
                arbitraryWaveformGen.OnParameterTextChanged(sender, e);
        }

        // Event handler for parameter text box lost focus
        private void ArbitraryParamTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (arbitraryWaveformGen != null)
                arbitraryWaveformGen.OnParameterLostFocus(sender, e);
        }

        // Event handler for when the arbitrary waveform category changes
        private void ArbitraryWaveformCategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (arbitraryWaveformGen != null)
                arbitraryWaveformGen.OnCategorySelectionChanged(sender, e);
        }

        // Update the arbitrary waveform info text when a waveform is selected
        private void ArbitraryWaveformComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (arbitraryWaveformGen != null)
                arbitraryWaveformGen.OnWaveformSelectionChanged(sender, e);
        }

        private void RefreshArbitraryWaveformSettings(int channel)
        {
            if (arbitraryWaveformGen != null)
            {
                arbitraryWaveformGen.ActiveChannel = channel;
                arbitraryWaveformGen.RefreshParameters(); // Now uses the base class method
            }
        }

        private void ApplyArbitraryWaveformButton_Click(object sender, RoutedEventArgs e)
        {
            if (arbitraryWaveformGen != null)
                arbitraryWaveformGen.ApplyParameters(); // Now uses the base class method
        }

        #endregion

        #region DC Mode Controls

        // All DC mode-specific methods and handlers
        // Add this method to handle DC voltage changes
        private void DCVoltageTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (dcGenerator != null)
                dcGenerator.OnDCVoltageTextChanged(sender, e);
        }

        private void DCVoltageTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (dcGenerator != null)
                dcGenerator.OnDCVoltageLostFocus(sender, e);
        }

        private void DCVoltageUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dcGenerator != null)
                dcGenerator.OnDCVoltageUnitChanged(sender, e);
        }

        private void DCImpedanceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dcGenerator != null)
                dcGenerator.OnDCImpedanceChanged(sender, e);
        }




        #endregion

    }
}


