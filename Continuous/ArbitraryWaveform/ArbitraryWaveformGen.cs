using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DG2072_USB_Control.Services;
using DG2072_USB_Control.Continuous.ArbitraryWaveform.Descriptions;

namespace DG2072_USB_Control.Continuous.ArbitraryWaveform
{
    public class ArbitraryWaveformGen : WaveformGenerator, IArbitraryWaveformEventHandler
    {
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

        // Constructor
        public ArbitraryWaveformGen(RigolDG2072 device, int channel, Window mainWindow)
            : base(device, channel, mainWindow)
        {
            // Initialize UI references
            _applyButton = mainWindow.FindName("ApplyArbitraryWaveformButton") as Button;
            _categoryComboBox = mainWindow.FindName("ArbitraryWaveformCategoryComboBox") as ComboBox;
            _parametersGroupBox = mainWindow.FindName("WaveformParametersGroup") as GroupBox;
            _param1DockPanel = mainWindow.FindName("ArbitraryParam1DockPanel") as DockPanel;
            _param2DockPanel = mainWindow.FindName("ArbitraryParam2DockPanel") as DockPanel;
            _param1TextBox = mainWindow.FindName("ArbitraryParam1TextBox") as TextBox;
            _param2TextBox = mainWindow.FindName("ArbitraryParam2TextBox") as TextBox;
            _param1UnitTextBlock = mainWindow.FindName("ArbitraryParam1UnitTextBlock") as TextBlock;
            _param2UnitTextBlock = mainWindow.FindName("ArbitraryParam2UnitTextBlock") as TextBlock;
            _waveformComboBox = mainWindow.FindName("ArbitraryWaveformComboBox") as ComboBox;
            _waveformApplicationsTextBlock = mainWindow.FindName("WaveformApplicationsTextBlock") as TextBlock;
            _waveformInfoTextBlock = mainWindow.FindName("ArbitraryWaveformInfoTextBlock") as TextBlock;

            // Get the Labels from the DockPanels
            if (_param1DockPanel != null && _param1DockPanel.Children.Count > 0)
                _param1Label = _param1DockPanel.Children[0] as Label;

            if (_param2DockPanel != null && _param2DockPanel.Children.Count > 0)
                _param2Label = _param2DockPanel.Children[0] as Label;

            // Initialize UI controls
            InitializeArbitraryWaveformControls();
        }

        #region WaveformGenerator Abstract Methods Implementation

        /// <summary>
        /// Override from base class to apply arbitrary waveform parameters
        /// </summary>
        public override void ApplyParameters()
        {
            if (!IsDeviceConnected()) return;

            ApplyArbitraryWaveform();
        }

        /// <summary>
        /// Override from base class to refresh arbitrary waveform settings from device
        /// </summary>
        public override void RefreshParameters()
        {
            if (!IsDeviceConnected()) return;

            RefreshArbitraryWaveformSettings();
        }

        #endregion

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
                string selectedWaveform;

                // Check if the selected item is a ComboBoxItem
                if (_waveformComboBox.SelectedItem is ComboBoxItem comboBoxItem)
                {
                    // Use Tag if available, otherwise fall back to Content
                    selectedWaveform = comboBoxItem.Tag?.ToString() ?? comboBoxItem.Content.ToString();
                }
                else
                {
                    // Fall back to the original behavior
                    selectedWaveform = _waveformComboBox.SelectedItem.ToString();
                }

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
                // Use base class helper method for timer management
                CreateOrResetTimer(ref _param1UpdateTimer, () =>
                {
                    if (double.TryParse(_param1TextBox.Text, out double param))
                    {
                        // Update will happen when Apply button is clicked
                        Log($"Parameter 1 set to {param}");
                    }
                });
            }
            else if (textBox == _param2TextBox)
            {
                // Use base class helper method for timer management
                CreateOrResetTimer(ref _param2UpdateTimer, () =>
                {
                    if (double.TryParse(_param2TextBox.Text, out double param))
                    {
                        Log($"Parameter 2 set to {param}");
                    }
                });
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

        // Initialize the arbitrary waveform controls
        private void InitializeArbitraryWaveformControls()
        {
            try
            {
                // Clear existing items
                if (_categoryComboBox != null)
                    _categoryComboBox.Items.Clear();

                // Get all categories from the RigolDG2072 instance
                var categories = Device.GetArbitraryWaveformCategories();

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
                    var waveforms = Device.GetArbitraryWaveformNames(selectedCategory);

                    // Add each waveform to the ComboBox with a descriptive name
                    foreach (var waveform in waveforms)
                    {
                        string descriptiveName = Device.GetArbitraryWaveformDescription(waveform);

                        // Create a ComboBoxItem with the descriptive name as Content
                        // and the original code as Tag for reference when sending to device
                        ComboBoxItem item = new ComboBoxItem();
                        item.Content = descriptiveName;
                        item.Tag = waveform;  // Store original code

                        _waveformComboBox.Items.Add(item);
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
                // Reset parameters to defaults
                if (_param1TextBox != null) _param1TextBox.Text = "1.0";
                if (_param2TextBox != null) _param2TextBox.Text = "1.0";

                // Update the waveform info text
                if (_waveformInfoTextBlock != null)
                {
                    // Get comprehensive description
                    string fullDescription = ArbitraryWaveformDescriptions.GetDetailedDescription(waveformName);

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

                // Get the selected category
                if (Enum.TryParse(_categoryComboBox.SelectedItem.ToString(),
                                  out RigolDG2072.ArbitraryWaveformCategory selectedCategory))
                {
                    // Extract the original waveform code from the ComboBoxItem
                    string selectedArbWaveform;

                    // Check if the selected item is a ComboBoxItem
                    if (_waveformComboBox.SelectedItem is ComboBoxItem comboBoxItem)
                    {
                        // Get the original code from Tag
                        selectedArbWaveform = comboBoxItem.Tag?.ToString();

                        // If Tag is null, fall back to Content
                        if (string.IsNullOrEmpty(selectedArbWaveform))
                        {
                            selectedArbWaveform = comboBoxItem.Content.ToString();
                        }
                    }
                    else
                    {
                        // Fall back to the original behavior
                        selectedArbWaveform = _waveformComboBox.SelectedItem.ToString();
                    }

                    // Get current parameters from UI using base class methods
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
                    Device.SetArbitraryWaveform(ActiveChannel, selectedCategory, selectedArbWaveform);

                    // Apply basic parameters
                    Device.SetFrequency(ActiveChannel, frequency);
                    Device.SetAmplitude(ActiveChannel, amplitude);
                    Device.SetOffset(ActiveChannel, offset);
                    Device.SetPhase(ActiveChannel, phase);

                    // Apply additional parameters if available
                    if (additionalParams.Count > 0)
                    {
                        // Log the additional parameters
                        foreach (var param in additionalParams)
                        {
                            Log($"Parameter {param.Key}: {param.Value}");
                        }
                    }

                    Log($"Applied {selectedArbWaveform} arbitrary waveform from {selectedCategory} category to Channel {ActiveChannel}");
                }
            }
            catch (Exception ex)
            {
                Log($"Error applying arbitrary waveform: {ex.Message}");
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Refresh the arbitrary waveform settings from the device
        /// </summary>
        public void RefreshArbitraryWaveformSettings()
        {
            try
            {
                // Get current arbitrary waveform type
                string waveformType = Device.GetArbitraryWaveformType(ActiveChannel);

                // Try to get the friendly name and category
                string deviceWaveformName = Device.GetCurrentArbitraryWaveformName(ActiveChannel);
                var category = Device.GetCurrentArbitraryWaveformCategory(ActiveChannel);

                // Select the correct category in the combo box if found
                if (category.HasValue && _categoryComboBox != null)
                {
                    _categoryComboBox.SelectedItem = category.Value.ToString();
                    LoadArbitraryWaveformsForCategory();
                }

                // Select the correct waveform in the combo box if found
                if (!string.IsNullOrEmpty(deviceWaveformName) && _waveformComboBox != null)
                {
                    // Look for ComboBoxItem with matching Tag (original waveform code)
                    foreach (var item in _waveformComboBox.Items)
                    {
                        if (item is ComboBoxItem comboBoxItem &&
                            comboBoxItem.Tag?.ToString() == deviceWaveformName)
                        {
                            _waveformComboBox.SelectedItem = item;
                            break;
                        }
                        // Fallback to old behavior if Tag isn't set
                        else if (item.ToString() == deviceWaveformName)
                        {
                            _waveformComboBox.SelectedItem = item;
                            break;
                        }
                    }
                }

                // Update parameter controls based on selected waveform
                if (_waveformComboBox?.SelectedItem != null)
                {
                    string selectedWaveform;

                    // Check if the selected item is a ComboBoxItem
                    if (_waveformComboBox.SelectedItem is ComboBoxItem comboBoxItem)
                    {
                        // Use Tag if available, otherwise fall back to Content
                        selectedWaveform = comboBoxItem.Tag?.ToString() ?? comboBoxItem.Content.ToString();
                    }
                    else
                    {
                        // Fall back to the original behavior
                        selectedWaveform = _waveformComboBox.SelectedItem.ToString();
                    }

                    UpdateArbitraryWaveformParameters(selectedWaveform);
                }

                Log($"Refreshed arbitrary waveform settings for Channel {ActiveChannel}");
            }
            catch (Exception ex)
            {
                Log($"Error refreshing arbitrary waveform settings: {ex.Message}");
            }
        }

        #endregion
    }
}