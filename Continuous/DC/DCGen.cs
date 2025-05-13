using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DG2072_USB_Control.Services;

namespace DG2072_USB_Control.Continuous.DC
{
    public class DCGen : IDCEventHandler
    {
        // Device reference
        private readonly RigolDG2072 _device;

        // Active channel
        private int _activeChannel;

        // UI elements
        private readonly TextBox _dcVoltageTextBox;
        private readonly ComboBox _dcVoltageUnitComboBox;
        private readonly ComboBox _dcImpedanceComboBox;

        // Update timer for debouncing
        private DispatcherTimer _dcVoltageUpdateTimer;

        // Event for logging
        public event EventHandler<string> LogEvent;

        // Constructor
        public DCGen(RigolDG2072 device, int channel, Window mainWindow)
        {
            _device = device;
            _activeChannel = channel;

            // Initialize UI references
            _dcVoltageTextBox = mainWindow.FindName("DCVoltageTextBox") as TextBox;
            _dcVoltageUnitComboBox = mainWindow.FindName("DCVoltageUnitComboBox") as ComboBox;
            _dcImpedanceComboBox = mainWindow.FindName("DCImpedanceComboBox") as ComboBox;
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

        #region IDCEventHandler Implementation

        public void OnDCVoltageTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsDeviceConnected()) return;
            if (!double.TryParse(_dcVoltageTextBox.Text, out double voltage)) return;

            // Use a timer to debounce rapid changes
            if (_dcVoltageUpdateTimer == null)
            {
                _dcVoltageUpdateTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                _dcVoltageUpdateTimer.Tick += (s, args) =>
                {
                    _dcVoltageUpdateTimer.Stop();
                    if (double.TryParse(_dcVoltageTextBox.Text, out double volt))
                    {
                        ApplyDCVoltage(volt);
                    }
                };
            }

            _dcVoltageUpdateTimer.Stop();
            _dcVoltageUpdateTimer.Start();
        }

        public void OnDCVoltageLostFocus(object sender, RoutedEventArgs e)
        {
            if (!IsDeviceConnected()) return;
            if (double.TryParse(_dcVoltageTextBox.Text, out double voltage))
            {
                // Format the value with the appropriate number of decimal places
                _dcVoltageTextBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(voltage);

                // Apply the formatted value
                ApplyDCVoltage(voltage);
            }
        }

        public void OnDCVoltageUnitChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsDeviceConnected()) return;
            if (!double.TryParse(_dcVoltageTextBox.Text, out double voltage)) return;

            string unitStr = ((ComboBoxItem)_dcVoltageUnitComboBox.SelectedItem).Content.ToString();
            double multiplier = unitStr == "mV" ? 0.001 : 1.0;  // Convert mV to V if needed

            ApplyDCVoltage(voltage * multiplier);
        }

        public void OnDCImpedanceChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsDeviceConnected()) return;

            string impedanceStr = ((ComboBoxItem)_dcImpedanceComboBox.SelectedItem).Content.ToString();

            try
            {
                if (impedanceStr == "High-Z")
                {
                    // Set to high impedance
                    _device.SendCommand($":OUTP{_activeChannel}:IMP INF");
                }
                else if (impedanceStr.EndsWith("Ω"))
                {
                    double impedance = 50.0; // Default value

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
                    _device.SendCommand($":OUTP{_activeChannel}:IMP {impedance}");
                }

                Log($"Set output impedance for channel {_activeChannel} to {impedanceStr}");

                // After changing impedance, we need to reapply the DC voltage
                // since the voltage displayed may change based on load impedance
                if (double.TryParse(_dcVoltageTextBox.Text, out double voltage))
                {
                    string unitStr = ((ComboBoxItem)_dcVoltageUnitComboBox.SelectedItem).Content.ToString();
                    double multiplier = unitStr == "mV" ? 0.001 : 1.0;
                    ApplyDCVoltage(voltage * multiplier);
                }
            }
            catch (Exception ex)
            {
                Log($"Error setting impedance: {ex.Message}");
            }
        }

        #endregion

        #region Core Functionality

        // Check if the device is connected
        private bool IsDeviceConnected()
        {
            return _device != null && _device.IsConnected;
        }

        // Apply DC voltage to the device
        private void ApplyDCVoltage(double voltage)
        {
            if (!IsDeviceConnected()) return;

            try
            {
                // For DC, we use the APPLY:DC command with placeholders for frequency and amplitude
                _device.SetDCVoltage(_activeChannel, voltage);
                Log($"Set DC voltage for channel {_activeChannel} to {voltage} V");
            }
            catch (Exception ex)
            {
                Log($"Error applying DC voltage: {ex.Message}");
            }
        }

        #endregion

        #region Public Methods

        // Update DC voltage value in the UI from device
        public void UpdateDCVoltageValue()
        {
            if (!IsDeviceConnected()) return;

            try
            {
                double dcVoltage = _device.GetDCVoltage(_activeChannel);

                // Check current unit setting and adjust displayed value
                string unit = ((ComboBoxItem)_dcVoltageUnitComboBox.SelectedItem).Content.ToString();
                if (unit == "mV")
                {
                    _dcVoltageTextBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(dcVoltage * 1000);
                }
                else
                {
                    _dcVoltageTextBox.Text = UnitConversionUtility.FormatWithMinimumDecimals(dcVoltage);
                }
            }
            catch (Exception ex)
            {
                Log($"Error updating DC voltage value: {ex.Message}");
            }
        }

        // Update the impedance ComboBox based on the current device setting
        public void UpdateImpedanceSelection()
        {
            if (!IsDeviceConnected()) return;

            try
            {
                double impedance = _device.GetOutputImpedance(_activeChannel);

                // Select the appropriate impedance in the combo box
                if (double.IsInfinity(impedance))
                {
                    // High-Z
                    for (int i = 0; i < _dcImpedanceComboBox.Items.Count; i++)
                    {
                        ComboBoxItem item = _dcImpedanceComboBox.Items[i] as ComboBoxItem;
                        if (item != null && item.Content.ToString() == "High-Z")
                        {
                            _dcImpedanceComboBox.SelectedIndex = i;
                            break;
                        }
                    }
                }
                else
                {
                    // Find closest match
                    ComboBoxItem bestMatch = null;
                    double bestDifference = double.MaxValue;

                    foreach (ComboBoxItem item in _dcImpedanceComboBox.Items)
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
                        _dcImpedanceComboBox.SelectedItem = bestMatch;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error updating impedance selection: {ex.Message}");
            }
        }

        // Apply all DC parameters
        public void ApplyDCParameters()
        {
            if (!IsDeviceConnected()) return;

            try
            {
                // Apply DC voltage
                if (double.TryParse(_dcVoltageTextBox.Text, out double voltage))
                {
                    string unitStr = ((ComboBoxItem)_dcVoltageUnitComboBox.SelectedItem).Content.ToString();
                    double multiplier = unitStr == "mV" ? 0.001 : 1.0;
                    ApplyDCVoltage(voltage * multiplier);
                }

                // Apply impedance setting
                if (_dcImpedanceComboBox.SelectedItem != null)
                {
                    string impedanceStr = ((ComboBoxItem)_dcImpedanceComboBox.SelectedItem).Content.ToString();
                    if (impedanceStr == "High-Z")
                    {
                        _device.SetOutputImpedanceHighZ(_activeChannel);
                    }
                    else
                    {
                        double impedance = 50.0; // Default

                        // Parse the value
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

                        _device.SetOutputImpedance(_activeChannel, impedance);
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error applying DC parameters: {ex.Message}");
            }
        }

        // Refresh all DC settings from the device
        public void RefreshDCSettings()
        {
            if (!IsDeviceConnected()) return;

            UpdateDCVoltageValue();
            UpdateImpedanceSelection();
        }

        #endregion
    }
}