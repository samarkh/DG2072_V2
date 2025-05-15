using System;

namespace DG2072_USB_Control.Continuous.ArbitraryWaveform.Descriptions
{
    public class MathematicalDescriptions : IWaveformDescription
    {
        public bool SupportsWaveform(string waveformName)
        {
            switch (waveformName.ToUpper())
            {
                case "SINC":
                case "GAUSS":
                case "LORENTZ":
                case "EXPRISE":
                case "EXPFALL":
                case "ABSSINEHALF":
                case "ABSSINE":
                case "SQRT":
                case "X2DATA":
                    return true;
                default:
                    return false;
            }
        }

        public string GetBasicInfo(string waveformName)
        {
            switch (waveformName.ToUpper())
            {
                case "SINC":
                    return "Sinc Function is a sine function divided by its argument: sin(x)/x. " +
                           "This function is widely used in signal processing and interpolation.";

                case "GAUSS":
                case "GAUSSIAN":
                    return "Gaussian Function is a bell-shaped curve that represents the normal distribution " +
                           "in probability theory. It's defined as exp(-x^2/2).";

                case "LORENTZ":
                    return "Lorentz Function represents a resonance curve. It's commonly used in spectroscopy " +
                           "and describes the shape of resonance phenomena.";

                case "EXPRISE":
                    return "Exponential Rise Function represents a growth that increases by a constant percentage " +
                           "over time. It's commonly seen in charging capacitors and population growth.";

                case "EXPFALL":
                    return "Exponential Fall Function represents a decay that decreases by a constant percentage " +
                           "over time. It's commonly seen in discharging capacitors and radioactive decay.";

                default:
                    return $"The {waveformName} waveform is a mathematical function used for signal processing.";
            }
        }

        public string GetParameterInfo(string waveformName)
        {
            switch (waveformName.ToUpper())
            {
                case "SINC":
                    return "Parameters:\n" +
                           "• Zero Crossings: Controls the number of zero crossings in the sinc function. Higher values increase the oscillation frequency.";

                case "GAUSS":
                case "GAUSSIAN":
                case "LORENTZ":
                    return "Parameters:\n" +
                           "• Width: Controls the width of the function's peak (%). Larger values create a broader curve.\n" +
                           "• Center: Sets the position of the peak within the waveform period (%).";

                case "EXPRISE":
                case "EXPFALL":
                    return "Parameters:\n" +
                           "• Time Constant: Controls how quickly the function rises or falls (%). Smaller values result in faster change.";

                default:
                    return "Use the standard controls to adjust frequency, amplitude, offset, and phase.";
            }
        }

        public string GetApplicationInfo(string waveformName)
        {
            switch (waveformName.ToUpper())
            {
                case "SINC":
                    return "Applications:\n" +
                           "• Filter design (especially for low-pass filters)\n" +
                           "• Signal reconstruction in digital signal processing\n" +
                           "• Signal interpolation\n" +
                           "• Optical diffraction patterns\n" +
                           "• Antenna design";

                case "GAUSS":
                case "GAUSSIAN":
                    return "Applications:\n" +
                           "• Filter design (minimal time-bandwidth product)\n" +
                           "• Statistical modeling and analysis\n" +
                           "• Laser pulse shaping\n" +
                           "• Random noise generation\n" +
                           "• Spectroscopic line shape analysis";

                case "LORENTZ":
                    return "Applications:\n" +
                           "• Spectroscopic line shape analysis\n" +
                           "• Resonance studies in physics\n" +
                           "• NMR and EPR spectroscopy\n" +
                           "• Damped oscillator systems\n" +
                           "• Circuit analysis with resonant components";

                case "EXPRISE":
                    return "Applications:\n" +
                           "• Capacitor charging simulation\n" +
                           "• Growth process modeling\n" +
                           "• RC circuit response\n" +
                           "• Step response testing\n" +
                           "• Thermal systems analysis";

                case "EXPFALL":
                    return "Applications:\n" +
                           "• Capacitor discharging simulation\n" +
                           "• Decay process modeling\n" +
                           "• RC circuit discharge response\n" +
                           "• Half-life measurements\n" +
                           "• Transient response analysis";

                default:
                    return "Common applications include signal processing, simulation, and mathematical modeling.";
            }
        }

        public string GetParameterHelp(string waveformName, int paramNumber)
        {
            string paramKey = $"{waveformName.ToUpper()}_PARAM{paramNumber}";

            switch (paramKey)
            {
                case "SINC_PARAM1":
                    return "Zero Crossings: Controls how many times the sinc function crosses zero on each side of " +
                           "the central peak. Higher values create more oscillations in the signal.";

                case "GAUSS_PARAM1":
                case "GAUSSIAN_PARAM1":
                case "LORENTZ_PARAM1":
                    return "Width: Controls the width of the function's peak as a percentage of the total period. " +
                           "Larger values create a broader curve, while smaller values make the peak narrower and sharper.";

                case "GAUSS_PARAM2":
                case "GAUSSIAN_PARAM2":
                case "LORENTZ_PARAM2":
                    return "Center: Determines where the peak of the function appears within the waveform period, " +
                           "as a percentage from 0% (beginning) to 100% (end).";

                case "EXPRISE_PARAM1":
                    return "Time Constant: Controls how quickly the function rises. It represents the time needed " +
                           "to reach approximately 63% of the final value. Smaller values result in faster rise times.";

                case "EXPFALL_PARAM1":
                    return "Time Constant: Controls how quickly the function falls. It represents the time needed " +
                           "to decrease to approximately 37% of the initial value. Smaller values result in faster fall times.";

                default:
                    return "This parameter adjusts a specific mathematical characteristic of the waveform.";
            }
        }
    }
}