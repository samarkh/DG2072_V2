using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Controls.Primitives; // Add this at the top of the file
using DG2072_USB_Control.Services;

namespace DG2072_USB_Control.Continuous.Harmonics
{
    /// <summary>
    /// Manages harmonics functionality for the DG2072 device
    /// </summary>
    public class HarmonicsManager
    {
        private readonly RigolDG2072 _device;
        private int _activeChannel;

        // Store last settings for change detection
        private Dictionary<int, double> _lastAmplitudes = new Dictionary<int, double>();
        private Dictionary<int, double> _lastPhases = new Dictionary<int, double>();
        private bool[] _lastEnabledHarmonics = new bool[7];
        // Add these fields to the HarmonicsManager class
        private readonly ToggleButton _harmonicsToggle;
        private readonly bool _isPercentageMode;

        // Event for logging
        public event EventHandler<string> LogEvent;

        public HarmonicsManager(RigolDG2072 device, int channel)
        {
            _device = device;
            _activeChannel = channel;
        }

        // Method to change active channel
        public void SetActiveChannel(int channel)
        {
            if (channel < 1 || channel > 2)
                throw new ArgumentException("Channel must be 1 or 2");

            _activeChannel = channel;
        }

        // Log helper method
        private void Log(string message)
        {
            LogEvent?.Invoke(this, message);
        }

        /// <summary>
        /// Builds a harmonic pattern string based on the enabled harmonics
        /// </summary>
        public string BuildHarmonicPattern(bool[] enabledHarmonics)
        {
            // Build the pattern string
            char[] pattern = new char[8];
            pattern[0] = 'X'; // Fundamental always enabled

            for (int i = 0; i < 7 && i < enabledHarmonics.Length; i++)
            {
                pattern[i + 1] = enabledHarmonics[i] ? '1' : '0';
            }

            return new string(pattern);
        }

        ///// <summary>
        ///// Updates harmonic amplitudes when the fundamental amplitude changes
        ///// </summary>
        //public void UpdateHarmonicsForFundamentalChange(double newFundamentalAmplitude)
        //{
        //    try
        //    {
        //        // Only proceed if harmonics are enabled and in percentage mode
        //        if (_harmonicsToggle.IsChecked != true || !_isPercentageMode)
        //            return;

        //        // Store the new fundamental amplitude
        //        _fundamentalAmplitude = newFundamentalAmplitude;

        //        // Get current UI values (which are percentages in the UI)
        //        bool[] enabledHarmonics = GetEnabledHarmonics();
        //        Dictionary<int, double> percentages = GetHarmonicAmplitudes();
        //        Dictionary<int, double> phases = GetHarmonicPhases();

        //        // Apply all settings - this will convert percentages to absolute values
        //        // using the new fundamental amplitude
        //        // Replace the problematic line with the following:
        //        ApplyHarmonicSettings(enabledHarmonics, percentages, phases, _isPercentageMode);
        //        _harmonicsManager.ApplyHarmonicSettings(enabledHarmonics, percentages, phases, _isPercentageMode);

        //        Log("Harmonic amplitudes updated for new fundamental amplitude");
        //    }
        //    catch (Exception ex)
        //    {
        //        Log($"Error updating harmonics for fundamental change: {ex.Message}");
        //    }
        //}



        /// <summary>
        /// Updates the harmonic pattern on the device
        /// </summary>
        public void UpdateHarmonicPattern(bool[] enabledHarmonics)
        {
            try
            {
                // Find the highest enabled harmonic
                int highestHarmonic = 2;  // Default to 2
                for (int i = 6; i >= 0 && i < enabledHarmonics.Length; i--)
                {
                    if (enabledHarmonics[i])
                    {
                        highestHarmonic = i + 2;
                        break;
                    }
                }

                // Check if any harmonics are enabled
                bool anyHarmonicEnabled = enabledHarmonics.Any(x => x);
                if (!anyHarmonicEnabled)
                {
                    Log("Warning: No harmonics are enabled.");
                    return;
                }

                // Set type to USER
                _device.SendCommand($":SOUR{_activeChannel}:HARM:TYPE USER");
                Thread.Sleep(50);

                // Set order to highest enabled harmonic
                _device.SendCommand($":SOUR{_activeChannel}:HARM:ORDE {highestHarmonic}");
                Thread.Sleep(50);

                // Create and set the user pattern
                string userPattern = BuildHarmonicPattern(enabledHarmonics);
                _device.SendCommand($":SOUR{_activeChannel}:HARM:USER {userPattern}");
                Thread.Sleep(50);
            }
            catch (Exception ex)
            {
                Log($"Error updating harmonic pattern: {ex.Message}");
            }
        }

        /// <summary>
        /// Toggles the harmonic state on/off
        /// </summary>
        public void SetHarmonicState(bool enabled)
        {
            try
            {
                if (enabled)
                {
                    // Enable harmonic mode
                    _device.SendCommand($":SOUR{_activeChannel}:HARM:STAT ON");
                    Log($"Enabled harmonics for Channel {_activeChannel}");
                }
                else
                {
                    // Disable harmonic mode and reset pattern
                    _device.SendCommand($":SOUR{_activeChannel}:HARM:STAT OFF");
                    _device.SendCommand($":SOUR{_activeChannel}:HARM:USER X0000000");
                    Log($"Disabled harmonics for Channel {_activeChannel} and reset user pattern");
                }
            }
            catch (Exception ex)
            {
                Log($"Error setting harmonic state: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies a harmonic amplitude value
        /// </summary>
        public void SetHarmonicAmplitude(int harmonicNumber, double amplitude, bool isPercentage)
        {
            try
            {
                if (harmonicNumber < 2 || harmonicNumber > 8)
                    throw new ArgumentException("Harmonic number must be between 2 and 8");

                // Convert percentage to absolute if needed
                if (isPercentage)
                {
                    double fundamentalAmplitude = _device.GetAmplitude(_activeChannel);
                    amplitude = (amplitude / 100.0) * fundamentalAmplitude;
                }

                // Set the amplitude
                _device.SendCommand($":SOUR{_activeChannel}:HARM:AMPL {harmonicNumber},{amplitude}");

                // Store for change detection
                _lastAmplitudes[harmonicNumber] = amplitude;

                Log($"Set harmonic {harmonicNumber} amplitude to {amplitude:F4}V");
            }
            catch (Exception ex)
            {
                Log($"Error setting harmonic amplitude: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies a harmonic phase value
        /// </summary>
        public void SetHarmonicPhase(int harmonicNumber, double phase)
        {
            try
            {
                if (harmonicNumber < 2 || harmonicNumber > 8)
                    throw new ArgumentException("Harmonic number must be between 2 and 8");

                // Normalize phase to 0-360 range
                phase = ((phase % 360) + 360) % 360;

                // Set the phase
                _device.SendCommand($":SOUR{_activeChannel}:HARM:PHAS {harmonicNumber},{phase}");

                // Store for change detection
                _lastPhases[harmonicNumber] = phase;

                Log($"Set harmonic {harmonicNumber} phase to {phase:F1}°");
            }
            catch (Exception ex)
            {
                Log($"Error setting harmonic phase: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies all harmonic settings based on the required sequence
        /// </summary>
        public void ApplyHarmonicSettings(bool[] enabledHarmonics, Dictionary<int, double> amplitudes, Dictionary<int, double> phases, bool isPercentageMode)
        {
            try
            {
                // Follow the specified sequence:

                // 1. Start
                Log("Starting harmonic settings application...");

                // 2. Send :SOUR(1/2):HARMONIC:USER X0000000
                _device.SendCommand($":SOUR{_activeChannel}:HARM:USER X0000000");
                Thread.Sleep(50);

                // 3. Get Amplitude
                double fundamentalAmplitude = _device.GetAmplitude(_activeChannel);

                // 4. Get Frequency
                double frequency = _device.GetFrequency(_activeChannel);

                // 5-12. Process each harmonic (2-8)
                for (int i = 2; i <= 8; i++)
                {
                    // 6. Is Harmonic (N) checkbox selected
                    bool isEnabled = enabledHarmonics[i - 2];

                    if (isEnabled)
                    {
                        // 7. If TRUE
                        double amplitude = 0;
                        if (amplitudes.TryGetValue(i, out amplitude))
                        {
                            // 7a. Is % selected?
                            // 7b. If YES, Amplitude (N) = Amplitude (N) / 100 * Amplitude
                            if (isPercentageMode)
                            {
                                amplitude = (amplitude / 100.0) * fundamentalAmplitude;
                            }

                            // 7c. Send :SOUR(1/2):HARM:AMPL (N), Amplitude (N)
                            _device.SendCommand($":SOUR{_activeChannel}:HARM:AMPL {i},{amplitude}");
                            Thread.Sleep(30);
                            _lastAmplitudes[i] = amplitude;
                        }

                        // 7d. Send :SOUR(1/2):HARM:PHAS (N), Angle (N)
                        if (phases.TryGetValue(i, out double phaseValue))
                        {
                            _device.SendCommand($":SOUR{_activeChannel}:HARM:PHAS {i},{phaseValue}");
                            Thread.Sleep(30);
                            _lastPhases[i] = phaseValue;
                        }

                        // 7e. Alter Bit position (N) to 1 in sequence 'x0000000'
                        // This happens in BuildHarmonicPattern

                        // 7f. :SOUR(1/2):HARM:ORDE (N)
                        _device.SendCommand($":SOUR{_activeChannel}:HARM:ORDE {i}");
                        Thread.Sleep(30);
                    }
                    else
                    {
                        // 8. If FALSE, Send SOUR(1/2):HARM: (N), 0
                        _device.SendCommand($":SOUR{_activeChannel}:HARM:AMPL {i},0");
                        Thread.Sleep(30);
                    }

                    // 10-11. Incrementing N and checking counter is handled by the for loop
                }

                // Find the highest enabled harmonic
                int highestHarmonic = 2;
                for (int i = 6; i >= 0; i--)
                {
                    if (enabledHarmonics[i])
                    {
                        highestHarmonic = i + 2;
                        break;
                    }
                }

                // Build harmonic pattern
                string userPattern = BuildHarmonicPattern(enabledHarmonics);

                // Apply the pattern
                _device.SendCommand($":SOUR{_activeChannel}:HARM:TYPE USER");
                Thread.Sleep(50);

                _device.SendCommand($":SOUR{_activeChannel}:HARM:ORDE {highestHarmonic}");
                Thread.Sleep(50);

                _device.SendCommand($":SOUR{_activeChannel}:HARM:USER {userPattern}");
                Thread.Sleep(50);

                // 13. :SOUR(1/2):HARM:STATE ON
                _device.SendCommand($":SOUR{_activeChannel}:HARM:STAT ON");
                Thread.Sleep(100);

                // 14. :SOUR(1/2):APPL:HARM
                double offset = _device.GetOffset(_activeChannel);
                double phase = _device.GetPhase(_activeChannel);
                _device.SendCommand($":SOURce{_activeChannel}:APPL:HARM {frequency},{fundamentalAmplitude},{offset},{phase}");
                Thread.Sleep(100);

                // 15. Store 'X0000000' sequence - already done by updating _lastEnabledHarmonics
                Array.Copy(enabledHarmonics, _lastEnabledHarmonics,
                    Math.Min(enabledHarmonics.Length, _lastEnabledHarmonics.Length));

                Log("All harmonic settings applied successfully");
            }
            catch (Exception ex)
            {
                Log($"Error applying harmonic settings: {ex.Message}");
            }
        }

        // Add this method to HarmonicsManager class - it's a wrapper that exposes the device's GetAmplitude
        public double GetFundamentalAmplitude()
        {
            return _device.GetAmplitude(_activeChannel);
        }


        /// <summary>
        /// Gets the current harmonic settings from the device
        /// </summary>
        // Modify GetCurrentHarmonicSettings to be smarter about percentage vs absolute
        public (bool IsEnabled, Dictionary<int, double> Amplitudes, Dictionary<int, double> Phases, bool[] EnabledHarmonics)
            GetCurrentHarmonicSettings(bool isPercentageMode)
        {
            Dictionary<int, double> amplitudes = new Dictionary<int, double>();
            Dictionary<int, double> phases = new Dictionary<int, double>();
            bool[] enabledHarmonics = new bool[7];
            bool isEnabled = false;

            try
            {
                // Get harmonic state
                isEnabled = _device.GetHarmonicState(_activeChannel);

                if (isEnabled)
                {
                    // Get fundamental amplitude for percentage calculations
                    double fundamentalAmplitude = _device.GetAmplitude(_activeChannel);

                    // Get enabled harmonics from pattern
                    string userPattern = _device.SendQuery($":SOUR{_activeChannel}:HARM:USER?").Trim();
                    if (userPattern.Length >= 8)
                    {
                        for (int i = 0; i < 7 && i + 1 < userPattern.Length; i++)
                        {
                            enabledHarmonics[i] = userPattern[i + 1] == '1';
                        }
                    }

                    // Get amplitudes and phases - always store absolute values internally
                    for (int i = 2; i <= 8; i++)
                    {
                        double amplitude = _device.GetHarmonicAmplitude(_activeChannel, i);
                        double phase = _device.GetHarmonicPhase(_activeChannel, i);

                        // For the return values, convert to percentage if needed
                        if (isPercentageMode && fundamentalAmplitude > 0)
                        {
                            amplitude = (amplitude / fundamentalAmplitude) * 100.0;
                        }

                        amplitudes[i] = amplitude;
                        phases[i] = phase;

                        // Store in the last settings dictionaries (for change detection)
                        _lastAmplitudes[i] = _device.GetHarmonicAmplitude(_activeChannel, i); // Always store absolute
                        _lastPhases[i] = phase;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error getting harmonic settings: {ex.Message}");
            }

            return (isEnabled, amplitudes, phases, enabledHarmonics);
        }
        /// <summary>
        /// Retrieves the harmonic phases from the device or UI
        /// </summary>
        private Dictionary<int, double> GetHarmonicPhases()
        {
            Dictionary<int, double> phases = new Dictionary<int, double>();

            try
            {
                // Assuming the device provides a method to get harmonic phases
                for (int i = 2; i <= 8; i++)
                {
                    double phase = _device.GetHarmonicPhase(_activeChannel, i);
                    phases[i] = phase;
                }
            }
            catch (Exception ex)
            {
                Log($"Error retrieving harmonic phases: {ex.Message}");
            }

            return phases;
        }

        // Update the constructor to initialize these fields
        public HarmonicsManager(RigolDG2072 device, int channel, ToggleButton harmonicsToggle, bool isPercentageMode)
        {
            _device = device;
            _activeChannel = channel;
            _harmonicsToggle = harmonicsToggle;
            _isPercentageMode = isPercentageMode;
        }
    }
}