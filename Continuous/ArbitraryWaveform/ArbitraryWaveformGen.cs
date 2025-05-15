using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DG2072_USB_Control.Services;
using DG2072_USB_Control.Continuous.ArbitraryWaveform.Descriptions;

namespace DG2072_USB_Control.Continuous.ArbitraryWaveform
{
    public class ArbitraryWaveformGen : IArbitraryWaveformEventHandler
    {
        // Device reference
        private readonly RigolDG2072 _device;

        // Active channel
        private int _activeChannel;

        // UI elements
        private readonly ComboBox _categoryComboBox;
        private readonly ComboBox _waveformComboBox;
        private readonly TextBox _param1TextBox;
        private readonly TextBox _param2TextBox;
        private readonly TextBlock _param1UnitTextBlock;
        private readonly TextBlock _param2UnitTextBlock;
        private readonly TextBlock _waveformInfoTextBlock;
        private readonly GroupBox _parametersGroupBox;
        private readonly DockPanel _param1DockPanel;
        private readonly DockPanel _param2DockPanel;
        private readonly Label _param1Label;
        private readonly Label _param2Label;
        private readonly Button _applyButton;
        private readonly TextBlock _waveformApplicationsTextBlock;
        

        // Update timers for debouncing
        private DispatcherTimer _param1UpdateTimer;
        private DispatcherTimer _param2UpdateTimer;

        // Event for logging
        public event EventHandler<string> LogEvent;

        // Constructor
        public ArbitraryWaveformGen(RigolDG2072 device, int channel, Window mainWindow)
        {
            _device = device;
            _activeChannel = channel;

            // Initialize UI references
            _categoryComboBox = mainWindow.FindName("ArbitraryWaveformCategoryComboBox") as ComboBox;
            _waveformComboBox = mainWindow.FindName("ArbitraryWaveformComboBox") as ComboBox;
            _param1TextBox =    mainWindow.FindName("ArbitraryParam1TextBox") as TextBox;
            _param2TextBox =    mainWindow.FindName("ArbitraryParam2TextBox") as TextBox;
            _param1UnitTextBlock = mainWindow.FindName("ArbitraryParam1UnitTextBlock") as TextBlock;
            _param2UnitTextBlock =      mainWindow.FindName("ArbitraryParam2UnitTextBlock") as TextBlock;
            _waveformInfoTextBlock =    mainWindow.FindName("ArbitraryWaveformInfoTextBlock") as TextBlock;
            _parametersGroupBox =       mainWindow.FindName("WaveformParametersGroup") as GroupBox;
            _param1DockPanel =          mainWindow.FindName("ArbitraryParam1DockPanel") as DockPanel;
            _param2DockPanel =          mainWindow.FindName("ArbitraryParam2DockPanel") as DockPanel;
           
            _applyButton =          mainWindow.FindName("ApplyArbitraryWaveformButton") as Button;


            _waveformApplicationsTextBlock = mainWindow.FindName("WaveformApplicationsTextBlock") as TextBlock;
            _waveformInfoTextBlock = mainWindow.FindName("WaveformInfoTextBlock") as TextBlock;



            // Get the Labels from the DockPanels
            if (_param1DockPanel != null && _param1DockPanel.Children.Count > 0)
                _param1Label = _param1DockPanel.Children[0] as Label;

            if (_param2DockPanel != null && _param2DockPanel.Children.Count > 0)
                _param2Label = _param2DockPanel.Children[0] as Label;

            // Initialize UI controls
            InitializeArbitraryWaveformControls();
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

        #region IArbitraryWaveformEventHandler Implementation

        public void OnCategorySelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Update the available waveforms for the selected category
            LoadArbitraryWaveformsForCategory();

            // If there are waveforms and one is selected, auto-apply
            if (_waveformComboBox?.Items.Count > 0 && _waveformComboBox.SelectedItem != null)
            {
                ApplyArbitraryWaveform();
            }
        }

        public void OnWaveformSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_waveformComboBox?.SelectedItem != null)
            {
                string selectedWaveform = _waveformComboBox.SelectedItem.ToString();
                UpdateArbitraryWaveformParameters(selectedWaveform);

                // Auto-apply the waveform when selection changes
                ApplyArbitraryWaveform();
            }
        }

        public void OnParameterTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsDeviceConnected()) return;

            TextBox textBox = sender as TextBox;
            if (textBox == null || !double.TryParse(textBox.Text, out double value)) return;

            // Determine which parameter is being changed
            DispatcherTimer timer = null;

            if (textBox == _param1TextBox)
            {
                if (_param1UpdateTimer == null)
                {
                    _param1UpdateTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(500)
                    };
                    _param1UpdateTimer.Tick += (s, args) =>
                    {
                        _param1UpdateTimer.Stop();
                        if (double.TryParse(_param1TextBox.Text, out double param))
                        {
                            // Update will happen when Apply button is clicked
                            Log($"Parameter 1 set to {param}");
                        }
                    };
                }
                timer = _param1UpdateTimer;
            }
            else if (textBox == _param2TextBox)
            {
                if (_param2UpdateTimer == null)
                {
                    _param2UpdateTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(500)
                    };
                    _param2UpdateTimer.Tick += (s, args) =>
                    {
                        _param2UpdateTimer.Stop();
                        if (double.TryParse(_param2TextBox.Text, out double param))
                        {
                            Log($"Parameter 2 set to {param}");
                        }
                    };
                }
                timer = _param2UpdateTimer;
            }

            if (timer != null)
            {
                timer.Stop();
                timer.Start();
            }
        }

        public void OnParameterLostFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (textBox == null || !double.TryParse(textBox.Text, out double value)) return;

            // Format the value with appropriate number of decimal places
            textBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(value, 1);

            // Auto-apply when parameters change
            ApplyArbitraryWaveform();
        }

        public void OnApplyButtonClick(object sender, RoutedEventArgs e)
        {
            ApplyArbitraryWaveform();
        }

        #endregion

        #region Core Functionality

        // Check if the device is connected
        private bool IsDeviceConnected()
        {
            return _device != null && _device.IsConnected;
        }

        // Initialize the arbitrary waveform controls
        private void InitializeArbitraryWaveformControls()
        {
            try
            {
                // Clear existing items
                if (_categoryComboBox != null)
                    _categoryComboBox.Items.Clear();

                // Get all categories from the RigolDG2072 instance
                var categories = _device.GetArbitraryWaveformCategories();

                // Add each category to the ComboBox
                foreach (var category in categories)
                {
                    _categoryComboBox.Items.Add(category.ToString());
                }

                // Select the first category by default
                if (_categoryComboBox.Items.Count > 0)
                {
                    _categoryComboBox.SelectedIndex = 0;

                    // Add this line to explicitly load waveforms for the selected category
                    LoadArbitraryWaveformsForCategory();
                }
            }
            catch (Exception ex)
            {
                Log($"Error initializing arbitrary waveform controls: {ex.Message}");
            }
        }

        // Load waveforms for the selected category
        private void LoadArbitraryWaveformsForCategory()
        {
            try
            {
                // Clear existing items
                if (_waveformComboBox != null)
                    _waveformComboBox.Items.Clear();

                // Get the selected category
                if (_categoryComboBox?.SelectedItem == null)
                    return;

                // Parse the selected category string back to the enum value
                if (Enum.TryParse(_categoryComboBox.SelectedItem.ToString(),
                                  out RigolDG2072.ArbitraryWaveformCategory selectedCategory))
                {
                    // Get waveforms for the selected category
                    var waveforms = _device.GetArbitraryWaveformNames(selectedCategory);

                    // Add each waveform to the ComboBox
                    foreach (var waveform in waveforms)
                    {
                        _waveformComboBox.Items.Add(waveform);
                    }

                    // Select the first waveform by default
                    if (_waveformComboBox.Items.Count > 0)
                    {
                        _waveformComboBox.SelectedIndex = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error loading waveforms for category: {ex.Message}");
            }
        }

        // Update parameter controls based on waveform type
        private void UpdateArbitraryWaveformParameters(string waveformName)
        {
            try
            {
                // Reset parameters to defaults (existing code)
                if (_param1TextBox != null) _param1TextBox.Text = "1.0";
                if (_param2TextBox != null) _param2TextBox.Text = "1.0";

                // Set the parameter labels and units based on waveform type (existing code)
                switch (waveformName.ToUpper())
                {
                    // ... existing switch case code ...
                }

                // Update the waveform info text
                if (_waveformInfoTextBlock != null)
                {
                    // Get full description 
                    string fullDescription = _device.GetArbitraryWaveformInfo(waveformName);

                    // Split description at "Applications:" if present
                    int appIndex = fullDescription.IndexOf("Applications:");

                    if (appIndex > 0)
                    {
                        // Get main info (everything before "Applications:")
                        string mainInfo = fullDescription.Substring(0, appIndex).Trim();

                        // Get applications info
                        string applicationsPart = fullDescription.Substring(appIndex).Trim();

                        // Set the separated texts
                        _waveformInfoTextBlock.Text = mainInfo + "\n\nChanges are applied automatically.";

                        if (_waveformApplicationsTextBlock != null)
                        {
                            _waveformApplicationsTextBlock.Text = applicationsPart;
                        }
                    }
                    else
                    {
                        // No applications section, just use the whole text
                        _waveformInfoTextBlock.Text = fullDescription +
                            "\n\nChanges are applied automatically.";

                        if (_waveformApplicationsTextBlock != null)
                        {
                            _waveformApplicationsTextBlock.Text = "";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error updating arbitrary waveform parameters: {ex.Message}");
            }
        }

        // Apply the arbitrary waveform to the device
        private void ApplyArbitraryWaveform()
        {
            try
            {
                if (_categoryComboBox?.SelectedItem == null ||
                    _waveformComboBox?.SelectedItem == null)
                    return;

                // Get the selected category and waveform
                if (Enum.TryParse(_categoryComboBox.SelectedItem.ToString(),
                                  out RigolDG2072.ArbitraryWaveformCategory selectedCategory))
                {
                    string selectedArbWaveform = _waveformComboBox.SelectedItem.ToString();

                    // Get current parameters from UI
                    double frequency = GetFrequencyFromUI();
                    double amplitude = GetAmplitudeFromUI();
                    double offset = GetOffsetFromUI();
                    double phase = GetPhaseFromUI();

                    // Get additional parameters if applicable
                    Dictionary<string, double> additionalParams = new Dictionary<string, double>();

                    if (_parametersGroupBox?.Visibility == Visibility.Visible)
                    {
                        if (_param1DockPanel?.Visibility == Visibility.Visible &&
                            double.TryParse(_param1TextBox?.Text, out double param1))
                        {
                            additionalParams["1"] = param1;
                        }

                        if (_param2DockPanel?.Visibility == Visibility.Visible &&
                            double.TryParse(_param2TextBox?.Text, out double param2))
                        {
                            additionalParams["2"] = param2;
                        }
                    }

                    // Apply the arbitrary waveform
                    _device.SetArbitraryWaveform(_activeChannel, selectedCategory, selectedArbWaveform);

                    // Apply basic parameters
                    _device.SetFrequency(_activeChannel, frequency);
                    _device.SetAmplitude(_activeChannel, amplitude);
                    _device.SetOffset(_activeChannel, offset);
                    _device.SetPhase(_activeChannel, phase);

                    // Apply additional parameters if available
                    if (additionalParams.Count > 0)
                    {
                        // The device specific API would need to handle these
                        foreach (var param in additionalParams)
                        {
                            Log($"Parameter {param.Key}: {param.Value}");
                        }
                    }

                    // Log the operation
                    Log($"Applied {selectedArbWaveform} arbitrary waveform from {selectedCategory} category to Channel {_activeChannel}");
                }
            }
            catch (Exception ex)
            {
                Log($"Error applying arbitrary waveform: {ex.Message}");
            }
        }

        // Helper methods to get values from MainWindow UI
        private double GetFrequencyFromUI()
        {
            TextBox freqTextBox = FindControl("ChannelFrequencyTextBox") as TextBox;
            ComboBox unitComboBox = FindControl("ChannelFrequencyUnitComboBox") as ComboBox;

            if (freqTextBox != null && unitComboBox != null &&
                double.TryParse(freqTextBox.Text, out double frequency))
            {
                string freqUnit = UnitConversionUtility.GetFrequencyUnit(unitComboBox);
                double freqMultiplier = UnitConversionUtility.GetFrequencyMultiplier(freqUnit);
                return frequency * freqMultiplier;
            }

            return 1000.0; // Default 1kHz
        }

        private double GetAmplitudeFromUI()
        {
            TextBox ampTextBox = FindControl("ChannelAmplitudeTextBox") as TextBox;
            ComboBox unitComboBox = FindControl("ChannelAmplitudeUnitComboBox") as ComboBox;

            if (ampTextBox != null && unitComboBox != null &&
                double.TryParse(ampTextBox.Text, out double amplitude))
            {
                string ampUnit = UnitConversionUtility.GetAmplitudeUnit(unitComboBox);
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
            if (_categoryComboBox != null)
            {
                Window mainWindow = Window.GetWindow(_categoryComboBox);
                return mainWindow?.FindName(controlName);
            }
            return null;
        }

        #endregion

        #region Public Methods

        // Refresh the arbitrary waveform settings from the device
        public void RefreshArbitraryWaveformSettings()
        {
            try
            {
                // Get current arbitrary waveform type
                string waveformType = _device.GetArbitraryWaveformType(_activeChannel);

                // Try to get the friendly name and category
                string friendlyName = _device.GetCurrentArbitraryWaveformName(_activeChannel);
                var category = _device.GetCurrentArbitraryWaveformCategory(_activeChannel);

                // Select the correct category in the combo box if found
                if (category.HasValue && _categoryComboBox != null)
                {
                    _categoryComboBox.SelectedItem = category.Value.ToString();
                    LoadArbitraryWaveformsForCategory();
                }

                // Select the correct waveform in the combo box if found
                if (!string.IsNullOrEmpty(friendlyName) && _waveformComboBox != null)
                {
                    foreach (var item in _waveformComboBox.Items)
                    {
                        if (item.ToString() == friendlyName)
                        {
                            _waveformComboBox.SelectedItem = item;
                            break;
                        }
                    }
                }

                // Update parameter controls based on selected waveform
                if (_waveformInfoTextBlock != null)
                {
                    // Get description parts from ArbitraryWaveformDescriptions
                    string basicInfo = Continuous.ArbitraryWaveform.Descriptions.ArbitraryWaveformDescriptions.GetBasicInfo(waveformName);
                    string paramInfo = Continuous.ArbitraryWaveform.Descriptions.ArbitraryWaveformDescriptions.GetParameterInfo(waveformName);
                    string applicationInfo = Continuous.ArbitraryWaveform.Descriptions.ArbitraryWaveformDescriptions.GetApplicationInfo(waveformName);

                    // Set main info text (without applications)
                    _waveformInfoTextBlock.Text = basicInfo + "\n\n" + paramInfo + "\n\nChanges are applied automatically.";

                    // Set applications text (including the header)
                    if (_waveformApplicationsTextBlock != null)
                    {
                        _waveformApplicationsTextBlock.Text = "Common Applications:\n" + applicationInfo;
                    }
                }

                Log($"Refreshed arbitrary waveform settings for Channel {_activeChannel}");
            }
            catch (Exception ex)
            {
                Log($"Error refreshing arbitrary waveform settings: {ex.Message}");
            }
        }

        #endregion
    }
}