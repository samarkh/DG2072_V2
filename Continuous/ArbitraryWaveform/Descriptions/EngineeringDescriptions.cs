using System;

namespace DG2072_USB_Control.Continuous.ArbitraryWaveform.Descriptions
{
    public class EngineeringDescriptions : IWaveformDescription
    {
        public bool SupportsWaveform(string waveformName)
        {
            switch (waveformName.ToUpper())
            {
                case "BANDLIMITED":
                case "CHIRP":
                case "ATTALT":
                case "BWORTH":
                case "CHSHEV1":
                case "CHSHEV2":
                case "SINEVER":
                case "SINETRA":
                case "COMBIN":
                case "CPULSE":
                case "PAHCUR":
                case "CWPULSE":
                case "DISCHAR":
                case "DUALTONE":
                case "FM":
                case "AMPALT":
                case "GATEVIBR":
                case "GAUSSPULSE":
                case "SWINGOSC":
                case "LFMPULSE":
                case "LOG":
                case "MCNOISE":
                case "NPULSE":
                case "NEGRAMP":
                case "PM":
                case "PPULSE":
                case "RIPPLE":
                case "PWM":
                case "QUAKE":
                case "RADAR":
                case "ROUNDHALF":
                case "ROUNDPM":
                case "SCR":
                case "PFM":
                case "STAIRDN":
                case "STAIRUD":
                case "STAIRUP":
                case "STEPRESP":
                case "SURGE":
                case "DAMPEDOSC":
                case "BLAWAVE":
                case "TRAPEZIA":
                case "TV":
                case "VOICE":
                    return true;
                default:
                    return false;
            }
        }

        public string GetBasicInfo(string waveformName)
        {
            switch (waveformName.ToUpper())
            {
                case "BANDLIMITED":
                case "CHIRP":
                    return "A chirp or bandlimited signal is a frequency-swept waveform where the frequency changes " +
                           "continuously over time, typically from a start to end frequency. This provides a way to " +
                           "test frequency responses across a wide spectrum.";

                case "ATTALT":
                    return "Attenuation Oscillation Curve (AttALT) shows periodic reduction in amplitude over time. " +
                           "This waveform models signal attenuation patterns in transmission lines and other media.";

                case "BWORTH":
                    return "Butterworth Filter waveform represents the frequency response of a Butterworth filter, " +
                           "which is designed to have a maximally flat frequency response in the passband. This is " +
                           "a common filter type in signal processing applications.";

                case "CHSHEV1":
                    return "Chebyshev Type I Filter waveform represents the frequency response of a Chebyshev filter " +
                           "with ripple in the passband but steeper roll-off than Butterworth filters. This filter " +
                           "sacrifices passband flatness for steeper transition to stopband.";

                case "CHSHEV2":
                    return "Chebyshev Type II Filter waveform represents the frequency response of a Chebyshev filter " +
                           "with ripple in the stopband but flat passband. This filter provides steep cutoff while " +
                           "maintaining a flat passband response.";

                case "SINEVER":
                    return "Chopper Sine Waveform exhibits vertical switching characteristics on a sine wave. " +
                           "This represents waveforms typically seen in power conversion systems using chopping techniques.";

                case "SINETRA":
                    return "Clipped Sine Waveform shows a sine wave with amplitude limiting (flat peaks). This simulates " +
                           "sine waves that have been clipped by amplifier overdriving or intentional limiting.";

                case "STEPRESP":
                    return "Step Response Signal shows how a system responds to a sudden change in input. This is a " +
                           "fundamental test signal for characterizing system dynamics, stability, and response time.";

                case "RADAR":
                    return "Radar Signal consists of specialized pulses used in radar systems for object detection and " +
                           "ranging. This simulates typical electromagnetic patterns used in radar applications.";

                case "TV":
                    return "TV Signal emulates a composite video signal for television systems, including sync " +
                           "pulses and video information. This is useful for testing video transmission systems.";

                case "VOICE":
                    return "Voice Signal models the average amplitude envelope of human speech patterns. This " +
                           "simulates the acoustic energy distribution typical in vocal communications.";

                // Add more engineering waveform descriptions...

                default:
                    return $"The {waveformName} is an engineering waveform used in signal processing and system analysis.";
            }
        }

        public string GetParameterInfo(string waveformName)
        {
            switch (waveformName.ToUpper())
            {
                case "BANDLIMITED":
                case "CHIRP":
                    return "Parameters:\n" +
                           "• Start Frequency: Sets the initial frequency of the sweep (Hz).\n" +
                           "• End Frequency: Sets the final frequency of the sweep (Hz).\n\n" +
                           "The frequency sweeps linearly from start to end over one period.";

                case "STEPRESP":
                    return "Parameters:\n" +
                           "The step response characteristics are controlled by the main frequency parameter, " +
                           "which determines how fast the step response reaches steady state.";

                // Add more parameter descriptions...

                default:
                    return "Use the frequency, amplitude, offset and phase controls to adjust the basic characteristics.";
                    //return "Common Applications:";



            }
        }

        public string GetApplicationInfo(string waveformName)
        {
            switch (waveformName.ToUpper())
            {
                case "BANDLIMITED":
                case "CHIRP":
                    return "Applications:\n" +
                           "• Radar and sonar systems\n" +
                           "• Frequency response analysis\n" +
                           "• Component bandwidth testing\n" +
                           "• Audio and acoustic testing\n" +
                           "• Ultrasonic systems";

                case "STEPRESP":
                    return "Applications:\n" +
                           "• Control systems analysis\n" +
                           "• Amplifier and filter testing\n" +
                           "• Transient response characterization\n" +
                           "• System stability evaluation\n" +
                           "• Servo mechanism testing";

                case "RADAR":
                    return "Applications:\n" +
                           "• Radar system development and testing\n" +
                           "• Object detection simulation\n" +
                           "• Distance measurement systems\n" +
                           "• Doppler effect demonstrations\n" +
                           "• RF receiver testing";

                case "TV":
                    return "Applications:\n" +
                           "• Television system testing\n" +
                           "• Video transmission evaluation\n" +
                           "• Display device calibration\n" +
                           "• Video signal processing\n" +
                           "• Sync separator circuit testing";

                // Add more application descriptions...

                default:
                    return "Common applications include system testing, signal processing research, and electronic component evaluation.";
            }
        }

        public string GetParameterHelp(string waveformName, int paramNumber)
        {
            string paramKey = $"{waveformName.ToUpper()}_PARAM{paramNumber}";

            switch (paramKey)
            {
                case "BANDLIMITED_PARAM1":
                case "CHIRP_PARAM1":
                    return "Start Frequency (Hz): Sets the beginning frequency of the sweep. " +
                           "The waveform will begin oscillating at this frequency. This should typically be lower " +
                           "than the End Frequency for an up-chirp or higher for a down-chirp.";

                case "BANDLIMITED_PARAM2":
                case "CHIRP_PARAM2":
                    return "End Frequency (Hz): Sets the final frequency of the sweep. " +
                           "The waveform will complete its period at this frequency. For a meaningful sweep, " +
                           "this should differ significantly from the start frequency.";

                // Add more parameter help text...

                default:
                    return "Adjust this parameter to modify the waveform's engineering characteristics.";
            }
        }
    }
}