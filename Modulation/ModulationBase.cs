using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using DG2072_USB_Control.Services;

namespace DG2072_USB_Control.Modulation
{
    /// <summary>
    /// Base class for all modulation types
    /// </summary>
    public abstract class ModulationBase
    {
        // Common properties
        protected readonly RigolDG2072 Device;
        protected int ActiveChannelNumber;
        protected readonly Window MainWindow;

        // Event for logging
        public event EventHandler<string> LogEvent;

        /// <summary>
        /// Constructor with common initialization
        /// </summary>
        protected ModulationBase(RigolDG2072 device, int channel, Window mainWindow)
        {
            Device = device;
            ActiveChannelNumber = channel;
            MainWindow = mainWindow;
        }

        /// <summary>
        /// Property for the active channel
        /// </summary>
        public int ActiveChannel
        {
            get => ActiveChannelNumber;
            set => ActiveChannelNumber = value;
        }

        /// <summary>
        /// Helper method for logging
        /// </summary>
        protected void Log(string message)
        {
            LogEvent?.Invoke(this, message);
        }

        /// <summary>
        /// Check if the device is connected
        /// </summary>
        protected bool IsDeviceConnected()
        {
            return Device != null && Device.IsConnected;
        }

        /// <summary>
        /// Apply the modulation-specific parameters
        /// </summary>
        public abstract void ApplyModulation();

        /// <summary>
        /// Enable or disable the modulation
        /// </summary>
        public abstract void SetModulationState(bool enabled);

        /// <summary>
        /// Set the modulation source (Internal/External)
        /// </summary>
        public abstract void SetModulationSource(string source);

        /// <summary>
        /// Refresh the modulation-specific parameters from the device
        /// </summary>
        public abstract void RefreshParameters();

        /// <summary>
        /// Find a control by name in the main window
        /// </summary>
        protected object FindControl(string controlName)
        {
            return MainWindow?.FindName(controlName);
        }

        /// <summary>
        /// Get the text value from a TextBox control
        /// </summary>
        protected string GetTextBoxValue(string controlName, string defaultValue = "")
        {
            TextBox textBox = FindControl(controlName) as TextBox;
            return textBox?.Text ?? defaultValue;
        }

        /// <summary>
        /// Get a double value from a TextBox control
        /// </summary>
        protected double GetDoubleFromTextBox(string controlName, double defaultValue = 0.0)
        {
            string text = GetTextBoxValue(controlName);
            return double.TryParse(text, out double value) ? value : defaultValue;
        }

        /// <summary>
        /// Get the selected item text from a ComboBox
        /// </summary>
        protected string GetComboBoxSelectedValue(string controlName, string defaultValue = "")
        {
            ComboBox comboBox = FindControl(controlName) as ComboBox;
            if (comboBox?.SelectedItem is ComboBoxItem selectedItem)
            {
                return selectedItem.Content.ToString();
            }
            return defaultValue;
        }

        /// <summary>
        /// Check if a ToggleButton is checked
        /// </summary>
        protected bool IsToggleButtonChecked(string controlName, bool defaultValue = false)
        {
            System.Windows.Controls.Primitives.ToggleButton toggleButton = 
                FindControl(controlName) as System.Windows.Controls.Primitives.ToggleButton;
            return toggleButton?.IsChecked ?? defaultValue;
        }
    }
}