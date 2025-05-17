using System;
using System.Windows.Controls;

namespace DG2072_USB_Control.Services
{
    /// <summary>
    /// Utility class for unit conversion operations
    /// </summary>
    public static class UnitConversionUtility
    {
        /// <summary>
        /// Gets the frequency unit string from a ComboBox selection
        /// </summary>
        public static string GetFrequencyUnit(ComboBox comboBox)
        {
            if (comboBox.SelectedItem == null)
                return "Hz";

            return ((ComboBoxItem)comboBox.SelectedItem).Content.ToString();
        }

        /// <summary>
        /// Gets the amplitude unit string from a ComboBox selection
        /// </summary>
        public static string GetAmplitudeUnit(ComboBox comboBox)
        {
            if (comboBox.SelectedItem == null)
                return "Vpp";

            return ((ComboBoxItem)comboBox.SelectedItem).Content.ToString();
        }

        /// <summary>
        /// Gets the multiplier value for converting a frequency to Hz
        /// </summary>
        public static double GetFrequencyMultiplier(string unit)
        {
            switch (unit)
            {
                case "Hz": return 1.0;
                case "kHz": return 1.0e3;
                case "MHz": return 1.0e6;
                case "mHz": return 1.0e-3;
                case "µHz": return 1.0e-6;
                default: return 1.0;
            }
        }

        /// <summary>
        /// Gets the multiplier value for converting an amplitude to Vpp
        /// </summary>
        public static double GetAmplitudeMultiplier(string unit)
        {
            switch (unit)
            {
                case "Vpp": return 1.0;
                case "mVpp": return 1.0e-3;
                case "Vrms": return 2.0 * Math.Sqrt(2.0); // Convert Vrms to Vpp for sine waves
                case "mVrms": return 2.0e-3 * Math.Sqrt(2.0); // Convert mVrms to Vpp for sine waves
                default: return 1.0;
            }
        }

        /// <summary>
        /// Converts a frequency value to its base µHz value (preserving resolution)
        /// </summary>
        public static double ConvertToMicroHz(double value, string unit)
        {
            switch (unit)
            {
                case "Hz": return value * 1e6;
                case "kHz": return value * 1e9;
                case "MHz": return value * 1e12;
                case "mHz": return value * 1e3;
                case "µHz": return value;
                default: return value * 1e6; // Default to Hz
            }
        }

        /// <summary>
        /// Converts a µHz value to the specified unit
        /// </summary>
        public static double ConvertFromMicroHz(double microHzValue, string unit)
        {
            switch (unit)
            {
                case "Hz": return microHzValue / 1e6;
                case "kHz": return microHzValue / 1e9;
                case "MHz": return microHzValue / 1e12;
                case "mHz": return microHzValue / 1e3;
                case "µHz": return microHzValue;
                default: return microHzValue / 1e6; // Default to Hz
            }
        }

        /// <summary>
        /// Gets the period unit string from a ComboBox selection
        /// </summary>
        public static string GetPeriodUnit(ComboBox comboBox)
        {
            if (comboBox.SelectedItem == null)
                return "s";

            return ((ComboBoxItem)comboBox.SelectedItem).Content.ToString();
        }

        /// <summary>
        /// Gets the multiplier value for converting a period to seconds
        /// </summary>
        public static double GetPeriodMultiplier(string unit)
        {
            switch (unit)
            {
                case "s": return 1.0;
                case "ms": return 1.0e-3;
                case "µs": return 1.0e-6;
                case "ns": return 1.0e-9;
                case "ps": return 1.0e-12;
                default: return 1.0;
            }
        }

        /// <summary>
        /// Converts a period value to its base ps value (preserving resolution)
        /// </summary>
        public static double ConvertToPicoSeconds(double value, string unit)
        {
            switch (unit)
            {
                case "s": return value * 1e12;
                case "ms": return value * 1e9;
                case "µs": return value * 1e6;
                case "ns": return value * 1e3;
                case "ps": return value;
                default: return value * 1e12; // Default to seconds
            }
        }

        /// <summary>
        /// Converts a ps value to the specified unit
        /// </summary>
        public static double ConvertFromPicoSeconds(double picoSecondsValue, string unit)
        {
            switch (unit)
            {
                case "s": return picoSecondsValue / 1e12;
                case "ms": return picoSecondsValue / 1e9;
                case "µs": return picoSecondsValue / 1e6;
                case "ns": return picoSecondsValue / 1e3;
                case "ps": return picoSecondsValue;
                default: return picoSecondsValue / 1e12; // Default to seconds
            }
        }

        /// <summary>
        /// Converts a frequency in Hz to period in seconds
        /// </summary>
        public static double FrequencyToPeriod(double frequencyHz)
        {
            if (frequencyHz == 0)
                throw new DivideByZeroException("Cannot convert zero frequency to period");

            return 1.0 / frequencyHz;
        }

        /// <summary>
        /// Converts a period in seconds to frequency in Hz
        /// </summary>
        public static double PeriodToFrequency(double periodSeconds)
        {
            if (periodSeconds == 0)
                throw new DivideByZeroException("Cannot convert zero period to frequency");

            return 1.0 / periodSeconds;
        }

        /// <summary>
        /// Formats a double value with at least the specified minimum decimal places,
        /// but preserves significant decimal places beyond the minimum.
        /// </summary>
        /// 

        /// <summary>
        /// Formats a double value with appropriate number of decimal places based on magnitude
        /// </summary>
        public static string FormatWithMinimumDecimals(double value, int minDecimals = 2)
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

        /// <summary>
        /// Adjusts a value and unit based on auto-ranging rules
        /// </summary>
        public static void AdjustValueAndUnit(TextBox textBox, ComboBox unitComboBox, string[] units,
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
                textBox.Text = FormatWithMinimumDecimals(displayValue);

                // Find and select the unit in the combo box
                for (int i = 0; i < unitComboBox.Items.Count; i++)
                {
                    ComboBoxItem item = unitComboBox.Items[i] as ComboBoxItem;
                    if (item != null && item.Content.ToString() == units[unitIndex])
                    {
                        unitComboBox.SelectedIndex = i;
                        break;
                    }
                }
            }
            catch
            {
                // In case of error, leave as is
            }
        }

        /// <summary>
        /// Gets the offset unit string from a ComboBox selection
        /// </summary>
        public static string GetOffsetUnit(ComboBox comboBox)
        {
            if (comboBox.SelectedItem == null)
                return "V";

            return ((ComboBoxItem)comboBox.SelectedItem).Content.ToString();
        }

        /// <summary>
        /// Gets the multiplier value for converting an offset to V
        /// </summary>
        public static double GetOffsetMultiplier(string unit)
        {
            switch (unit)
            {
                case "V": return 1.0;
                case "mV": return 1.0e-3;
                default: return 1.0;
            }
        }

    }
}