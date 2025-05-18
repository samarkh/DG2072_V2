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
using DG2072_USB_Control.Modulation; // New namespace for modulation classes

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

        // Operation mode tracking
        private bool isModulationMode = false;
        private string currentModulationType = "AM";

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

        private DockPanel DutyCycleDockPanel;

        private bool _frequencyModeActive = true; // Default to frequency mode
        private DockPanel PulsePeriodDockPanel;
        private DockPanel PhaseDockPanel;
        private ChannelHarmonicController harmonicController;

        private double frequencyRatio = 2.0; // Default frequency ratio (harmonic)

        private DockPanel DCVoltageDockPanel;

        // Harmonics management
        // private HarmonicsManager _harmonicsManager;// Never used
        // private HarmonicsUIController _harmonicsUIController;// never used

        // Continuous mode generators
        private PulseGen pulseGenerator;
        private DualToneGen dualToneGen;
        private RampGen rampGenerator;
        private SquareGen squareGenerator;
        private SinGen sineGenerator;
        private DCGen dcGenerator;
        private NoiseGen noiseGenerator;
        private ArbitraryWaveformGen arbitraryWaveformGen;

        // Modulation generators
        private AMModulation amModulation;
        private FMModulation fmModulation;
        private PMModulation pmModulation;
        private ASKModulation askModulation;
        private FSKModulation fskModulation;
        private PSKModulation pskModulation;
        private PWMModulation pwmModulation;

        public MainWindow()
        {
            InitializeComponent();

            // Initialize the device communication
            rigolDG2072 = new RigolDG2072();
            rigolDG2072.LogEvent += (s, message) => LogMessage(message);

            // Initialize ComboBoxes
            ChannelWaveformComboBox.SelectedIndex = 0;
            ModulationTypeComboBox.SelectedIndex = 0;

            // Initialize auto-refresh feature
            InitializeAutoRefresh();
        }

        #region Window Events

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Find and store references to UI elements
            InitializeUIReferences();

            // Initialize continuous mode generators
            InitializeContinuousModeGenerators();

            // Initialize modulation mode generators
            InitializeModulationGenerators();

            // Use a small delay before auto-connecting
            StartupDelayedAutoConnect();
        }

        private void InitializeUIReferences()
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

            // Find and store reference to DC panel
            DCVoltageDockPanel = FindVisualParent<DockPanel>(DCVoltageTextBox);
        }

        private void InitializeContinuousModeGenerators()
        {
            // Initialize the pulse generator
            pulseGenerator = new PulseGen(rigolDG2072, activeChannel, this);
            pulseGenerator.LogEvent += (s, message) => LogMessage(message);

            // Initialize the dual tone generator
            dualToneGen = new DualToneGen(rigolDG2072, activeChannel, this);
            dualToneGen.LogEvent += (s, message) => LogMessage(message);

            // Initialize the ramp generator
            rampGenerator = new RampGen(rigolDG2072, activeChannel, this);
            rampGenerator.LogEvent += (s, message) => LogMessage(message);

            // Initialize the square generator
            squareGenerator = new SquareGen(rigolDG2072, activeChannel, this);
            squareGenerator.LogEvent += (s, message) => LogMessage(message);

            // Initialize the sine generator
            sineGenerator = new SinGen(rigolDG2072, activeChannel, this);
            sineGenerator.LogEvent += (s, message) => LogMessage(message);

            // Initialize the DC generator
            dcGenerator = new DCGen(rigolDG2072, activeChannel, this);
            dcGenerator.LogEvent += (s, message) => LogMessage(message);

            // Initialize the noise generator
            noiseGenerator = new NoiseGen(rigolDG2072, activeChannel, this);
            noiseGenerator.LogEvent += (s, message) => LogMessage(message);

            // Initialize the arbitrary waveform generator
            arbitraryWaveformGen = new ArbitraryWaveformGen(rigolDG2072, activeChannel, this);
            arbitraryWaveformGen.LogEvent += (s, message) => LogMessage(message);
        }

        private void InitializeModulationGenerators()
        {
            // Initialize AM modulation generator
            amModulation = new AMModulation(rigolDG2072, activeChannel, this);
            amModulation.LogEvent += (s, message) => LogMessage(message);

            // Initialize FM modulation generator
            fmModulation = new FMModulation(rigolDG2072, activeChannel, this);
            fmModulation.LogEvent += (s, message) => LogMessage(message);

            // Initialize PM modulation generator
            pmModulation = new PMModulation(rigolDG2072, activeChannel, this);
            pmModulation.LogEvent += (s, message) => LogMessage(message);

            // Initialize ASK modulation generator
            askModulation = new ASKModulation(rigolDG2072, activeChannel, this);
            askModulation.LogEvent += (s, message) => LogMessage(message);

            // Initialize FSK modulation generator
            fskModulation = new FSKModulation(rigolDG2072, activeChannel, this);
            fskModulation.LogEvent += (s, message) => LogMessage(message);

            // Initialize PSK modulation generator
            pskModulation = new PSKModulation(rigolDG2072, activeChannel, this);
            pskModulation.LogEvent += (s, message) => LogMessage(message);

            // Initialize PWM modulation generator
            pwmModulation = new PWMModulation(rigolDG2072, activeChannel, this);
            pwmModulation.LogEvent += (s, message) => LogMessage(message);
        }

        private void StartupDelayedAutoConnect()
        {
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

            startupTimer.Start();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Disconnect on window closing
            if (isConnected)
            {
                LogMessage("Application closing - performing safe disconnect...");

                try
                {
                    // If we're in modulation mode, turn it off before disconnecting
                    if (isModulationMode)
                    {
                        LogMessage("Disabling modulation before closing...");
                        DisableAllModulation();
                    }

                    // If we're in harmonic mode, disable it before disconnecting
                    string currentWaveform = rigolDG2072.SendQuery($":SOUR{activeChannel}:FUNC?").Trim().ToUpper();
                    if (currentWaveform.Contains("HARM"))
                    {
                        LogMessage("Disabling harmonic mode before closing...");
                        rigolDG2072.SendCommand($":SOUR{activeChannel}:HARM:STAT OFF");
                        System.Threading.Thread.Sleep(50);
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

        #endregion

        #region Operation Mode Handling

        // Handler for operation mode radio buttons
        private void OperationModeRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (!isConnected) return;

            RadioButton radioButton = sender as RadioButton;
            if (radioButton == null) return;

            isModulationMode = (radioButton == ModulationModeRadioButton);

            // Update UI based on mode
            UpdateOperationModeUI();

            // Refresh settings based on current mode
            if (isModulationMode)
            {
                RefreshModulationSettings();
                LogMessage($"Switched to Modulation mode - {currentModulationType}");
            }
            else
            {
                RefreshChannelSettings();
                LogMessage("Switched to Continuous mode");
            }
        }

        // Update UI elements visibility based on operation mode
        private void UpdateOperationModeUI()
        {
            // Update visibility of mode-specific controls
            WaveformSelectionDockPanel.Visibility = isModulationMode ? Visibility.Collapsed : Visibility.Visible;
            ContinuousModeGrid.Visibility = isModulationMode ? Visibility.Collapsed : Visibility.Visible;
            ModulationModeGrid.Visibility = isModulationMode ? Visibility.Visible : Visibility.Collapsed;

            // Update group box header
            ChannelParametersGroupBox.Header = $"Channel {activeChannel} Parameters";

            // Show/hide Apply button based on mode
            ChannelApplyButton.Visibility = isModulationMode ? Visibility.Collapsed : Visibility.Visible;
        }

        #endregion

        #region Modulation Control Handlers

        // Handler for modulation type selection
        private void ModulationTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isConnected || !isModulationMode) return;

            ComboBoxItem selectedItem = ModulationTypeComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem == null) return;

            // Get the previous modulation type for cleanup
            string previousModulationType = currentModulationType;

            // Update current modulation type
            currentModulationType = selectedItem.Content.ToString();

            // Hide all modulation group boxes
            AMModulationGroupBox.Visibility = Visibility.Collapsed;
            FMModulationGroupBox.Visibility = Visibility.Collapsed;
            PMModulationGroupBox.Visibility = Visibility.Collapsed;
            // Hide other modulation panels...

            // Show the selected modulation group box
            switch (currentModulationType)
            {
                case "AM":
                    AMModulationGroupBox.Visibility = Visibility.Visible;
                    break;
                case "FM":
                    FMModulationGroupBox.Visibility = Visibility.Visible;
                    break;
                case "PM":
                    PMModulationGroupBox.Visibility = Visibility.Visible;
                    break;
                // Show other panels based on selection...
                default:
                    LogMessage($"Unknown modulation type: {currentModulationType}");
                    break;
            }

            // Disable previous modulation if it was active
            DisableModulation(previousModulationType);

            // Refresh settings for the newly selected modulation
            RefreshModulationSettings();

            LogMessage($"Selected modulation type: {currentModulationType}");
        }

        // Disable modulation of specific type
        private void DisableModulation(string modulationType)
        {
            if (!isConnected) return;

            try
            {
                // Send command to disable the specified modulation
                rigolDG2072.SendCommand($":SOUR{activeChannel}:{modulationType}:STAT OFF");
                LogMessage($"Disabled {modulationType} modulation");
            }
            catch (Exception ex)
            {
                LogMessage($"Error disabling {modulationType} modulation: {ex.Message}");
            }
        }

        // Disable all modulation types
        private void DisableAllModulation()
        {
            if (!isConnected) return;

            try
            {
                // Disable all modulation types
                string[] modTypes = { "AM", "FM", "PM", "ASK", "FSK", "PSK", "PWM" };
                foreach (string modType in modTypes)
                {
                    rigolDG2072.SendCommand($":SOUR{activeChannel}:{modType}:STAT OFF");
                }
                LogMessage("Disabled all modulation");
            }
            catch (Exception ex)
            {
                LogMessage($"Error disabling all modulation: {ex.Message}");
            }
        }

        // Refresh settings for current modulation type
        private void RefreshModulationSettings()
        {
            if (!isConnected || !isModulationMode) return;

            try
            {
                // Refresh based on current modulation type
                switch (currentModulationType)
                {
                    case "AM":
                        amModulation.RefreshParameters();
                        break;
                    case "FM":
                        fmModulation.RefreshParameters();
                        break;
                    case "PM":
                        pmModulation.RefreshParameters();
                        break;
                    case "ASK":
                        askModulation.RefreshParameters();
                        break;
                    case "FSK":
                        fskModulation.RefreshParameters();
                        break;
                    case "PSK":
                        pskModulation.RefreshParameters();
                        break;
                    case "PWM":
                        pwmModulation.RefreshParameters();
                        break;
                    default:
                        LogMessage($"Unknown modulation type: {currentModulationType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error refreshing {currentModulationType} modulation settings: {ex.Message}");
            }
        }

        #region AM Modulation Event Handlers

        private void AMStateToggle_Click(object sender, RoutedEventArgs e)
        {
            if (!isConnected) return;

            bool isOn = AMStateToggle.IsChecked == true;
            AMStateToggle.Content = isOn ? "ON" : "OFF";

            try
            {
                // Set AM state
                rigolDG2072.SendCommand($":SOUR{activeChannel}:AM:STAT {(isOn ? "ON" : "OFF")}");
                LogMessage($"Set AM state to {(isOn ? "ON" : "OFF")}");
            }
            catch (Exception ex)
            {
                LogMessage($"Error setting AM state: {ex.Message}");
            }
        }

        private void AMSourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isConnected) return;

            try
            {
                ComboBoxItem selectedItem = AMSourceComboBox.SelectedItem as ComboBoxItem;
                if (selectedItem == null) return;

                string source = selectedItem.Content.ToString();

                // Set AM source
                rigolDG2072.SendCommand($":SOUR{activeChannel}:AM:SOUR {source}");

                // Show/hide internal settings based on source
                AMInternalSettingsGroupBox.Visibility = (source == "Internal") ? Visibility.Visible : Visibility.Collapsed;

                LogMessage($"Set AM source to {source}");
            }
            catch (Exception ex)
            {
                LogMessage($"Error setting AM source: {ex.Message}");
            }
        }

        private void AMDepthTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isConnected) return;
            if (!double.TryParse(AMDepthTextBox.Text, out double depth)) return;

            // Use a timer to debounce changes
            DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                try
                {
                    if (double.TryParse(AMDepthTextBox.Text, out double d))
                    {
                        // Ensure depth is in valid range (0-120%)
                        d = Math.Max(0, Math.Min(120, d));

                        // Set AM depth
                        rigolDG2072.SendCommand($":SOUR{activeChannel}:AM:DEPT {d}");
                        LogMessage($"Set AM depth to {d}%");
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"Error setting AM depth: {ex.Message}");
                }
            };
            timer.Start();
        }

        private void AMDepthTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!isConnected) return;
            if (!double.TryParse(AMDepthTextBox.Text, out double depth)) return;

            try
            {
                // Ensure depth is in valid range (0-120%)
                depth = Math.Max(0, Math.Min(120, depth));

                // Update text with validated value
                AMDepthTextBox.Text = depth.ToString("F1");

                // Set AM depth
                rigolDG2072.SendCommand($":SOUR{activeChannel}:AM:DEPT {depth}");
                LogMessage($"Set AM depth to {depth}%");
            }
            catch (Exception ex)
            {
                LogMessage($"Error setting AM depth: {ex.Message}");
            }
        }

        private void AMWaveformComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isConnected) return;

            try
            {
                ComboBoxItem selectedItem = AMWaveformComboBox.SelectedItem as ComboBoxItem;
                if (selectedItem == null) return;

                string waveform = selectedItem.Content.ToString();
                string waveformCommand = MapWaveformNameToCommand(waveform);

                // Set AM internal waveform
                rigolDG2072.SendCommand($":SOUR{activeChannel}:AM:INT:FUNC {waveformCommand}");
                LogMessage($"Set AM internal waveform to {waveform}");
            }
            catch (Exception ex)
            {
                LogMessage($"Error setting AM internal waveform: {ex.Message}");
            }
        }

        private void AMFrequencyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isConnected) return;
            if (!double.TryParse(AMFrequencyTextBox.Text, out double frequency)) return;

            // Use a timer to debounce changes
            DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                try
                {
                    if (double.TryParse(AMFrequencyTextBox.Text, out double freq))
                    {
                        string unit = UnitConversionUtility.GetFrequencyUnit(AMFrequencyUnitComboBox);
                        double actualFrequency = freq * UnitConversionUtility.GetFrequencyMultiplier(unit);

                        // Set AM internal frequency
                        rigolDG2072.SendCommand($":SOUR{activeChannel}:AM:INT:FREQ {actualFrequency}");
                        LogMessage($"Set AM internal frequency to {freq} {unit} ({actualFrequency} Hz)");
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"Error setting AM internal frequency: {ex.Message}");
                }
            };
            timer.Start();
        }

        private void AMFrequencyTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!isConnected) return;
            if (!double.TryParse(AMFrequencyTextBox.Text, out double frequency)) return;

            try
            {
                string unit = UnitConversionUtility.GetFrequencyUnit(AMFrequencyUnitComboBox);
                double actualFrequency = frequency * UnitConversionUtility.GetFrequencyMultiplier(unit);

                // Format with appropriate precision
                AMFrequencyTextBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(frequency);

                // Set AM internal frequency
                rigolDG2072.SendCommand($":SOUR{activeChannel}:AM:INT:FREQ {actualFrequency}");
                LogMessage($"Set AM internal frequency to {frequency} {unit} ({actualFrequency} Hz)");
            }
            catch (Exception ex)
            {
                LogMessage($"Error setting AM internal frequency: {ex.Message}");
            }
        }

        private void AMFrequencyUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isConnected) return;
            if (!double.TryParse(AMFrequencyTextBox.Text, out double frequency)) return;

            try
            {
                string unit = UnitConversionUtility.GetFrequencyUnit(AMFrequencyUnitComboBox);
                double actualFrequency = frequency * UnitConversionUtility.GetFrequencyMultiplier(unit);

                // Set AM internal frequency
                rigolDG2072.SendCommand($":SOUR{activeChannel}:AM:INT:FREQ {actualFrequency}");
                LogMessage($"Set AM internal frequency to {frequency} {unit} ({actualFrequency} Hz)");
            }
            catch (Exception ex)
            {
                LogMessage($"Error setting AM internal frequency: {ex.Message}");
            }
        }

        private void AMApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isConnected) return;

            try
            {
                // Apply all AM settings
                amModulation.ApplyParameters();
                LogMessage("Applied AM modulation settings");
            }
            catch (Exception ex)
            {
                LogMessage($"Error applying AM modulation settings: {ex.Message}");
            }
        }

        #endregion

        #region FM Modulation Event Handlers

        private void FMStateToggle_Click(object sender, RoutedEventArgs e)
        {
            if (!isConnected) return;

            bool isOn = FMStateToggle.IsChecked == true;
            FMStateToggle.Content = isOn ? "ON" : "OFF";

            try
            {
                // Set FM state
                rigolDG2072.SendCommand($":SOUR{activeChannel}:FM:STAT {(isOn ? "ON" : "OFF")}");
                LogMessage($"Set FM state to {(isOn ? "ON" : "OFF")}");
            }
            catch (Exception ex)
            {
                LogMessage($"Error setting FM state: {ex.Message}");
            }
        }

        private void FMSourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isConnected) return;

            try
            {
                ComboBoxItem selectedItem = FMSourceComboBox.SelectedItem as ComboBoxItem;
                if (selectedItem == null) return;

                string source = selectedItem.Content.ToString();

                // Set FM source
                rigolDG2072.SendCommand($":SOUR{activeChannel}:FM:SOUR {source}");

                // Show/hide internal settings based on source
                FMInternalSettingsGroupBox.Visibility = (source == "Internal") ? Visibility.Visible : Visibility.Collapsed;

                LogMessage($"Set FM source to {source}");
            }
            catch (Exception ex)
            {
                LogMessage($"Error setting FM source: {ex.Message}");
            }
        }

        private void FMDeviationTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isConnected) return;
            if (!double.TryParse(FMDeviationTextBox.Text, out double deviation)) return;

            // Use a timer to debounce changes
            DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                try
                {
                    if (double.TryParse(FMDeviationTextBox.Text, out double dev))
                    {
                        string unit = UnitConversionUtility.GetFrequencyUnit(FMDeviationUnitComboBox);
                        double actualDeviation = dev * UnitConversionUtility.GetFrequencyMultiplier(unit);

                        // Set FM deviation
                        rigolDG2072.SendCommand($":SOUR{activeChannel}:FM:DEV {actualDeviation}");
                        LogMessage($"Set FM deviation to {dev} {unit} ({actualDeviation} Hz)");
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"Error setting FM deviation: {ex.Message}");
                }
            };
            timer.Start();
        }

        private void FMDeviationTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!isConnected) return;
            if (!double.TryParse(FMDeviationTextBox.Text, out double deviation)) return;

            try
            {
                string unit = UnitConversionUtility.GetFrequencyUnit(FMDeviationUnitComboBox);
                double actualDeviation = deviation * UnitConversionUtility.GetFrequencyMultiplier(unit);

                // Format with appropriate precision
                FMDeviationTextBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(deviation);

                // Set FM deviation
                rigolDG2072.SendCommand($":SOUR{activeChannel}:FM:DEV {actualDeviation}");
                LogMessage($"Set FM deviation to {deviation} {unit} ({actualDeviation} Hz)");
            }
            catch (Exception ex)
            {
                LogMessage($"Error setting FM deviation: {ex.Message}");
            }
        }

        private void FMDeviationUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isConnected) return;
            if (!double.TryParse(FMDeviationTextBox.Text, out double deviation)) return;

            try
            {
                string unit = UnitConversionUtility.GetFrequencyUnit(FMDeviationUnitComboBox);
                double actualDeviation = deviation * UnitConversionUtility.GetFrequencyMultiplier(unit);

                // Set FM deviation
                rigolDG2072.SendCommand($":SOUR{activeChannel}:FM:DEV {actualDeviation}");
                LogMessage($"Set FM deviation to {deviation} {unit} ({actualDeviation} Hz)");
            }
            catch (Exception ex)
            {
                LogMessage($"Error setting FM deviation: {ex.Message}");
            }
        }

        private void FMWaveformComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isConnected) return;

            try
            {
                ComboBoxItem selectedItem = FMWaveformComboBox.SelectedItem as ComboBoxItem;
                if (selectedItem == null) return;

                string waveform = selectedItem.Content.ToString();
                string waveformCommand = MapWaveformNameToCommand(waveform);

                // Set FM internal waveform
                rigolDG2072.SendCommand($":SOUR{activeChannel}:FM:INT:FUNC {waveformCommand}");
                LogMessage($"Set FM internal waveform to {waveform}");
            }
            catch (Exception ex)
            {
                LogMessage($"Error setting FM internal waveform: {ex.Message}");
            }
        }

        private void FMFrequencyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isConnected) return;
            if (!double.TryParse(FMFrequencyTextBox.Text, out double frequency)) return;

            // Use a timer to debounce changes
            DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                try
                {
                    if (double.TryParse(FMFrequencyTextBox.Text, out double freq))
                    {
                        string unit = UnitConversionUtility.GetFrequencyUnit(FMFrequencyUnitComboBox);
                        double actualFrequency = freq * UnitConversionUtility.GetFrequencyMultiplier(unit);

                        // Set FM internal frequency
                        rigolDG2072.SendCommand($":SOUR{activeChannel}:FM:INT:FREQ {actualFrequency}");
                        LogMessage($"Set FM internal frequency to {freq} {unit} ({actualFrequency} Hz)");
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"Error setting FM internal frequency: {ex.Message}");
                }
            };
            timer.Start();
        }

        private void FMFrequencyTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!isConnected) return;
            if (!double.TryParse(FMFrequencyTextBox.Text, out double frequency)) return;

            try
            {
                string unit = UnitConversionUtility.GetFrequencyUnit(FMFrequencyUnitComboBox);
                double actualFrequency = frequency * UnitConversionUtility.GetFrequencyMultiplier(unit);

                // Format with appropriate precision
                FMFrequencyTextBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(frequency);

                // Set FM internal frequency
                rigolDG2072.SendCommand($":SOUR{activeChannel}:FM:INT:FREQ {actualFrequency}");
                LogMessage($"Set FM internal frequency to {frequency} {unit} ({actualFrequency} Hz)");
            }
            catch (Exception ex)
            {
                LogMessage($"Error setting FM internal frequency: {ex.Message}");
            }
        }

        private void FMFrequencyUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isConnected) return;
            if (!double.TryParse(FMFrequencyTextBox.Text, out double frequency)) return;

            try
            {
                string unit = UnitConversionUtility.GetFrequencyUnit(FMFrequencyUnitComboBox);
                double actualFrequency = frequency * UnitConversionUtility.GetFrequencyMultiplier(unit);

                // Set FM internal frequency
                rigolDG2072.SendCommand($":SOUR{activeChannel}:FM:INT:FREQ {actualFrequency}");
                LogMessage($"Set FM internal frequency to {frequency} {unit} ({actualFrequency} Hz)");
            }
            catch (Exception ex)
            {
                LogMessage($"Error setting FM internal frequency: {ex.Message}");
            }
        }

        private void FMApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isConnected) return;

            try
            {
                // Apply all FM settings
                fmModulation.ApplyParameters();
                LogMessage("Applied FM modulation settings");
            }
            catch (Exception ex)
            {
                LogMessage($"Error applying FM modulation settings: {ex.Message}");
            }
        }

        #endregion

        // Add other modulation type event handlers similarly (PM, ASK, FSK, PSK, PWM)

        // Helper method to map UI waveform names to SCPI commands
        private string MapWaveformNameToCommand(string waveform)
        {
            switch (waveform.ToUpper())
            {
                case "SINE": return "SIN";
                case "SQUARE": return "SQU";
                case "TRIANGLE": return "TRI";
                case "UPRAMP": return "RAMP";
                case "DNRAMP": return "NRAMP";
                case "NOISE": return "NOIS";
                case "ARB": return "USER";
                default: return waveform;
            }
        }

        #endregion

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
                ChannelParametersGroupBox.Header = "Channel 1 Parameters";

                // Update all the component classes with the new active channel
                UpdateComponentChannels(activeChannel);
            }
            else
            {
                // Switch to Channel 2
                activeChannel = 2;
                ChannelToggleButton.Content = "Channel 2";
                ActiveChannelTextBlock.Text = "Channel 2";
                ChannelParametersGroupBox.Header = "Channel 2 Parameters";

                // Update all the component classes with the new active channel
                UpdateComponentChannels(activeChannel);
            }

            // Refresh the UI to show current settings for the selected channel
            if (isConnected)
            {
                if (isModulationMode)
                {
                    RefreshModulationSettings();
                }
                else
                {
                    RefreshChannelSettings();
                }
            }
        }

        // Update all component classes with new active channel
        private void UpdateComponentChannels(int channel)
        {
            // Update continuous mode generators
            if (pulseGenerator != null) pulseGenerator.ActiveChannel = channel;
            if (rampGenerator != null) rampGenerator.ActiveChannel = channel;
            if (squareGenerator != null) squareGenerator.ActiveChannel = channel;
            if (sineGenerator != null) sineGenerator.ActiveChannel = channel;
            if (dcGenerator != null) dcGenerator.ActiveChannel = channel;
            if (noiseGenerator != null) noiseGenerator.ActiveChannel = channel;
            if (arbitraryWaveformGen != null) arbitraryWaveformGen.ActiveChannel = channel;
            if (dualToneGen != null) dualToneGen.ActiveChannel = channel;

            // Update modulation generators
            if (amModulation != null) amModulation.ActiveChannel = channel;
            if (fmModulation != null) fmModulation.ActiveChannel = channel;
            if (pmModulation != null) pmModulation.ActiveChannel = channel;
            if (askModulation != null) askModulation.ActiveChannel = channel;
            if (fskModulation != null) fskModulation.ActiveChannel = channel;
            if (pskModulation != null) pskModulation.ActiveChannel = channel;
            if (pwmModulation != null) pwmModulation.ActiveChannel = channel;
        }

        // Keep the existing RefreshChannelSettings method
        private void RefreshChannelSettings()
        {
            // Your existing implementation...
        }

        #endregion

        // Include all your existing methods...
        // You can reuse most of your existing code, just add the new modulation functionality

        #region Helper Methods and Stubs

        // Helper method to find parent control (already implemented)
        private T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            // Existing implementation...
            return null;
        }

        // These are stubs for existing methods - replace with your actual implementations
        private void InitializeAutoRefresh() { }
        private void UpdateAutoRefreshState(bool connected) { }
        private bool Connect() { return true; }
        private bool Disconnect() { return true; }
        private void RefreshInstrumentSettings() { }
        private void LogMessage(string message) { }

        #endregion
    }
}

// Add a new namespace for modulation classes
namespace DG2072_USB_Control.Modulation
{
    using System;
    using System.Windows;
    using System.Windows.Controls;

    // Base class for all modulation types
    public abstract class ModulationBase
    {
        protected RigolDG2072 _device;
        protected int _activeChannel;

        public event EventHandler<string> LogEvent;

        public ModulationBase(RigolDG2072 device, int activeChannel)
        {
            _device = device;
            _activeChannel = activeChannel;
        }

        public int ActiveChannel
        {
            get => _activeChannel;
            set => _activeChannel = value;
        }

        protected void LogMessage(string message) => LogEvent?.Invoke(this, message);

        public abstract void ApplyParameters();
        public abstract void RefreshParameters();
    }

    // AM Modulation class
    public class AMModulation : ModulationBase
    {
        private MainWindow _mainWindow;

        public AMModulation(RigolDG2072 device, int activeChannel, MainWindow mainWindow)
            : base(device, activeChannel)
        {
            _mainWindow = mainWindow;
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
            try
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

    // PM Modulation class (stub)
    public class PMModulation : ModulationBase
    {
        public PMModulation(RigolDG2072 device, int activeChannel, MainWindow mainWindow)
            : base(device, activeChannel) { }

        public override void ApplyParameters() { }
        public override void RefreshParameters() { }
    }

    // ASK Modulation class (stub)
    public class ASKModulation : ModulationBase
    {
        public ASKModulation(RigolDG2072 device, int activeChannel, MainWindow mainWindow)
            : base(device, activeChannel) { }

        public override void ApplyParameters() { }
        public override void RefreshParameters() { }
    }

    // FSK Modulation class (stub)
    public class FSKModulation : ModulationBase
    {
        public FSKModulation(RigolDG2072 device, int activeChannel, MainWindow mainWindow)
            : base(device, activeChannel) { }

        public override void ApplyParameters() { }
        public override void RefreshParameters() { }
    }

    // PSK Modulation class (stub)
    public class PSKModulation : ModulationBase
    {
        public PSKModulation(RigolDG2072 device, int activeChannel, MainWindow mainWindow)
            : base(device, activeChannel) { }

        public override void ApplyParameters() { }
        public override void RefreshParameters() { }
    }

    // PWM Modulation class (stub)
    public class PWMModulation : ModulationBase
    {
        public PWMModulation(RigolDG2072 device, int activeChannel, MainWindow mainWindow)
            : base(device, activeChannel) { }

        public override void ApplyParameters() { }
        public override void RefreshParameters() { }
    }
}