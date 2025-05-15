using System;

namespace DG2072_USB_Control.Continuous.ArbitraryWaveform.Descriptions
{
    public class AutomotiveDescriptions : IWaveformDescription
    {
        public bool SupportsWaveform(string waveformName)
        {
            switch (waveformName.ToUpper())
            {
                case "IGNITION":
                case "ISO167502SP":
                case "ISO76372TP1":
                case "ISO76372TP2A":
                case "ISO76372TP2B":
                case "ISO76372TP3A":
                case "ISO76372TP3B":
                case "ISO76372TP4":
                case "ISO76372TP5A":
                case "ISO76372TP5B":
                case "ISO167502VR":
                    return true;
                default:
                    return false;
            }
        }

        public string GetBasicInfo(string waveformName)
        {
            switch (waveformName.ToUpper())
            {
                case "IGNITION":
                    return "Ignition Waveform provides a complete voltage profile during an engine ignition cycle. " +
                           "It simulates the characteristic voltage patterns seen in automotive ignition systems, " +
                           "including initial voltage rise, spark firing, and subsequent oscillation.";

                case "ISO167502SP":
                    return "Starting Profile With Ringing (ISO16750-2 SP) simulates the voltage profile during engine " +
                           "start-up, including the characteristic oscillatory ringing that occurs. This standard test " +
                           "pattern is used for evaluating electronic component performance during cranking.";

                case "ISO76372TP1":
                    return "Disconnection Transient (ISO7637-2 TP1) simulates the transient pulse generated when " +
                           "inductive loads are disconnected in a vehicle electrical system. This is used for testing " +
                           "component immunity to kick-back voltage spikes.";

                case "ISO76372TP2A":
                    return "Inductance In Wiring Transient (ISO7637-2 TP2A) simulates transient pulses caused by " +
                           "inductance in the vehicle wiring harness. This pattern is used for evaluating electronic " +
                           "module immunity to wiring harness effects.";

                case "ISO76372TP2B":
                    return "Ignition Switching Off Transient (ISO7637-2 TP2B) represents the transient pulse that " +
                           "occurs when the vehicle ignition is turned off. This standard test waveform is used for " +
                           "testing automotive system behavior during shutdown sequences.";

                // Add more automotive waveform descriptions...

                default:
                    return $"The {waveformName} waveform is an automotive electronics test signal conforming to industry standards.";
            }
        }

        public string GetParameterInfo(string waveformName)
        {
            switch (waveformName.ToUpper())
            {
                case "IGNITION":
                    return "Parameters:\n" +
                           "This waveform uses the standard frequency, amplitude, and offset controls to adjust " +
                           "the timing and voltage characteristics of the ignition profile.";

                case "ISO167502SP":
                case "ISO76372TP1":
                case "ISO76372TP2A":
                case "ISO76372TP2B":
                case "ISO76372TP3A":
                case "ISO76372TP3B":
                case "ISO76372TP4":
                case "ISO76372TP5A":
                case "ISO76372TP5B":
                case "ISO167502VR":
                    return "Parameters:\n" +
                           "This standardized automotive test pulse uses the frequency parameter to control " +
                           "the repetition rate, with amplitude and offset parameters determining voltage levels " +
                           "according to ISO test specifications.";

                // Add more parameter descriptions...

                default:
                    return "Use the frequency, amplitude, offset and phase controls to adjust the basic characteristics.";
            }
        }

        public string GetApplicationInfo(string waveformName)
        {
            switch (waveformName.ToUpper())
            {
                case "IGNITION":
                    return "Applications:\n" +
                           "• Engine control unit (ECU) testing\n" +
                           "• Ignition system diagnostics\n" +
                           "• Ignition coil evaluation\n" +
                           "• Spark plug testing\n" +
                           "• Vehicle electrical system simulation";

                case "ISO167502SP":
                    return "Applications:\n" +
                           "• Automotive electronics startup testing\n" +
                           "• Battery management system evaluation\n" +
                           "• ECU cranking performance testing\n" +
                           "• Voltage regulator evaluation\n" +
                           "• Power supply design verification";

                case "ISO76372TP1":
                    return "Applications:\n" +
                           "• Electromagnetic compatibility (EMC) testing\n" +
                           "• Electronic module immunity testing\n" +
                           "• Transient voltage suppression design\n" +
                           "• Component durability evaluation\n" +
                           "• Vehicle electronics quality assurance";

                // Add more application descriptions...

                default:
                    return "Applications include automotive electronics testing, component qualification, and vehicle electrical system simulation.";
            }
        }

        public string GetParameterHelp(string waveformName, int paramNumber)
        {
            string paramKey = $"{waveformName.ToUpper()}_PARAM{paramNumber}";

            // Most automotive waveforms don't have additional parameters beyond the standards
            return "This standardized automotive test pulse follows ISO specifications. Use frequency to control " +
                   "repetition rate and amplitude/offset to set appropriate voltage levels.";
        }
    }
}