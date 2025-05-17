using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace DG2072_USB_Control.Services
{
    /// <summary>
    /// Base class for all waveform generators
    /// </summary>
    public abstract class WaveformGenerator
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
        protected WaveformGenerator(RigolDG2072 device, int channel, Window mainWindow)
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
        /// Apply the waveform-specific parameters
        /// </summary>
        public abstract void ApplyParameters();

        /// <summary>
        /// Refresh the waveform-specific parameters from the device
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
        /// Helper to create and manage a debounce timer
        /// </summary>
        protected void CreateOrResetTimer(ref DispatcherTimer timer, Action action, int delayMs = 500)
        {
            if (timer == null)
            {
                timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delayMs) };

                // Create a local copy of the timer to avoid capturing the ref parameter
                DispatcherTimer localTimer = timer;
                Action localAction = action;
                timer.Tick += (s, args) =>
                {
                    localTimer.Stop();
                    localAction();
                };
            }

            timer.Stop();
            timer.Start();
        }

        /// <summary>
        /// Helper method to adjust time values and units automatically
        /// </summary>
        protected void AdjustTimeAndUnit(TextBox textBox, ComboBox unitComboBox, string[] units,
            Func<double, string, double> toBaseUnit, Func<double, string, double> fromBaseUnit)
        {
            if (textBox == null || unitComboBox == null) return;
            if (!double.TryParse(textBox.Text, out double value)) return;

            try
            {
                string currentUnit = ((ComboBoxItem)unitComboBox.SelectedItem)?.Content.ToString();
                if (string.IsNullOrEmpty(currentUnit)) return;

                // Convert to base unit
                double baseValue = toBaseUnit(value, currentUnit);

                // Find the unit index
                int unitIndex = 0;
                for (int i = 0; i < units.Length; i++)
                {
                    if (units[i] == currentUnit)
                    {
                        unitIndex = i;
                        break;
                    }
                }

                // Calculate display value in the current unit
                double displayValue = fromBaseUnit(baseValue, units[unitIndex]);

                // Auto-range: handle values that are too large
                while (displayValue > 9999 && unitIndex < units.Length - 1)
                {
                    unitIndex++;
                    displayValue = fromBaseUnit(baseValue, units[unitIndex]);
                }

                // Auto-range: handle values that are too small
                while (displayValue < 0.1 && unitIndex > 0)
                {
                    unitIndex--;
                    displayValue = fromBaseUnit(baseValue, units[unitIndex]);
                }

                // Update the textbox with formatted value
                textBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(displayValue);

                // Find and select the unit in the combo box
                SelectUnitInComboBox(unitComboBox, units[unitIndex]);
            }
            catch (Exception ex)
            {
                Log($"Error adjusting value and unit: {ex.Message}");
            }
        }

        /// <summary>
        /// Select a unit in a ComboBox
        /// </summary>
        protected void SelectUnitInComboBox(ComboBox comboBox, string unitToSelect)
        {
            for (int i = 0; i < comboBox.Items.Count; i++)
            {
                ComboBoxItem item = comboBox.Items[i] as ComboBoxItem;
                if (item != null && item.Content.ToString() == unitToSelect)
                {
                    comboBox.SelectedIndex = i;
                    break;
                }
            }
        }

        /// <summary>
        /// Helper methods to get values from MainWindow UI
        /// </summary>
        protected double GetFrequencyFromUI()
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

        protected double GetAmplitudeFromUI()
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

        protected double GetOffsetFromUI()
        {
            TextBox offsetTextBox = FindControl("ChannelOffsetTextBox") as TextBox;

            if (offsetTextBox != null && double.TryParse(offsetTextBox.Text, out double offset))
            {
                return offset;
            }

            return 0.0; // Default 0V
        }

        protected double GetPhaseFromUI()
        {
            TextBox phaseTextBox = FindControl("ChannelPhaseTextBox") as TextBox;

            if (phaseTextBox != null && double.TryParse(phaseTextBox.Text, out double phase))
            {
                return phase;
            }

            return 0.0; // Default 0°
        }
    }
}