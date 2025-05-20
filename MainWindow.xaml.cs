
// MainWindow.xaml.cs (Core File)
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.Generic;





namespace DG2072_USB_Control
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        
        #region Properties and Fields

        // Device connection state
        private bool isConnected = false;
        
        // Active channel (1 or 2)
        private int activeChannel = 1;
        
        // Operation mode (Continuous or Modulation)
        private string operationMode = "Continuous";
        
        // Logger for commands
        private List<string> commandLog = new List<string>();

        private bool uiInitialized = false;


        #endregion

        #region Initialization and Lifecycle

        public MainWindow()
        {
            InitializeComponent();
            
            // Set initial UI state
            UpdateUIForConnectionState(isConnected);
            UpdateChannelDisplay(activeChannel);
            
            // Initialize waveform combobox
            if (ChannelWaveformComboBox != null)
            {
                ChannelWaveformComboBox.SelectedIndex = 0; // Default to Sine
            }
            
            // Initialize modulation type combobox
            if (ModulationTypeComboBox != null)
            {
                ModulationTypeComboBox.SelectedIndex = 0; // Default to AM
            }
        }

        /// <summary>
        /// Handles window loaded event to complete initialization
        /// </summary>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LogMessage("Application started");
            UpdateUIState();



            // Set initialization flag when everything is ready
            uiInitialized = true;

        }

        /// <summary>
        /// Handles window closing event to clean up resources
        /// </summary>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (isConnected)
            {
                // Ask user if they want to disconnect
                MessageBoxResult result = MessageBox.Show(
                    "You are still connected to the device. Do you want to disconnect before closing?",
                    "Disconnect Device?",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Placeholder for disconnect code
                    isConnected = false;
                    LogMessage("Disconnected from device on application close");
                }
            }
        }


        /// <summary>
        ///  a helper method to safely access UI controls:
        /// </summary>
        /// <returns></returns>
        private bool IsUIReady()
        {
            if (!uiInitialized)
            {
                LogMessage("Warning: UI access attempted before initialization completed");
                return false;
            }
            return true;
        }

        #endregion

        #region UI State Management

        /// <summary>
        /// Updates the entire UI state based on current settings
        /// </summary>
        private void UpdateUIState()
        {
            // Update connection status UI
            UpdateUIForConnectionState(isConnected);
            
            // Update channel display
            UpdateChannelDisplay(activeChannel);
            
            // Update operation mode UI
            if (operationMode == "Continuous")
            {
                ContinuousModeRadioButton.IsChecked = true;
                ContinuousModeGrid.Visibility = Visibility.Visible;
                ModulationModeGrid.Visibility = Visibility.Collapsed;
            }
            else
            {
                ModulationModeRadioButton.IsChecked = true;
                ContinuousModeGrid.Visibility = Visibility.Collapsed;
                ModulationModeGrid.Visibility = Visibility.Visible;
                
                // Update modulation UI
                if (ModulationTypeComboBox.SelectedItem != null)
                {
                    string selectedModulation = (ModulationTypeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
                    UpdateModulationUI(selectedModulation);
                }
            }
            
            // Update waveform-specific UI
            if (ChannelWaveformComboBox.SelectedItem != null)
            {
                string selectedWaveform = (ChannelWaveformComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
                HideAllWaveformPanels();
                
                switch (selectedWaveform)
                {
                    case "Pulse":
                        PulseGroupBox.Visibility = Visibility.Visible;
                        break;
                    case "Harmonic":
                        HarmonicsGroupBox.Visibility = Visibility.Visible;
                        break;
                    case "Dual Tone":
                        DualToneGroupBox.Visibility = Visibility.Visible;
                        break;
                    case "Arbitrary Waveform":
                        ArbitraryWaveformGroupBox.Visibility = Visibility.Visible;
                        break;
                    case "DC":
                        DCGroupBox.Visibility = Visibility.Visible;
                        break;
                }
            }
        }

        /// <summary>
        /// Hides all waveform-specific panels
        /// </summary>
        private void HideAllWaveformPanels()
        {
            PulseGroupBox.Visibility = Visibility.Collapsed;
            HarmonicsGroupBox.Visibility = Visibility.Collapsed;
            DualToneGroupBox.Visibility = Visibility.Collapsed;
            ArbitraryWaveformGroupBox.Visibility = Visibility.Collapsed;
            DCGroupBox.Visibility = Visibility.Collapsed;
        }

        #endregion

        #region Command Log Functions

        /// <summary>
        /// Adds a message to the command log
        /// </summary>
        public void LogMessage(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string logEntry = $"[{timestamp}] {message}";

            // Add to log collection
            commandLog.Add(logEntry);

            // Update UI on UI thread
            Dispatcher.Invoke(() =>
            {
                if (CommandLogTextBox != null)
                {
                    CommandLogTextBox.AppendText(logEntry + Environment.NewLine);
                    CommandLogTextBox.ScrollToEnd();
                }
            });
        }

        /// <summary>
        /// Clears the command log
        /// </summary>
        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            commandLog.Clear();
            CommandLogTextBox.Clear();
            LogMessage("Log cleared");
        }

        #endregion
    }
}