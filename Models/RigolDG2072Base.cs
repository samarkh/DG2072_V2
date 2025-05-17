// RigolDG2072 class

using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics;
namespace DG2072_USB_Control
{
    public partial class RigolDG2072 : ILogProvider
    {
        private readonly VisaManager visaManager;
       
        private const string DefaultResourceName = "USB0::0x1AB1::0x0644::DG2P224100508::INSTR";

        public event EventHandler<string> LogEvent;

        public void Log(string message)
        {
            // Raise the LogEvent to notify subscribers
            LogEvent?.Invoke(this, message);
        }

        // Add in RigolDG2072 class:
        public double GetSymmetry(int channel)
        {
            try
            {
                string response = SendQuery($"SOURCE{channel}:FUNCTION:RAMP:SYMMETRY?");
                if (double.TryParse(response, out double symmetry))
                {
                    return symmetry;
                }
                return 50.0; // Default value
            }
            catch (Exception ex)
            {
                LogEvent?.Invoke(this, $"Error getting symmetry for channel {channel}: {ex.Message}");
                return 50.0;
            }
        }

        public double GetDutyCycle(int channel)
        {
            try
            {
                string response = SendQuery($"SOURCE{channel}:FUNCTION:SQUARE:DCYCLE?");
                if (double.TryParse(response, out double dutyCycle))
                {
                    return dutyCycle;
                }
                return 50.0; // Default value
            }
            catch (Exception ex)
            {
                LogEvent?.Invoke(this, $"Error getting duty cycle for channel {channel}: {ex.Message}");
                return 50.0;
            }
        }

        public void SetDutyCycle(int channel, double dutyCycle)
        {
            try
            {
                // Ensure duty cycle is within valid range
                dutyCycle = Math.Max(0, Math.Min(100, dutyCycle));
                SendCommand($"SOURCE{channel}:FUNCTION:SQUARE:DCYCLE {dutyCycle}");
                LogEvent?.Invoke(this, $"Set CH{channel} duty cycle to {dutyCycle}%");
            }
            catch (Exception ex)
            {
                LogEvent?.Invoke(this, $"Error setting duty cycle for channel {channel}: {ex.Message}");
            }
        }

        public void SetSymmetry(int channel, double symmetry)
        {
            try
            {
                // Ensure symmetry is within valid range
                symmetry = Math.Max(0, Math.Min(100, symmetry));
                SendCommand($"SOURCE{channel}:FUNCTION:RAMP:SYMMETRY {symmetry}");
                LogEvent?.Invoke(this, $"Set CH{channel} symmetry to {symmetry}%");
            }
            catch (Exception ex)
            {
                LogEvent?.Invoke(this, $"Error setting symmetry for channel {channel}: {ex.Message}");
            }
        }

        public void EnableHarmonics(int channel, bool enabled, Dictionary<int, double> amplitudes = null, Dictionary<int, double> phases = null)
        {
            try
            {
                if (enabled)
                {
                    // Step 1: Get current parameters for the sine wave
                    double frequency = GetFrequency(channel);
                    double amplitude = GetAmplitude(channel);
                    double offset = GetOffset(channel);
                    double phase = GetPhase(channel);

                    // Step 2: Apply sine wave first - this is required for harmonics
                    SendCommand($"SOURCE{channel}:APPLy:SIN {frequency},{amplitude},{offset},{phase}");
                    System.Threading.Thread.Sleep(100);

                    // Step 3: Set harmonic to USER mode (always use USER mode as per UI design)
                    SendCommand($"SOURCE{channel}:HARM:TYPE USER");
                    System.Threading.Thread.Sleep(100);

                    // [steps to setup harmonics]

                    // Last step: Enable harmonic function (do this after setting all parameters)
                    SendCommand($"SOURCE{channel}:HARM:STAT ON");
                    System.Threading.Thread.Sleep(100);
                }
                else
                {
                    // Disable harmonic mode
                    SendCommand($"SOURCE{channel}:HARM:STA OFF");
                    System.Threading.Thread.Sleep(100);
                }
            }
            catch (Exception ex)
            {
                Log($"Error configuring harmonics: {ex.Message}");
                throw;
            }
        }

        public (ArbitraryWaveformCategory Category, string FriendlyName)? LastDetectedArbitraryWaveform { get; set; }

        public RigolDG2072()
        {
            visaManager = new VisaManager();
            visaManager.LogEvent += (s, message) => LogEvent?.Invoke(this, message);
        }

        #region Connection Management

        public bool Connect(string resourceName = DefaultResourceName)
        {
            return visaManager.Connect(resourceName);
        }

        public bool Disconnect()
        {
            return visaManager.Disconnect();
        }

        public bool IsConnected => visaManager.IsConnected;

        public string GetIdentification()
        {
            return visaManager.SendQuery("*IDN?");
        }

        public List<string> FindResources()
        {
            return visaManager.FindResources();
        }

        #endregion


        #region Basic Channel Control

        public void SetOutput(int channel, bool state)
        {
            ValidateChannel(channel);
            visaManager.SendCommand($"OUTPUT{channel} {(state ? "ON" : "OFF")}");
        }

        public string GetOutputState(int channel)
        {
            ValidateChannel(channel);
            return visaManager.SendQuery($"OUTPUT{channel}?");
        }

        /// <summary>
        /// Sets the period for any waveform
        /// </summary>
        public void SetPeriod(int channel, double period)
        {
            ValidateChannel(channel);

            // For pulse waveform, use the specific pulse period method
            string currentWaveform = SendQuery($":SOUR{channel}:FUNC?").Trim().ToUpper();
            if (currentWaveform.Contains("PULS"))
            {
                SetPulsePeriod(channel, period);
            }
            else
            {
                // For other waveforms, use general period command
                SendCommand($"SOURCE{channel}:PERiod {period}");
                Log($"Set CH{channel} period to {period} s using SOURCE:PERiod command directly");
            }
        }

        /// <summary>
        /// Gets the period for any waveform
        /// </summary>
        public double GetPeriod(int channel)
        {
            ValidateChannel(channel);

            // For pulse waveform, use the specific pulse period method
            string currentWaveform = SendQuery($":SOUR{channel}:FUNC?").Trim().ToUpper();
            if (currentWaveform.Contains("PULS"))
            {
                return GetPulsePeriod(channel);
            }
            else
            {
                // For other waveforms, use general period command
                string response = SendQuery($"SOURCE{channel}:PERiod?");
                if (double.TryParse(response, out double period))
                {
                    return period;
                }

                // Calculate from frequency if period query fails
                double frequency = GetFrequency(channel);
                if (frequency > 0)
                {
                    return 1.0 / frequency;
                }

                return 0.001; // 1ms default
            }
        }

        public void SetWaveform(int channel, string waveform)
        {
            ValidateChannel(channel);
            visaManager.SendCommand($"SOURCE{channel}:APPLY:{MapWaveformToScpiCommand(waveform)}");
        }

        public void SetFrequency(int channel, double frequency)
        {
            ValidateChannel(channel);

            // Use the exact FREQuency command from the SCPI documentation
            SendCommand($"SOURCE{channel}:FREQuency {frequency}");
            Log($"Set CH{channel} frequency to {frequency} Hz using SOURCE:FREQuency command directly");
        }

        public double GetFrequency(int channel)
        {
            ValidateChannel(channel);
            // Use the FREQuency query command as specified in the SCPI documentation
            string response = SendQuery($"SOURCE{channel}:FREQuency?");
            if (double.TryParse(response, out double frequency))
            {
                return frequency;
            }
            return 1000.0; // Default 1kHz
        }

        public void SendCommand(string command)
        {
            if (!IsConnected)
                throw new InvalidOperationException("Device not connected");

            visaManager.SendCommand(command);
            Log($"Command sent: {command}");
        }

        public string SendQuery(string query)
        {
            if (!IsConnected)
                throw new InvalidOperationException("Device not connected");

            string response = visaManager.SendQuery(query);
            //Log($"Query: {query}, Response: {response}");
            return response;
        }
 
        public void SetAmplitude(int channel, double amplitude)
        {
            ValidateChannel(channel);
            visaManager.SendCommand($"SOURCE{channel}:VOLTAGE {amplitude}");
        }

        public double GetAmplitude(int channel)
        {
            ValidateChannel(channel);
            string response = visaManager.SendQuery($"SOURCE{channel}:VOLTAGE?");
            if (double.TryParse(response, out double amplitude))
            {
                return amplitude;
            }
            return 0.0;
        }

        public void SetOffset(int channel, double offset)
        {
            ValidateChannel(channel);
            visaManager.SendCommand($"SOURCE{channel}:VOLTAGE:OFFSET {offset}");
        }

        public double GetOffset(int channel)
        {
            ValidateChannel(channel);
            string response = visaManager.SendQuery($"SOURCE{channel}:VOLTAGE:OFFSET?");
            if (double.TryParse(response, out double offset))
            {
                return offset;
            }
            return 0.0;
        }

        public void SetPhase(int channel, double phase)
        {
            ValidateChannel(channel);
            visaManager.SendCommand($"SOURCE{channel}:PHASE {phase}");
        }

        public double GetPhase(int channel)
        {
            ValidateChannel(channel);
            string response = visaManager.SendQuery($"SOURCE{channel}:PHASE?");
            if (double.TryParse(response, out double phase))
            {
                return phase;
            }
            return 0.0;
        }

        public void ApplyWaveform(int channel, string waveform, double frequency, double amplitude, double offset, double phase)
        {
            ValidateChannel(channel);
            visaManager.SendCommand($"SOURCE{channel}:APPLY:{MapWaveformToScpiCommand(waveform)} {frequency},{amplitude},{offset},{phase}");
        }

        public void ApplyDualToneWaveform(int channel, Dictionary<string, object> parameters)
        {
            try
            {
                // Extract parameters with defaults
                double frequency = parameters.TryGetValue("Frequency", out object freqObj) ? Convert.ToDouble(freqObj) : 1000.0;
                double amplitude = parameters.TryGetValue("Amplitude", out object ampObj) ? Convert.ToDouble(ampObj) : 1.0;
                double offset = parameters.TryGetValue("Offset", out object offsetObj) ? Convert.ToDouble(offsetObj) : 0.0;
                double phase = parameters.TryGetValue("Phase", out object phaseObj) ? Convert.ToDouble(phaseObj) : 0.0;

                // For dual tone, we need the secondary frequency
                double frequency2 = parameters.TryGetValue("Frequency2", out object freq2Obj) ? Convert.ToDouble(freq2Obj) : 2000.0;

                // Approach 1: Use direct APPLY command with all parameters
                // This is the most comprehensive approach and should set everything at once
                //SendCommand($"SOURce{channel}:APPLy:DUALTone {frequency},{amplitude},{offset},{phase}");
                //System.Threading.Thread.Sleep(100);

                //// Then explicitly set the second frequency
                //SendCommand($"SOURce{channel}:FUNCtion:DUALTONE:FREQ2 {frequency2}");
                //System.Threading.Thread.Sleep(50);

                // Approach 2: Set each parameter individually to ensure they're applied
                // Some devices require setting parameters after mode selection
                SendCommand($"SOURce{channel}:FUNCtion DUALTONE");
                System.Threading.Thread.Sleep(50);

                SendCommand($"SOURce{channel}:FUNCtion:DUALTONE:FREQ1 {frequency}");
                System.Threading.Thread.Sleep(30);

                SendCommand($"SOURce{channel}:FUNCtion:DUALTONE:FREQ2 {frequency2}");
                System.Threading.Thread.Sleep(30);

                // Try several amplitude command variants (device may require specific syntax for dual tone)
                SendCommand($"SOURce{channel}:VOLTage {amplitude}");
                System.Threading.Thread.Sleep(30);

                SendCommand($"SOURce{channel}:VOLTage:AMPLitude {amplitude}");
                System.Threading.Thread.Sleep(30);

                // Try several offset command variants
                SendCommand($"SOURce{channel}:VOLTage:OFFSet {offset}");
                System.Threading.Thread.Sleep(30);

                // Set phase
                SendCommand($"SOURce{channel}:PHASe {phase}");
                System.Threading.Thread.Sleep(30);

                // Log the operation
                Log($"Applied DUAL TONE to CH{channel} with Freq1={frequency}Hz, Freq2={frequency2}Hz, Amp={amplitude}Vpp, Offset={offset}V, Phase={phase}°");
            }
            catch (Exception ex)
            {
                Log($"Error in ApplyDualToneWaveform: {ex.Message}");
                throw;
            }
        }


        private string MapWaveformToScpiCommand(string waveform)
        {
            switch (waveform.ToUpper())
            {
                case "SINE": return "SIN";
                case "SQUARE": return "SQU";
                case "RAMP": return "RAMP";
                case "PULSE": return "PULS";
                case "NOISE": return "NOIS";
                case "DUAL TONE": return "DUALTONE";
                case "HARMONIC": return "SIN"; // Harmonics are based on sine waves
                case "ARBITRARY WAVEFORM": return "USER";
                default: return waveform.ToUpper();
            }
        }
      
        public void QueryDualToneCapabilities(int channel)
        {
            try
            {
                // Log current function
                string function = SendQuery($"SOURce{channel}:FUNC?");
                Log($"Current function: {function}");

                // Try to query dual tone parameters
                string freq1 = SendQuery($"SOURce{channel}:FUNCtion:DUALTONE:FREQ1?");
                Log($"Dual tone freq1: {freq1}");

                string freq2 = SendQuery($"SOURce{channel}:FUNCtion:DUALTONE:FREQ2?");
                Log($"Dual tone freq2: {freq2}");

                // Query amplitude and offset
                string amplitude = SendQuery($"SOURce{channel}:VOLTage?");
                Log($"Amplitude: {amplitude}");

                string offset = SendQuery($"SOURce{channel}:VOLTage:OFFSet?");
                Log($"Offset: {offset}");
            }
            catch (Exception ex)
            {
                Log($"Error in QueryDualToneCapabilities: {ex.Message}");
            }
        }

        #endregion


        #region Pulse Methods
        public void ApplyPulseWaveform(int channel, Dictionary<string, object> parameters)
        {
            // Extract parameters with defaults
            double frequency = parameters.TryGetValue("Frequency", out object freqObj) ? Convert.ToDouble(freqObj) : 1000.0;
            double amplitude = parameters.TryGetValue("Amplitude", out object ampObj) ? Convert.ToDouble(ampObj) : 1.0;
            double offset = parameters.TryGetValue("Offset", out object offsetObj) ? Convert.ToDouble(offsetObj) : 0.0;
            double phase = parameters.TryGetValue("Phase", out object phaseObj) ? Convert.ToDouble(phaseObj) : 0.0;

            // Apply the pulse waveform
            string command = $"SOURce{channel}:APPLy:PULSe {frequency},{amplitude},{offset},{phase}";
            SendCommand(command);

            // Set additional pulse-specific parameters after waveform is applied
            if (parameters.TryGetValue("Period", out object periodObj))
            {
                double period = Convert.ToDouble(periodObj);
                SetPulsePeriod(channel, period);
            }

            if (parameters.TryGetValue("Width", out object widthObj))
            {
                double width = Convert.ToDouble(widthObj);
                SetPulseWidth(channel, width);
            }

            if (parameters.TryGetValue("RiseTime", out object riseObj))
            {
                double riseTime = Convert.ToDouble(riseObj);
                SetPulseRiseTime(channel, riseTime);
            }

            if (parameters.TryGetValue("FallTime", out object fallObj))
            {
                double fallTime = Convert.ToDouble(fallObj);
                SetPulseFallTime(channel, fallTime);
            }

            // Note: We don't need to handle duty cycle for pulse waveforms
            // as they are fully defined by period and width

            Log($"Applied PULSE to CH{channel} with Freq={frequency}Hz, Amp={amplitude}Vpp, Offset={offset}V, Phase={phase}°");
        }

        public void SetPulseWidth(int channel, double width)
        {
            ValidateChannel(channel);
            SendCommand($"SOURCE{channel}:PULSE:WIDTH {width}");
        }

        public double GetPulseWidth(int channel)
        {
            ValidateChannel(channel);
            string response = SendQuery($"SOURCE{channel}:PULSE:WIDTH?");
            if (double.TryParse(response, out double width))
            {
                return width;
            }
            return 0.0000005; // Default
        }

        public void SetPulseRiseTime(int channel, double riseTime)
        {
            ValidateChannel(channel);
            SendCommand($"SOURCE{channel}:PULSE:TRANSITION:LEADING {riseTime}");
        }

        public double GetPulseRiseTime(int channel)
        {
            ValidateChannel(channel);
            string response = SendQuery($"SOURCE{channel}:PULSE:TRANSITION:LEADING?");
            if (double.TryParse(response, out double riseTime))
            {
                return riseTime;
            }
            return 0.00000002; // Default
        }

        public void SetPulseFallTime(int channel, double fallTime)
        {
            ValidateChannel(channel);
            SendCommand($"SOURCE{channel}:PULSE:TRANSITION:TRAILING {fallTime}");
        }

        public double GetPulseFallTime(int channel)
        {
            ValidateChannel(channel);
            string response = SendQuery($"SOURCE{channel}:PULSE:TRANSITION:TRAILING?");
            if (double.TryParse(response, out double fallTime))
            {
                return fallTime;
            }
            return 0.00000002; // Default
        }

        public void SetPulseTransitionBoth(int channel, double transitionTime)
        {
            ValidateChannel(channel);
            SendCommand($"SOURCE{channel}:PULSE:TRANSITION {transitionTime}");
        }
        #endregion


        #region Additional Pulse Methods
        // Updated SetPulsePeriod method to use the correct SCPI command
        public void SetPulsePeriod(int channel, double period)
        {
            ValidateChannel(channel);

            // Use the exact PERiod command from the SCPI documentation
            // This sends directly to the device without frequency conversion
            SendCommand($"SOURCE{channel}:PERiod {period}");
            Log($"Set CH{channel} pulse period to {period} s using SOURCE:PERiod command directly");
        }



        // Updated GetPulsePeriod method to use the correct SCPI command
        // Also update the GetPulsePeriod method to ensure it queries the period directly
        public double GetPulsePeriod(int channel)
        {
            ValidateChannel(channel);

            // Query the period directly instead of calculating from frequency
            string response = SendQuery($"SOURCE{channel}:PERiod?");
            if (double.TryParse(response, out double period))
            {
                return period;
            }

            // Default fallback
            return 0.001; // 1ms default
        }

        // Calculate duty cycle from pulse width and period
        public double CalculatePulseDutyCycle(int channel)
        {
            double width = GetPulseWidth(channel);
            double period = GetPulsePeriod(channel);

            if (period > 0)
            {
                return (width / period) * 100.0;
            }
            return 50.0; // Default
        }
        #endregion


        #region Harmonics

        // Harmonic state (on/off)
        public void SetHarmonicState(int channel, bool state)
        {
            ValidateChannel(channel);
            SendCommand($"SOURCE{channel}:HARM:STA {(state ? "ON" : "OFF")}");
        }

        // Improved safe method for harmonic state checking
        public bool GetHarmonicState(int channel)
        {
            try
            {
                // Due to the consistent query errors, we'll try a different approach
                // Instead of querying directly, we'll try to infer the state based on
                // whether the device is in SIN mode and has a USER pattern set

                // First check if we're in SIN mode - required for harmonics
                string waveform = SendQuery($"SOURCE{channel}:FUNC?").Trim().ToUpper();
                if (waveform != "SIN")
                    return false;

                // Try getting the harmonic type
                string type = "NONE";
                try
                {
                    type = SendQuery($"SOURCE{channel}:HARM:TYPE?").Trim().ToUpper();
                }
                catch
                {
                    // If this fails, harmonics are probably off
                    return false;
                }

                // If we got this far, try the most direct approach - the problematic query
                try
                {
                    string state = SendQuery($"SOURCE{channel}:HARM?").Trim().ToUpper();
                    return state == "ON";
                }
                catch
                {
                    // If direct query fails, make a best guess based on the other information
                    return (waveform == "SIN" && type != "NONE");
                }
            }
            catch (Exception ex)
            {
                Log($"Error determining harmonic state: {ex.Message}. Assuming OFF.");
                return false;
            }
        }

        // Harmonic type (EVEN, ODD, ALL, USER)
        public void SetHarmonicType(int channel, string type)
        {
            ValidateChannel(channel);
            if (!IsValidHarmonicType(type))
            {
                throw new ArgumentException($"Invalid harmonic type: {type}. Must be EVEN, ODD, ALL, or USER.");
            }
            SendCommand($"SOURCE{channel}:HARM:TYPE {type.ToUpper()}");
        }

        public string GetHarmonicType(int channel)
        {
            ValidateChannel(channel);
            return SendQuery($"SOURCE{channel}:HARM:TYPE?");
        }

        // Harmonic order (highest harmonic)
        public void SetHarmonicOrder(int channel, int order)
        {
            ValidateChannel(channel);
            if (order < 2 || order > 8)
            {
                throw new ArgumentException("Harmonic order must be between 2 and 8.");
            }
            SendCommand($"SOURCE{channel}:HARM:ORDER {order}");
        }

        public int GetHarmonicOrder(int channel)
        {
            ValidateChannel(channel);
            string response = SendQuery($"SOURCE{channel}:HARM:ORDER?");
            if (double.TryParse(response, out double order))
            {
                return (int)order;
            }
            return 0;
        }

        // Harmonic amplitude
        public void SetHarmonicAmplitude(int channel, int harmonicNumber, double amplitude)
        {
            ValidateChannel(channel);
            if (harmonicNumber < 2 || harmonicNumber > 8)
            {
                throw new ArgumentException("Harmonic number must be between 2 and 8.");
            }
            SendCommand($"SOURCE{channel}:HARM:AMPL {harmonicNumber},{amplitude}");
        }

        public double GetHarmonicAmplitude(int channel, int harmonicNumber)
        {
            ValidateChannel(channel);
            if (harmonicNumber < 2 || harmonicNumber > 8)
            {
                throw new ArgumentException("Harmonic number must be between 2 and 8.");
            }
            string response = SendQuery($"SOURCE{channel}:HARM:AMPL? {harmonicNumber}");
            if (double.TryParse(response, out double amplitude))
            {
                return amplitude;
            }
            return 0.0;
        }

        // Harmonic phase
        public void SetHarmonicPhase(int channel, int harmonicNumber, double phase)
        {
            ValidateChannel(channel);
            if (harmonicNumber < 2 || harmonicNumber > 8)
            {
                throw new ArgumentException("Harmonic number must be between 2 and 8.");
            }
            SendCommand($"SOURCE{channel}:HARM:PHASE {harmonicNumber},{phase}");
        }

        public double GetHarmonicPhase(int channel, int harmonicNumber)
        {
            ValidateChannel(channel);
            if (harmonicNumber < 2 || harmonicNumber > 8)
            {
                throw new ArgumentException("Harmonic number must be between 2 and 8.");
            }
            string response = SendQuery($"SOURCE{channel}:HARM:PHASE? {harmonicNumber}");
            if (double.TryParse(response, out double phase))
            {
                return phase;
            }
            return 0.0;
        }

        // User-defined harmonics (which ones are enabled)
        public void SetUserDefinedHarmonics(int channel, bool[] enabledHarmonics)
        {
            ValidateChannel(channel);
            if (enabledHarmonics.Length != 7)
            {
                throw new ArgumentException("Enabled harmonics array must have exactly 7 elements (for harmonics 2-8).");
            }

            char[] userHarmonics = new char[8];
            userHarmonics[0] = 'X'; // Fundamental waveform is always enabled

            for (int i = 0; i < 7; i++)
            {
                userHarmonics[i + 1] = enabledHarmonics[i] ? '1' : '0';
            }

            string userHarmonicString = new string(userHarmonics);
            SendCommand($"SOURCE{channel}:HARM:USER {userHarmonicString}");
        }

        // Helper method to validate harmonic types
        private bool IsValidHarmonicType(string type)
        {
            string upperType = type.ToUpper();
            return upperType == "EVEN" || upperType == "ODD" || upperType == "ALL" || upperType == "USER";
        }


        // Apply specific waveform with parameters
        public void ApplyWaveformWithSpecificParams(int channel, string waveform, Dictionary<string, object> parameters)
        {
            ValidateChannel(channel);

            // Extract common parameters
            double frequency = parameters.TryGetValue("Frequency", out object freqObj) ? Convert.ToDouble(freqObj) : 1000.0;
            double amplitude = parameters.TryGetValue("Amplitude", out object ampObj) ? Convert.ToDouble(ampObj) : 1.0;
            double offset = parameters.TryGetValue("Offset", out object offsetObj) ? Convert.ToDouble(offsetObj) : 0.0;
            double phase = parameters.TryGetValue("Phase", out object phaseObj) ? Convert.ToDouble(phaseObj) : 0.0;

            if (waveform.ToUpper() == "HARMonic")
            {
                // For harmonic waveform, first set to sine, then enable harmonics
                ApplyWaveform(channel, "SINE", frequency, amplitude, offset, phase);
                SetHarmonicState(channel, true);
            }
            else
            {
                // For all other waveforms, use standard apply method
                ApplyWaveform(channel, waveform, frequency, amplitude, offset, phase);
            }
        }


        protected void ValidateChannel(int channel)
        {
            if (channel < 1 || channel > 2)
            {
                throw new ArgumentException("Channel must be 1 or 2.");
            }
        }

        // Add the ChannelHarmonicController class at the end of the file, after the RigolDG2072 class
        public class ChannelHarmonicController
        {
            private RigolDG2072 rigolDG2072;
            private bool isPercentageMode = true;
            //private double _fundamentalFrequencyInHz = 1000.0;
            private double _fundamentalAmplitude = 1.0;
            private int channel;

            public ChannelHarmonicController(RigolDG2072 device, int channelNumber)
            {
                rigolDG2072 = device;
                channel = channelNumber;
            }

            // Toggle harmonic functionality on/off
            public void ToggleHarmonics(bool isEnabled)
            {
                if (rigolDG2072 == null) return;

                rigolDG2072.SetHarmonicState(channel, isEnabled);

                // Get current parameters
                double frequency = rigolDG2072.GetFrequency(channel);
                double amplitude = rigolDG2072.GetAmplitude(channel);
                double offset = rigolDG2072.GetOffset(channel);
                double phase = rigolDG2072.GetPhase(channel);

                Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "Frequency", frequency },
                { "Amplitude", amplitude },
                { "Offset", offset },
                { "Phase", phase }
            };

                if (isEnabled)
                {
                    // Switch to harmonic waveform
                    rigolDG2072.ApplyWaveformWithSpecificParams(channel, "Harmonic", parameters);
                }
                else
                {
                    // Switch back to sine waveform when harmonics are disabled
                    rigolDG2072.ApplyWaveformWithSpecificParams(channel, "Sine", parameters);
                }
            }

            // Switch between percentage and absolute amplitude modes
            public void SetAmplitudeMode(bool isPercentageMode)
            {
                this.isPercentageMode = isPercentageMode;
                UpdateHarmonicAmplitudeDisplays();
            }

            // Update harmonic amplitudes based on current mode (percentage or absolute)
            public Dictionary<int, double> UpdateHarmonicAmplitudeDisplays()
            {
                if (rigolDG2072 == null) return null;

                Dictionary<int, double> harmonicValues = new Dictionary<int, double>();

                try
                {
                    // Get the fundamental amplitude for percentage calculations
                    double fundamentalAmplitude = rigolDG2072.GetAmplitude(channel);
                    _fundamentalAmplitude = fundamentalAmplitude;

                    // Add fundamental value (always 100% or actual amplitude)
                    harmonicValues[1] = isPercentageMode ? 100.0 : fundamentalAmplitude;

                    // Get harmonic values (2-8)
                    for (int i = 2; i <= 8; i++)
                    {
                        double harmonicAmplitude = rigolDG2072.GetHarmonicAmplitude(channel, i);

                        if (isPercentageMode && fundamentalAmplitude > 0)
                        {
                            // Convert from absolute to percentage
                            double percentage = (harmonicAmplitude / fundamentalAmplitude) * 100;
                            harmonicValues[i] = percentage;
                        }
                        else
                        {
                            // Absolute amplitude
                            harmonicValues[i] = harmonicAmplitude;
                        }
                    }

                    return harmonicValues;
                }
                catch (Exception)
                {
                    return null;
                }
            }

            // Set which harmonics are enabled (2-8)
            public void SetEnabledHarmonics(bool[] enabledHarmonics)
            {
                if (rigolDG2072 == null || enabledHarmonics.Length != 7) return;

                try
                {
                    // Find the highest enabled harmonic
                    int highestHarmonic = 2; // Default to 2 if none are enabled
                    for (int i = 6; i >= 0; i--)
                    {
                        if (enabledHarmonics[i])
                        {
                            highestHarmonic = i + 2; // +2 because index 0 corresponds to harmonic 2
                            break;
                        }
                    }

                    // Set the harmonic order to the highest enabled harmonic
                    rigolDG2072.SetHarmonicOrder(channel, highestHarmonic);

                    // Set to USER mode and update enabled harmonics
                    rigolDG2072.SetHarmonicType(channel, "USER");
                    rigolDG2072.SetUserDefinedHarmonics(channel, enabledHarmonics);
                }
                catch (Exception)
                {
                    // Handle exceptions appropriately
                }
            }

            // Set the amplitude for a specific harmonic
            public void SetHarmonicAmplitude(int harmonicNumber, double value)
            {
                if (rigolDG2072 == null) return;

                try
                {
                    if (harmonicNumber == 1)
                    {
                        // For fundamental
                        if (isPercentageMode)
                        {
                            // In percentage mode, fundamental is always 100%
                            // Just update the fundamental amplitude on the device
                            double amplitude = rigolDG2072.GetAmplitude(channel);
                            rigolDG2072.SetAmplitude(channel, amplitude);
                        }
                        else
                        {
                            // Set the fundamental amplitude directly
                            rigolDG2072.SetAmplitude(channel, value);
                        }

                        // Update all harmonic display values since they depend on the fundamental
                        UpdateHarmonicAmplitudeDisplays();
                    }
                    else if (harmonicNumber >= 2 && harmonicNumber <= 8)
                    {
                        // For harmonics
                        if (isPercentageMode)
                        {
                            // Convert percentage to absolute value
                            double fundamentalAmplitude = rigolDG2072.GetAmplitude(channel);
                            value = (value / 100) * fundamentalAmplitude;
                        }

                        // Set the harmonic amplitude
                        rigolDG2072.SetHarmonicAmplitude(channel, harmonicNumber, value);
                    }
                }
                catch (Exception)
                {
                    // Handle exceptions appropriately
                }
            }

            // Set the phase for a specific harmonic
            public void SetHarmonicPhase(int harmonicNumber, double phase)
            {
                if (rigolDG2072 == null) return;

                try
                {
                    if (harmonicNumber == 1)
                    {
                        // Set the fundamental phase
                        rigolDG2072.SetPhase(channel, phase);
                    }
                    else if (harmonicNumber >= 2 && harmonicNumber <= 8)
                    {
                        // Set the harmonic phase
                        rigolDG2072.SetHarmonicPhase(channel, harmonicNumber, phase);
                    }
                }
                catch (Exception)
                {
                    // Handle exceptions appropriately
                }
            }

            // Apply all harmonic settings at once
            public void ApplyHarmonicSettings(bool enableHarmonics, bool[] enabledHarmonics, Dictionary<int, double> amplitudes, Dictionary<int, double> phases)
            {
                if (rigolDG2072 == null) return;

                try
                {
                    // Enable/disable harmonics
                    rigolDG2072.SetHarmonicState(channel, enableHarmonics);

                    // Set user-defined harmonics
                    if (enabledHarmonics != null && enabledHarmonics.Length == 7)
                    {
                        SetEnabledHarmonics(enabledHarmonics);
                    }

                    // Find the highest enabled harmonic
                    int highestHarmonic = 2;
                    if (enabledHarmonics != null)
                    {
                        for (int i = 6; i >= 0; i--)
                        {
                            if (enabledHarmonics[i])
                            {
                                highestHarmonic = i + 2;
                                break;
                            }
                        }
                    }

                    rigolDG2072.SetHarmonicOrder(channel, highestHarmonic);

                    // Update all amplitude and phase values
                    for (int i = 1; i <= 8; i++)
                    {
                        if (i == 1)
                        {
                            // Handle fundamental separately
                            if (amplitudes != null && amplitudes.ContainsKey(i) && !isPercentageMode)
                            {
                                rigolDG2072.SetAmplitude(channel, amplitudes[i]);
                            }

                            if (phases != null && phases.ContainsKey(i))
                            {
                                rigolDG2072.SetPhase(channel, phases[i]);
                            }
                        }
                        else
                        {
                            // Handle harmonics
                            if (amplitudes != null && amplitudes.ContainsKey(i))
                            {
                                double amplitudeValue = amplitudes[i];
                                if (isPercentageMode)
                                {
                                    // Convert percentage to absolute value
                                    double fundamentalAmplitude = rigolDG2072.GetAmplitude(channel);
                                    amplitudeValue = (amplitudeValue / 100) * fundamentalAmplitude;
                                }

                                rigolDG2072.SetHarmonicAmplitude(channel, i, amplitudeValue);
                            }

                            if (phases != null && phases.ContainsKey(i))
                            {
                                rigolDG2072.SetHarmonicPhase(channel, i, phases[i]);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // Handle exceptions appropriately
                }
            }
        }
        #endregion


        #region DualTone Methods

        // Add these methods to the RigolDG2072 class to support center/offset frequency control

        // Set center frequency for dual tone
        public void SetDualToneCenterFrequency(int channel, double centerFrequency)
        {
            try
            {
                ValidateChannel(channel);
                SendCommand($"SOURce{channel}:FUNCtion:DUALTone:CENTERFreq {centerFrequency}");
                Log($"Set CH{channel} dual tone center frequency to {centerFrequency} Hz");
            }
            catch (Exception ex)
            {
                Log($"Error setting dual tone center frequency: {ex.Message}");
                throw;
            }
        }

        // Get center frequency for dual tone
        public double GetDualToneCenterFrequency(int channel)
        {
            try
            {
                ValidateChannel(channel);
                string response = SendQuery($"SOURce{channel}:FUNCtion:DUALTone:CENTERFreq?");
                if (double.TryParse(response, out double centerFrequency))
                {
                    return centerFrequency;
                }
                return 1000.0; // Default if parsing fails
            }
            catch (Exception ex)
            {
                Log($"Error getting dual tone center frequency: {ex.Message}");
                return 1000.0; // Default on error
            }
        }

        // Set offset frequency for dual tone
        public void SetDualToneOffsetFrequency(int channel, double offsetFrequency)
        {
            try
            {
                ValidateChannel(channel);
                SendCommand($"SOURce{channel}:FUNCtion:DUALTone:OFFSETFreq {offsetFrequency}");
                Log($"Set CH{channel} dual tone offset frequency to {offsetFrequency} Hz");
            }
            catch (Exception ex)
            {
                Log($"Error setting dual tone offset frequency: {ex.Message}");
                throw;
            }
        }

        // Get offset frequency for dual tone
        public double GetDualToneOffsetFrequency(int channel)
        {
            try
            {
                ValidateChannel(channel);
                string response = SendQuery($"SOURce{channel}:FUNCtion:DUALTone:OFFSETFreq?");
                if (double.TryParse(response, out double offsetFrequency))
                {
                    return offsetFrequency;
                }
                return 1000.0; // Default if parsing fails
            }
            catch (Exception ex)
            {
                Log($"Error getting dual tone offset frequency: {ex.Message}");
                return 1000.0; // Default on error
            }
        }

        // Enhanced ApplyDualToneWaveform method with center/offset option
        public void ApplyDualToneWaveformWithCenterOffset(int channel, Dictionary<string, object> parameters)
        {
            try
            {
                // Extract center/offset parameters
                double centerFrequency = parameters.TryGetValue("CenterFrequency", out object centerObj) ? Convert.ToDouble(centerObj) : 1500.0;
                double offsetFrequency = parameters.TryGetValue("OffsetFrequency", out object offsetObj) ? Convert.ToDouble(offsetObj) : 1000.0;
                double amplitude = parameters.TryGetValue("Amplitude", out object ampObj) ? Convert.ToDouble(ampObj) : 1.0;
                double voltageOffset = parameters.TryGetValue("Offset", out object vOffsetObj) ? Convert.ToDouble(vOffsetObj) : 0.0;
                double phase = parameters.TryGetValue("Phase", out object phaseObj) ? Convert.ToDouble(phaseObj) : 0.0;

                // Calculate the individual frequencies from center and offset
                double freq1 = centerFrequency - (offsetFrequency / 2.0);
                double freq2 = centerFrequency + (offsetFrequency / 2.0);

                // Step 1: Set the function to DUALTONE first
                SendCommand($"SOURce{channel}:FUNCtion DUALTONE");
                System.Threading.Thread.Sleep(100);

                // Step 2: Set center and offset frequencies
                SendCommand($"SOURce{channel}:FUNCtion:DUALTone:CENTERFreq {centerFrequency}");
                System.Threading.Thread.Sleep(50);

                SendCommand($"SOURce{channel}:FUNCtion:DUALTone:OFFSETFreq {offsetFrequency}");
                System.Threading.Thread.Sleep(50);

                // Step 3: Set amplitude and other parameters
                SendCommand($"SOURce{channel}:VOLTage {amplitude}");
                System.Threading.Thread.Sleep(50);

                SendCommand($"SOURce{channel}:VOLTage:OFFSet {voltageOffset}");
                System.Threading.Thread.Sleep(50);

                SendCommand($"SOURce{channel}:PHASe {phase}");
                System.Threading.Thread.Sleep(50);

                // Log the operation
                Log($"Applied DUAL TONE to CH{channel} via center/offset:");
                Log($"  - Center Frequency = {centerFrequency} Hz");
                Log($"  - Offset Frequency = {offsetFrequency} Hz");
                Log($"  - Calculated F1 = {freq1} Hz");
                Log($"  - Calculated F2 = {freq2} Hz");
                Log($"  - Amplitude = {amplitude} Vpp");
                Log($"  - Offset = {voltageOffset} V");
                Log($"  - Phase = {phase}°");
            }
            catch (Exception ex)
            {
                Log($"Error in ApplyDualToneWaveformWithCenterOffset: {ex.Message}");
                throw;
            }
        }

        // Enhanced method to get all dual tone parameters
        /// <summary>
        /// Calculates the center frequency from two individual frequencies
        /// </summary>
        private double CalculateCenterFrequency(double f1, double f2)
        {
            return (f1 + f2) / 2.0;
        }

        /// <summary>
        /// Calculates the offset frequency from two individual frequencies
        /// </summary>
        private double CalculateOffsetFrequency(double f1, double f2)
        {
            return f2 - f1;
        }

        /// <summary>
        /// Enhanced method to get all dual tone parameters
        /// </summary>
        public Dictionary<string, double> GetAllDualToneParameters(int channel)
        {
            try
            {
                ValidateChannel(channel);
                Dictionary<string, double> parameters = new Dictionary<string, double>();

                // Get the individual frequencies
                string freq1Response = SendQuery($"SOURce{channel}:FUNCtion:DUALTone:FREQ1?");
                if (double.TryParse(freq1Response, out double freq1))
                {
                    parameters["Frequency1"] = freq1;
                }

                string freq2Response = SendQuery($"SOURce{channel}:FUNCtion:DUALTone:FREQ2?");
                if (double.TryParse(freq2Response, out double freq2))
                {
                    parameters["Frequency2"] = freq2;
                }

                // Try to get center frequency
                try
                {
                    string centerResponse = SendQuery($"SOURce{channel}:FUNCtion:DUALTone:CENTERFreq?");
                    if (double.TryParse(centerResponse, out double center))
                    {
                        parameters["CenterFrequency"] = center;
                    }
                }
                catch
                {
                    // If not supported, calculate from individual frequencies
                    if (parameters.ContainsKey("Frequency1") && parameters.ContainsKey("Frequency2"))
                    {
                        parameters["CenterFrequency"] = CalculateCenterFrequency(
                            parameters["Frequency1"], parameters["Frequency2"]);
                    }
                }

                try
                {
                    string offsetFreqResponse = SendQuery($"SOURce{channel}:FUNCtion:DUALTone:OFFSETFreq?");
                    if (double.TryParse(offsetFreqResponse, out double offsetFreq))
                    {
                        parameters["OffsetFrequency"] = offsetFreq;
                    }
                }
                catch
                {
                    // If not supported, calculate from individual frequencies
                    if (parameters.ContainsKey("Frequency1") && parameters.ContainsKey("Frequency2"))
                    {
                        parameters["OffsetFrequency"] = CalculateOffsetFrequency(
                            parameters["Frequency1"], parameters["Frequency2"]);
                    }
                }

                // Get other parameters
                string ampResponse = SendQuery($"SOURce{channel}:VOLTage?");
                if (double.TryParse(ampResponse, out double amplitude))
                {
                    parameters["Amplitude"] = amplitude;
                }

                string voltageOffsetResponse = SendQuery($"SOURce{channel}:VOLTage:OFFSet?");
                if (double.TryParse(voltageOffsetResponse, out double voltageOffset))
                {
                    parameters["Offset"] = voltageOffset;
                }

                string phaseResponse = SendQuery($"SOURce{channel}:PHASe?");
                if (double.TryParse(phaseResponse, out double phase))
                {
                    parameters["Phase"] = phase;
                }

                return parameters;
            }
            catch (Exception ex)
            {
                Log($"Error getting all dual tone parameters: {ex.Message}");
                return new Dictionary<string, double>();
            }
        }

        // Enhanced diagnostics method for dual tone mode
        public void DiagnosticDualToneMode(int channel)
        {
            try
            {
                ValidateChannel(channel);

                // Check current function
                string function = SendQuery($"SOURce{channel}:FUNC?");
                Log($"Current function: {function}");

                // Try all dual tone parameters
                try
                {
                    // Try individual frequencies
                    string freq1 = SendQuery($"SOURce{channel}:FUNCtion:DUALTone:FREQ1?");
                    Log($"Dual tone freq1: {freq1}");

                    string freq2 = SendQuery($"SOURce{channel}:FUNCtion:DUALTone:FREQ2?");
                    Log($"Dual tone freq2: {freq2}");

                    // Try center frequency
                    try
                    {
                        string centerFreq = SendQuery($"SOURce{channel}:FUNCtion:DUALTone:CENTERFreq?");
                        Log($"Dual tone center frequency: {centerFreq}");
                    }
                    catch (Exception ex)
                    {
                        Log($"Error querying center frequency: {ex.Message}");
                    }

                    // Try offset frequency
                    try
                    {
                        string offsetFreq = SendQuery($"SOURce{channel}:FUNCtion:DUALTone:OFFSETFreq?");
                        Log($"Dual tone offset frequency: {offsetFreq}");
                    }
                    catch (Exception ex)
                    {
                        Log($"Error querying offset frequency: {ex.Message}");
                    }

                    // Other parameters
                    string amp = SendQuery($"SOURce{channel}:VOLTage?");
                    Log($"Amplitude: {amp}");

                    string offset = SendQuery($"SOURce{channel}:VOLTage:OFFSet?");
                    Log($"Offset: {offset}");

                    string phase = SendQuery($"SOURce{channel}:PHASe?");
                    Log($"Phase: {phase}");
                }
                catch (Exception ex)
                {
                    Log($"Error during dual tone parameter query: {ex.Message}");
                }

                // Run a test cycle for dual tone
                Log("Starting dual tone diagnostic test cycle");

                // First try with direct frequency mode
                try
                {
                    // Set function
                    SendCommand($"SOURce{channel}:FUNCtion DUALTONE");
                    System.Threading.Thread.Sleep(100);

                    // Set frequencies
                    SendCommand($"SOURce{channel}:FUNCtion:DUALTone:FREQ1 1000");
                    System.Threading.Thread.Sleep(50);

                    SendCommand($"SOURce{channel}:FUNCtion:DUALTone:FREQ2 2000");
                    System.Threading.Thread.Sleep(50);

                    // Check what was set
                    string resultFunction = SendQuery($"SOURce{channel}:FUNC?");
                    Log($"Function after direct setting: {resultFunction}");

                    string resultFreq1 = SendQuery($"SOURce{channel}:FUNCtion:DUALTone:FREQ1?");
                    Log($"Freq1 after direct setting: {resultFreq1}");

                    string resultFreq2 = SendQuery($"SOURce{channel}:FUNCtion:DUALTone:FREQ2?");
                    Log($"Freq2 after direct setting: {resultFreq2}");
                }
                catch (Exception ex)
                {
                    Log($"Error in direct frequency test: {ex.Message}");
                }

                // Then try with center/offset mode
                try
                {
                    // Set function
                    SendCommand($"SOURce{channel}:FUNCtion DUALTONE");
                    System.Threading.Thread.Sleep(100);

                    // Set center/offset
                    SendCommand($"SOURce{channel}:FUNCtion:DUALTone:CENTERFreq 1500");
                    System.Threading.Thread.Sleep(50);

                    SendCommand($"SOURce{channel}:FUNCtion:DUALTone:OFFSETFreq 1000");
                    System.Threading.Thread.Sleep(50);

                    // Check what was set
                    string resultFunction = SendQuery($"SOURce{channel}:FUNC?");
                    Log($"Function after center/offset setting: {resultFunction}");

                    try
                    {
                        string resultCenter = SendQuery($"SOURce{channel}:FUNCtion:DUALTone:CENTERFreq?");
                        Log($"Center freq after setting: {resultCenter}");
                    }
                    catch { Log("Center frequency query failed"); }

                    try
                    {
                        string resultOffset = SendQuery($"SOURce{channel}:FUNCtion:DUALTone:OFFSETFreq?");
                        Log($"Offset freq after setting: {resultOffset}");
                    }
                    catch { Log("Offset frequency query failed"); }

                    // Check individual frequencies to verify calculations
                    string resultFreq1 = SendQuery($"SOURce{channel}:FUNCtion:DUALTone:FREQ1?");
                    Log($"Freq1 after center/offset setting: {resultFreq1}");

                    string resultFreq2 = SendQuery($"SOURce{channel}:FUNCtion:DUALTone:FREQ2?");
                    Log($"Freq2 after center/offset setting: {resultFreq2}");
                }
                catch (Exception ex)
                {
                    Log($"Error in center/offset test: {ex.Message}");
                }

                Log("Dual tone diagnostic test completed");
            }
            catch (Exception ex)
            {
                Log($"Error in diagnostic dual tone mode: {ex.Message}");
            }
        }

        #endregion


        #region Arbitrary Waveform Methods

        public enum ArbitraryWaveformCategory
        {
            Engineering,
            Medical,
            AutoElec,
            Maths
        }

        // Dictionary containing all arbitrary waveforms by category
        private readonly Dictionary<ArbitraryWaveformCategory, Dictionary<string, string>> _arbitraryWaveforms = new Dictionary<ArbitraryWaveformCategory, Dictionary<string, string>>
{
    {
        ArbitraryWaveformCategory.Engineering, new Dictionary<string, string>
        {
            { "AttALT", "ATTALT" },
            { "BandLim", "BANDLIMITED" },
            { "Bworth", "BUTTERWORTH" },
            { "Chshev1", "CHEBYSHEV1" },
            { "Chshev2", "CHEBYSHEV2" },
            { "SineVer", "SINEVER" },
            { "SineTra", "SINETRA" },
            { "Combin", "COMBIN" },
            { "CPulse", "CPULSE" },
            { "Pahcur", "PAHCUR" },
            { "CWPulse", "CWPULSE" },
            { "Dischar", "NIMHDISCHARGE" },
            { "AmpALT", "AMPALT" },
            { "Gamma", "GAMMA" },
            { "GateVibr", "GATEVIBR" },
            { "GausPul", "GAUSSPULSE" },
            { "SwigOsc", "SWINGOSC" },
            { "LFMPulse", "LFMPULSE" },
            { "Log", "LOG" },
            { "Lorentz", "LORENTZ" },
            { "MCNoise", "MCNOISE" },
            { "NPulse", "NPULSE" },
            { "NegRamp", "NEGRAMP" },
            { "PPulse", "PPULSE" },
            { "Ripple", "RIPPLE" },
            { "Quake", "QUAKE" },
            { "Radar", "RADAR" },
            { "RouHalf", "ROUNDHALF" },
            { "RousPM", "ROUNDPM" },
            { "SCR", "SCR" },
            { "PFM", "PFM" },
            { "Sinc", "SINC" },
            { "StairDn", "STAIRDN" },
            { "StairUD", "STAIRUD" },
            { "StairUp", "STAIRUP" },
            { "StepResp", "STEPRESP" },
            { "Surge", "SURGE" },
            { "DampOsc", "DAMPEDOSC" },
            { "BlaWave", "BLAWAVE" },
            { "Trapezia", "TRAPEZIA" },
            { "TV", "TV" },
            { "Voice", "VOICE" }
        }
    },
    {
        ArbitraryWaveformCategory.Medical, new Dictionary<string, string>
        {
            { "Cardiac", "CARDIAC" },
            { "ECG1", "ECG1" },
            { "ECG2", "ECG2" },
            { "ECG3", "ECG3" },
            { "ECG4", "ECG4" },
            { "ECG5", "ECG5" },
            { "ECG6", "ECG6" },
            { "ECG7", "ECG7" },
            { "ECG8", "ECG8" },
            { "ECG9", "ECG9" },
            { "ECG10", "ECG10" },
            { "ECG11", "ECG11" },
            { "ECG12", "ECG12" },
            { "ECG13", "ECG13" },
            { "ECG14", "ECG14" },
            { "ECG15", "ECG15" },
            { "EEG", "EEG" },
            { "EMG", "EMG" },
            { "EOG", "EOG" },
            { "LFPulse", "LFPULSE" },
            { "Tens1", "TENS1" },
            { "Tens2", "TENS2" },
            { "Tens3", "TENS3" },
            { "Pulgram", "PULSILOGRAM" },
            { "ResSpd", "RESSPEED" }
        }
    },
    {
        ArbitraryWaveformCategory.AutoElec, new Dictionary<string, string>
        {
            { "TP5A", "ISO76372TP5A" },
            { "TP5B", "ISO76372TP5B" },
            { "TP1", "ISO76372TP1" },
            { "TP2B", "ISO76372TP2B" },
            { "Ignition", "IGNITION" },
            { "TP2A", "ISO76372TP2A" },
            { "VR", "ISO167502VR" },
            { "SP", "ISO167502SP" },
            { "TP4", "ISO76372TP4" },
            { "TP3B", "ISO76372TP3B" },
            { "TP3A", "ISO76372TP3A" }
        }
    },
    {
        ArbitraryWaveformCategory.Maths, new Dictionary<string, string>
        {
            { "AbsSinH", "ABSSINEHALF" },
            { "AbsSine", "ABSSINE" },
            { "Laguerre", "LAGUERRE" },
            { "Airy", "AIRY" },
            { "ACos", "ACOS" },
            { "ACosH", "ACOSH" },
            { "ASecH", "ASECH" },
            { "ASinH", "ASINH" },
            { "ATanH", "ATANH" },
            { "ASin", "ASIN" },
            { "ATan", "ATAN" },
            { "Bartlett", "BARLETT" },
            { "Besseli", "BESSELJ" },
            { "Bessely", "BESSELY" },
            { "Blkman", "BLACKMAN" },
            { "BlkmanH", "BLACKMANH" },
            { "BohWin", "BOHMANWIN" },
            { "Cauchy", "CAUCHY" },
            { "ChebWin", "CHEBWIN" },
            { "Erfc", "ERFC" },
            { "ACscCon", "ACSCCON" },
            { "ACotCon", "ACOTCON" },
            { "ACscHCon", "ACSCHCON" },
            { "ACotHCon", "ACOTHCON" },
            { "ASecCon", "ASECCON" },
            { "CscCon", "CSCCON" },
            { "CscHCon", "CSCHCON" },
            { "CotHCon", "COTHCON" },
            { "RecipCon", "RECIPCON" },
            { "SecCon", "SECCON" },
            { "Cot", "COT" },
            { "Cubic", "CUBIC" },
            { "Dirichlet", "DIRICHLET" },
            { "Erf", "ERF" },
            { "ExpFall", "EXPFALL" },
            { "ExpRise", "EXPRISE" },
            { "FlatWin", "FLATTOPWIN" },
            { "Gauss", "GAUSS" },
            { "Ham", "HAMMING" },
            { "Hanning", "HANNING" },
            { "HavSin", "HAVERSINE" },
            { "CosH", "COSH" },
            { "SecH", "SECH" },
            { "SinH", "SINH" },
            { "TanH", "TANH" },
            { "CosInt", "COSINT" },
            { "SinInt", "SININT" },
            { "ErfcInv", "ERFCINV" },
            { "ErfInv", "ERFINV" },
            { "Kaiser", "KAISER" },
            { "Laplace", "LAPLACE" },
            { "Legend", "LEGEND" },
            { "LogNorm", "LOGNORMAL" },
            { "Maxwell", "MAXWELL" },
            { "BarWin", "BARTHANN" },
            { "ACscPro", "ACSCPRO" },
            { "ACotPro", "ACOTPRO" },
            { "ACscHPro", "ACSCHPRO" },
            { "ACotHPro", "ACOTHPRO" },
            { "ASecPro", "ASECPRO" },
            { "CscPro", "CSCPRO" },
            { "CscHPro", "CSCHPRO" },
            { "CotHPro", "COTHPRO" },
            { "RecipPro", "RECIPPRO" },
            { "SecPro", "SECPRO" },
            { "Rayleigh", "RAYLEIGH" },
            { "Boxcar", "BOXCAR" },
            { "ARB_X2", "X2DATA" },
            { "Sqrt", "SQRT" },
            { "Tan", "TAN" },
            { "Versiera", "VERSIERA" },
            { "Weibull", "WEIBULL" }
        }
    }
};

        /// <summary>
        /// Sets an arbitrary waveform for the specified channel
        /// </summary>
        public void SetArbitraryWaveform(int channel, ArbitraryWaveformCategory category, string waveformName)
        {
            ValidateChannel(channel);

            if (!_arbitraryWaveforms.ContainsKey(category))
                throw new ArgumentException($"Invalid arbitrary waveform category: {category}");

            var categoryWaveforms = _arbitraryWaveforms[category];
            if (!categoryWaveforms.ContainsKey(waveformName))
                throw new ArgumentException($"Invalid arbitrary waveform name: {waveformName}");

            string waveformCommand = categoryWaveforms[waveformName];
            SendCommand($"SOURCE{channel}:FUNCTION {waveformCommand}");
            Log($"Set arbitrary waveform for CH{channel} to {waveformName} ({waveformCommand})");
        }

        /// <summary>
        /// Gets all arbitrary waveform names for a specific category
        /// </summary>
        public List<string> GetArbitraryWaveformNames(ArbitraryWaveformCategory category)
        {
            if (!_arbitraryWaveforms.ContainsKey(category))
                throw new ArgumentException($"Invalid arbitrary waveform category: {category}");

            return new List<string>(_arbitraryWaveforms[category].Keys);
        }

        /// <summary>
        /// Gets a user-friendly description for an arbitrary waveform name
        /// </summary>
        public string GetArbitraryWaveformDescription(string waveformName)
        {
            Dictionary<string, string> descriptions = new Dictionary<string, string>
    {
        { "AM", "AM Signal" },
        { "AttALT", "Attenuation Oscillation Curve" },
        { "BandLim", "Bandwidth-Limited Signal" },
        { "Bworth", "Butterworth Filter" },
        { "Chshev1", "Chebyshev1 Filter" },
        { "Chshev2", "Chebyshev2 Filter" },
        { "SineVer", "Chopper Sine Waveform" },
        { "SineTra", "Clipped Sine Waveform" },
        { "Combin", "Combination Function" },
        { "CPulse", "C-Pulse Signal" },
        { "Pahcur", "Current Waveform Of Dc Brushless Motor" },
        { "CWPulse", "CW Pulse Signal" },
        { "Dischar", "Discharge Curve Of Ni-Mh Battery" },
        { "DualTone", "Dual-Tone Signal" },
        { "FM", "FM Signal" },
        { "AmpALT", "Gain Oscillation Curve" },
        { "Gamma", "Gamma Signal" },
        { "GateVibr", "Gate Self-Oscillation Signal" },
        { "GausPul", "Gauss Pulse" },
        { "SwigOsc", "Kinetic Energy-Time Curve Of Swing Oscillation" },
        { "LFMPulse", "Linear Fm Pulse Signal" },
        { "Log", "Logarithm Function And The Base Is 10" },
        { "Lorentz", "Lorentz Function" },
        { "MCNoise", "Mechanical Construction Noise" },
        { "NPulse", "Negative Pulse" },
        { "NegRamp", "Negative Ramp" },
        { "PM", "PM Signal" },
        { "PPulse", "Positive Pulse" },
        { "Ripple", "Power Ripple" },
        { "PWM", "PWM Signal" },
        { "Quake", "Quake Waveform" },
        { "Radar", "Radar Signal" },
        { "RouHalf", "Roundhalf Wave" },
        { "RousPM", "Roundspm Waveform" },
        { "SCR", "SCR Firing Profile" },
        { "PFM", "Sectioned Pulse Fm Signal" },
        { "Sinc", "Sinc Function" },
        { "StairDn", "Stair-Down Waveform" },
        { "StairUD", "Stair-Up And Stair-Down Waveform" },
        { "StairUp", "Stair-Up Waveform" },
        { "StepResp", "Step-Response Signal" },
        { "Surge", "Surge Signal" },
        { "DampOsc", "Time-Displacement Curve Of Damped Oscillation" },
        { "BlaWave", "Time-Velocity Curve Of Explosive Vibration" },
        { "Trapezia", "Trapezia Waveform" },
        { "TV", "TV Signal" },
        { "Voice", "Voice Signal" },
        { "Cardiac", "Cardiac Signal" },
        { "ECG1", "Electrocardiogram 1" },
        { "ECG10", "Electrocardiogram 10" },
        { "ECG11", "Electrocardiogram 11" },
        { "ECG12", "Electrocardiogram 12" },
        { "ECG13", "Electrocardiogram 13" },
        { "ECG14", "Electrocardiogram 14" },
        { "ECG15", "Electrocardiogram 15" },
        { "ECG2", "Electrocardiogram 2" },
        { "ECG3", "Electrocardiogram 3" },
        { "ECG4", "Electrocardiogram 4" },
        { "ECG5", "Electrocardiogram 5" },
        { "ECG6", "Electrocardiogram 6" },
        { "ECG7", "Electrocardiogram 7" },
        { "ECG8", "Electrocardiogram 8" },
        { "ECG9", "Electrocardiogram 9" },
        { "EEG", "Electroencephalogram" },
        { "EMG", "Electromyogram" },
        { "EOG", "Electro-Oculogram" },
        { "LFPulse", "Low Frequency Pulse Electrotherapy" },
        { "Tens1", "Nerve Stimulation Electrotherapy 1" },
        { "Tens2", "Nerve Stimulation Electrotherapy 2" },
        { "Tens3", "Nerve Stimulation Electrotherapy 3" },
        { "Pulgram", "Pulsilogram" },
        { "ResSpd", "Speed Curve Of The Respiration" },
        { "TP5A", "Cut-Off Of Battery Power Transient 5A" },
        { "TP5B", "Cut-Off Of Battery Power Transient 5B" },
        { "TP1", "Disconnection Transient" },
        { "TP2B", "Ignition Switching Off Transient" },
        { "Ignition", "Ignition Waveform" },
        { "TP2A", "Inductance In Wiring Transient" },
        { "VR", "Resetting Supply Voltage Profile" },
        { "SP", "Starting Profile With Ringing" },
        { "TP4", "Start-Up Profile" },
        { "TP3B", "Switching  Transient 3B" },
        { "TP3A", "Switching Transient 3A" },
        { "AbsSinH", "Absolute Value Of Half Sine" },
        { "AbsSine", "Absolute Value Of Sine" },
        { "Laguerre", "Aguerre Polynomial 4Th Order" },
        { "Airy", "Airy Function" },
        { "ACos", "Arc Cosine" },
        { "ACosH", "Arc Hyperbolic Cosine" },
        { "ASecH", "Arc Hyperbolic Secant" },
        { "ASinH", "Arc Hyperbolic Sine" },
        { "ATanH", "Arc Hyperbolic Tangent" },
        { "ASin", "Arc Sinc" },
        { "ATan", "Arc Tangent" },
        { "Bartlett", "Bartlett Window" },
        { "Besseli", "Bessel Functions Of The First Kind" },
        { "Bessely", "Bessel Functions Of The Second Kind" },
        { "Blkman", "Blackman Window" },
        { "BlkmanH", "Blackman-Harris Window" },
        { "BohWin", "Bohman Window" },
        { "Cauchy", "Cauchy Distribution" },
        { "ChebWin", "Chebyshev Window" },
        { "Erfc", "Complementary Error Function" },
        { "ACscCon", "Concave Arc Cosecant" },
        { "ACotCon", "Concave Arc Cotangent" },
        { "ACscHCon", "Concave Arc Hyperbolic Cosecant" },
        { "ACotHCon", "Concave Arc Hyperbolic Cotangent" },
        { "ASecCon", "Concave Arc Secant" },
        { "CscCon", "Concave Cosecant" },
        { "CscHCon", "Concave Hyperbolic Cosecant" },
        { "CotHCon", "Concave Hyperbolic Cotangent" },
        { "RecipCon", "Concave Reciprocal" },
        { "SecCon", "Concave Secant" },
        { "Cot", "Cotangent" },
        { "Cubic", "Cubic Function" },
        { "Dirichlet", "Dirichlet Function" },
        { "Erf", "Error Function" },
        { "ExpFall", "Exponential Fall Function" },
        { "ExpRise", "Exponential Rise Function" },
        { "FlatWin", "Flat Top Weighted Window" },
        { "Gauss", "Gaussian Distribution Or Normal Distribution" },
        { "Ham", "Hamming Window" },
        { "Hanning", "Hanning Window" },
        { "HavSin", "Haversine Function" },
        { "CosH", "Hyperbolic Cosine" },
        { "SecH", "Hyperbolic Secant" },
        { "SinH", "Hyperbolic Sine" },
        { "TanH", "Hyperbolic Tangent" },
        { "CosInt", "Integral Cosine" },
        { "SinInt", "Integral Sine" },
        { "ErfcInv", "Inverted Complementary Error Function" },
        { "ErfInv", "Inverted Error Function" },
        { "Kaiser", "Kaiser Window" },
        { "Laplace", "Laplace Distribution" },
        { "Legend", "Legend Polynomial 5Th Order" },
        { "LogNorm", "Logarithmic Normal Distribution" },
        { "Maxwell", "Maxwell Distribution" },
        { "BarWin", "Modified Bartlett-Hann Window" },
        { "ACscPro", "Protuberant Arc Cosecant" },
        { "ACotPro", "Protuberant Arc Cotangent" },
        { "ACscHPro", "Protuberant Arc Hyperbolic Cosecant" },
        { "ACotHPro", "Protuberant Arc Hyperbolic Cotangent" },
        { "ASecPro", "Protuberant Arc Secant" },
        { "CscPro", "Protuberant Cosecant" },
        { "CscHPro", "Protuberant Hyperbolic Cosecant" },
        { "CotHPro", "Protuberant Hyperbolic Cotangent" },
        { "RecipPro", "Protuberant Reciprocal" },
        { "SecPro", "Protuberant Secant" },
        { "Rayleigh", "Rayleigh Distribution" },
        { "Boxcar", "Rectangular Window" },
        { "ARB_X2", "Square Function" },
        { "Sqrt", "Square Root" },
        { "Tan", "Tangent" },
        { "Versiera", "Versiera" },
        { "Weibull", "Weibull Distribution" }
    };

            if (descriptions.ContainsKey(waveformName))
                return descriptions[waveformName];

            return waveformName; // Return the original name if no description is found
        }



        /// <summary>
        /// Gets the SCPI command for a specific arbitrary waveform
        /// </summary>
        /// <param name="category">The category of the arbitrary waveform</param>
        /// <param name="waveformName">The name of the arbitrary waveform</param>
        /// <returns>The SCPI command for the specified waveform</returns>
        public string GetScpiCommandForArbitraryWaveform(ArbitraryWaveformCategory category, string waveformName)
        {
            // Check if the category exists
            if (!_arbitraryWaveforms.ContainsKey(category))
                return waveformName.ToUpper(); // Default fallback

            // Check if the waveform exists in the category
            if (_arbitraryWaveforms[category].ContainsKey(waveformName))
                return _arbitraryWaveforms[category][waveformName];

            // If not found, return the original name as fallback
            return waveformName.ToUpper();
        }

        /// <summary>
        /// Searches all categories to find the SCPI command for a waveform name
        /// </summary>
        /// <param name="waveformName">The name of the arbitrary waveform</param>
        /// <returns>The SCPI command for the specified waveform if found, otherwise the original name</returns>
        public string FindScpiCommandForArbitraryWaveform(string waveformName)
        {
            foreach (var category in _arbitraryWaveforms.Keys)
            {
                if (_arbitraryWaveforms[category].ContainsKey(waveformName))
                    return _arbitraryWaveforms[category][waveformName];
            }

            return waveformName.ToUpper(); // Default fallback
        }

        /// <summary>
        /// Finds the category and friendly name for a given SCPI waveform command
        /// </summary>
        /// <param name="scpiCommand">The SCPI command string</param>
        /// <returns>A tuple with the category and friendly name, or null if not found</returns>
        public (ArbitraryWaveformCategory Category, string FriendlyName)? FindArbitraryWaveformByScpiCommand(string scpiCommand)
        {
            // Normalize the input
            string normalizedCommand = scpiCommand.Trim().ToUpper();

            // Search all categories
            foreach (var category in _arbitraryWaveforms.Keys)
            {
                // Search all waveforms in this category
                foreach (var kvp in _arbitraryWaveforms[category])
                {
                    if (kvp.Value.ToUpper() == normalizedCommand)
                    {
                        return (category, kvp.Key);
                    }
                }
            }

            // Not found
            return null;
        }


        /// <summary>
        /// Gets all arbitrary waveform categories
        /// </summary>
        public List<ArbitraryWaveformCategory> GetArbitraryWaveformCategories()
        {
            return new List<ArbitraryWaveformCategory>(_arbitraryWaveforms.Keys);
        }

        /// <summary>
        /// Determines whether the current waveform is an arbitrary waveform
        /// </summary>
        public bool IsArbitraryWaveform(int channel)
        {
            ValidateChannel(channel);
            string currentWaveform = SendQuery($"SOURCE{channel}:FUNC?").Trim().ToUpper();

            // Check all categories and waveforms
            foreach (var category in _arbitraryWaveforms.Values)
            {
                foreach (var waveform in category.Values)
                {
                    if (currentWaveform == waveform)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the friendly name of the current arbitrary waveform
        /// </summary>
        public string GetCurrentArbitraryWaveformName(int channel)
        {
            ValidateChannel(channel);
            string currentWaveform = SendQuery($"SOURCE{channel}:FUNC?").Trim().ToUpper();

            // Check all categories and waveforms
            foreach (var category in _arbitraryWaveforms)
            {
                foreach (var waveform in category.Value)
                {
                    if (currentWaveform == waveform.Value)
                        return waveform.Key;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Gets the category of the current arbitrary waveform
        /// </summary>
        public ArbitraryWaveformCategory? GetCurrentArbitraryWaveformCategory(int channel)
        {
            ValidateChannel(channel);
            string currentWaveform = SendQuery($"SOURCE{channel}:FUNC?").Trim().ToUpper();

            // Check all categories and waveforms
            foreach (var category in _arbitraryWaveforms)
            {
                foreach (var waveform in category.Value)
                {
                    if (currentWaveform == waveform.Value)
                        return category.Key;
                }
            }

            return null;
        }


        /// <summary>
        /// Gets information about a specific arbitrary waveform
        /// </summary>
        /// <param name="waveformName">Name of the waveform</param>
        /// <returns>Description of the waveform and its parameters</returns>
        public string GetArbitraryWaveformInfo(string waveformName)
        {
            switch (waveformName.ToUpper())
            {
                case "USER":
                    return "User-defined arbitrary waveform. Upload your custom waveform data through other means.";
                case "SINC":
                    return "Sinc waveform: sin(x)/x function, used in signal processing and filter design.";
                case "GAUSSIAN":
                    return "Gaussian waveform: Normal distribution curve. Commonly used in statistics and signal processing.";
                case "LORENTZ":
                    return "Lorentz waveform: Used in spectroscopy and resonance analysis.";
                case "EXPONENTIAL RISE":
                    return "Exponential rise: Models capacitor charging and other natural growth processes.";
                case "EXPONENTIAL FALL":
                    return "Exponential fall: Models capacitor discharging and natural decay processes.";
                case "HAVERSINE":
                    return "Haversine: Raised half-sine pulse, used in navigation and signal processing.";
                case "CARDIAC":
                    return "Cardiac: Simulates cardiac signal patterns for biomedical testing.";
                case "QUAKE":
                    return "Quake: Simulates earthquake vibration patterns.";
                case "CHIRP":
                    return "Chirp: Frequency swept signal, useful for testing frequency responses.";
                case "GAMMA":
                    return "Gamma: Special mathematical function used in statistics and physics.";
                case "VOICE":
                    return "Voice: Simulates voice pattern characteristics.";
                case "TV":
                    return "TV: Television signal pattern for testing.";
                case "HAMMING":
                    return "Hamming: Window function used in digital signal processing and spectral analysis.";
                case "HANNING":
                    return "Hanning: Window function used in digital signal processing with good frequency resolution.";
                case "KAISER":
                    return "Kaiser: Parameterized window function with adjustable side lobe characteristics.";
                default:
                    return "Standard arbitrary waveform. Use frequency, amplitude, offset and phase controls to adjust.";
            }
        }


        /// <summary>
        /// Applies an arbitrary waveform with basic parameters (frequency, amplitude, offset, phase)
        /// </summary>
        /// <param name="channel">Channel number (1 or 2)</param>
        /// <param name="waveformName">Name of the arbitrary waveform</param>
        /// <param name="frequency">Frequency in Hz</param>
        /// <param name="amplitude">Amplitude in Vpp</param>
        /// <param name="offset">DC offset in Volts</param>
        /// <param name="phase">Phase in degrees</param>
        /// <param name="additionalParams">Additional waveform-specific parameters</param>
        public void ApplyArbitraryWaveform(int channel, string waveformName, double frequency, double amplitude, double offset, double phase, Dictionary<string, double> additionalParams = null)
        {
            ValidateChannel(channel);

            try
            {
                // Map the friendly name to SCPI command
                string scpiWaveform = MapArbitraryWaveformToScpi(waveformName);

                // Apply command sets basic parameters in one command
                SendCommand($"SOURCE{channel}:APPLY:{scpiWaveform} {frequency},{amplitude},{offset},{phase}");
                System.Threading.Thread.Sleep(100); // Allow time for device to process

                // Apply any additional waveform-specific parameters
                if (additionalParams != null && additionalParams.Count > 0)
                {
                    foreach (var param in additionalParams)
                    {
                        try
                        {
                            SendCommand($"SOURCE{channel}:FUNCTION:{scpiWaveform}:PARAMETER{param.Key} {param.Value}");
                            System.Threading.Thread.Sleep(20);
                        }
                        catch (Exception paramEx)
                        {
                            // Log but continue with other parameters
                            Log($"Error setting additional parameter {param.Key}: {paramEx.Message}");
                        }
                    }
                }

                Log($"Applied arbitrary waveform '{waveformName}' to CH{channel} with " +
                    $"Freq={frequency}Hz, Amp={amplitude}Vpp, Offset={offset}V, Phase={phase}°");
            }
            catch (Exception ex)
            {
                Log($"Error applying arbitrary waveform: {ex.Message}");
                throw;
            }
        }


        /// <summary>
        /// Maps a friendly waveform name to its corresponding SCPI command
        /// </summary>
        /// <param name="waveformName">User-friendly waveform name</param>
        /// <returns>SCPI command for the waveform</returns>
        public string MapArbitraryWaveformToScpi(string waveformName)
        {
            switch (waveformName.ToUpper())
            {
                case "USER": return "USER";
                case "SINC": return "SINC";
                case "GAUSSIAN": return "GAUSS";
                case "LORENTZ": return "LORENTZ";
                case "EXPONENTIAL RISE": return "EXPRISE";
                case "EXPONENTIAL FALL": return "EXPFALL";
                case "HAVERSINE": return "HAVERSINE";
                case "CARDIAC": return "CARDIAC";
                case "QUAKE": return "QUAKE";
                case "CHIRP": return "BANDLIMITED";
                case "GAMMA": return "GAMMA";
                case "VOICE": return "VOICE";
                case "TV": return "TV";
                case "HAMMING": return "HAMMING";
                case "HANNING": return "HANNING";
                case "KAISER": return "KAISER";
                default: return "USER";
            }
        }



        /// <summary>
        /// Gets the current arbitrary waveform type from the device
        /// </summary>
        /// <param name="channel">Channel number (1 or 2)</param>
        /// <returns>SCPI waveform name</returns>
        public string GetArbitraryWaveformType(int channel)
        {
            ValidateChannel(channel);
            try
            {
                // For standard arbitrary waveforms, the device should report the type
                string response = SendQuery($"SOURCE{channel}:FUNC?");
                return response.Trim();
            }
            catch (Exception ex)
            {
                Log($"Error getting arbitrary waveform type: {ex.Message}");
                return "USER";
            }
        }

        #endregion


        #region DC Waveform Methods
        public double GetDCVoltage(int channel)
        {
            try
            {
                // For DC mode, the voltage is stored as the offset
                // We can use the same method to query it
                return GetOffset(channel);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting DC voltage for channel {channel}: {ex.Message}");
            }
        }

        public void SetDCVoltage(int channel, double voltage)
        {
            try
            {
                // Apply DC waveform with specified voltage
                // Per documentation, we need placeholders for frequency and amplitude
                SendCommand($":SOURCE{channel}:APPLY:DC 1,1,{voltage}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error setting DC voltage for channel {channel}: {ex.Message}");
            }
        }

        public void SetOutputImpedance(int channel, double impedance)
        {
            try
            {
                SendCommand($":OUTP{channel}:IMP {impedance}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error setting output impedance for channel {channel}: {ex.Message}");
            }
        }

        public void SetOutputImpedanceHighZ(int channel)
        {
            try
            {
                SendCommand($":OUTP{channel}:IMP INF");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error setting output impedance to High-Z for channel {channel}: {ex.Message}");
            }
        }

        public double GetOutputImpedance(int channel)
        {
            try
            {
                string response = SendQuery($":OUTP{channel}:IMP?");

                // Check for INF or very large value indicating High-Z
                if (response.Contains("INF") || double.Parse(response) > 1e10)
                {
                    return double.PositiveInfinity; // Indicate High-Z
                }

                return double.Parse(response);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting output impedance for channel {channel}: {ex.Message}");
            }
        }

        #endregion

    }
}