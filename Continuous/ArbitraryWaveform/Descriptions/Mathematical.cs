using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DG2072_USB_Control.Continuous.ArbitraryWaveform.Descriptions
{
    internal class Mathematical
    {
    }
}


namespace DG2072_USB_Control.Continuous.ArbitraryWaveform.Descriptions
{
    public class MathematicalDescriptions : IWaveformDescription
    {
        public bool SupportsWaveform(string waveformName)
        {
            switch (waveformName.ToUpper())
            {
                case "SINC":
                case "GAUSSIAN":
                case "GAUSS":
                case "LORENTZ":
                case "EXPONENTIAL RISE":
                case "EXPRISE":
                case "EXPONENTIAL FALL":
                case "EXPFALL":
                case "HAVERSINE":
                case "GAMMA":
                case "AIRY":
                case "BESSELI":
                case "BESSELY":
                case "ERF":
                case "ERFC":
                case "ERFINV":
                case "ERFCINV":
                case "DIRICHLET":
                case "COSINT":
                case "SININT":
                case "X2DATA":
                case "SQRT":
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
                    return "The sinc function is defined as sin(πx)/(πx), producing a waveform with a central peak and " +
                           "symmetrical side lobes that diminish in amplitude. It is the Fourier transform of a rectangular pulse " +
                           "and has fundamental importance in signal processing and sampling theory.";

                case "GAUSSIAN":
                case "GAUSS":
                    return "The Gaussian waveform follows the normal probability distribution, creating a symmetrical bell-shaped " +
                           "curve that is widely used in statistics, probability theory, and signal processing. It's defined by " +
                           "the function e^(-(x-μ)²/(2σ²)), where μ is the center and σ is related to the width.";

                case "LORENTZ":
                    return "The Lorentz function (also known as Cauchy distribution) produces a bell-shaped curve with wider " +
                           "tails than the Gaussian. It's frequently used in spectroscopy to model resonance phenomena " +
                           "and is defined by the function 1/[π·γ·(1+((x-x₀)/γ)²)], where γ controls the width.";

                case "EXPONENTIAL RISE":
                case "EXPRISE":
                    return "The exponential rise function models the charging of a capacitor in an RC circuit, starting " +
                           "at zero and approaching a maximum value asymptotically. It follows the formula 1-e^(-x/τ), " +
                           "where τ is the time constant that controls the rate of rise.";

                case "EXPONENTIAL FALL":
                case "EXPFALL":
                    return "The exponential decay function models the discharging of a capacitor in an RC circuit, starting " +
                           "at maximum and approaching zero asymptotically. It follows the formula e^(-x/τ), " +
                           "where τ is the time constant that controls the rate of decay.";

                case "HAVERSINE":
                    return "The haversine function is a raised half-sine pulse, defined as (1-cos(x))/2, " +
                           "commonly used in navigation calculations and as a windowing function in signal processing. " +
                           "It plays a key role in calculating great-circle distances on spherical surfaces.";

                case "GAMMA":
                    return "The gamma function extends the factorial to complex and real number arguments. " +
                           "It has applications in probability, statistics, and many areas of mathematics and physics. " +
                           "The function is defined as the improper integral from 0 to ∞ of t^(z-1)·e^(-t) dt.";

                // Add more mathematical waveform descriptions...

                default:
                    return $"The {waveformName} function is a mathematical waveform with applications in signal processing and analysis.";
            }
        }

        public string GetParameterInfo(string waveformName)
        {
            switch (waveformName.ToUpper())
            {
                case "SINC":
                    return "Parameters:\n" +
                           "• Zero Crossings: Controls the number of zero crossings on each side of the central peak. " +
                           "Higher values create more side lobes and can better represent ideal low-pass filter characteristics.";

                case "GAUSSIAN":
                case "GAUSS":
                case "LORENTZ":
                    return "Parameters:\n" +
                           "• Width: Controls the spread of the pulse as a percentage (%). Lower values create narrower pulses " +
                           "with higher peak amplitudes.\n" +
                           "• Center: Sets the center position of the pulse (%) within the waveform period, effectively " +
                           "shifting the curve horizontally.";

                case "EXPONENTIAL RISE":
                case "EXPRISE":
                case "EXPONENTIAL FALL":
                case "EXPFALL":
                    return "Parameters:\n" +
                           "• Time Constant: Controls how quickly the function rises or falls as a percentage of the period. " +
                           "Lower values create steeper transitions, simulating circuits with smaller RC time constants.";

                // Add more parameter descriptions...

                default:
                    return "Use the frequency, amplitude, offset and phase controls to adjust the basic characteristics.";
            }
        }

        public string GetApplicationInfo(string waveformName)
        {
            switch (waveformName.ToUpper())
            {
                case "SINC":
                    return "Applications:\n" +
                           "• Digital signal processing and filter design\n" +
                           "• Signal reconstruction and sampling theory\n" +
                           "• Communications systems testing\n" +
                           "• Antenna and RF systems characterization\n" +
                           "• Demonstration of Fourier transform principles";

                case "GAUSSIAN":
                case "GAUSS":
                    return "Applications:\n" +
                           "• Statistical and probability simulations\n" +
                           "• Radar pulse generation\n" +
                           "• Optical and laser systems testing\n" +
                           "• Filter response testing\n" +
                           "• Noise modeling and signal smoothing\n" +
                           "• Image processing algorithms";

                case "LORENTZ":
                    return "Applications:\n" +
                           "• Spectroscopy and resonance studies\n" +
                           "• Nuclear magnetic resonance (NMR) simulation\n" +
                           "• Optical systems testing\n" +
                           "• Specialized filter design\n" +
                           "• Physical resonance simulations";

                case "EXPONENTIAL RISE":
                case "EXPRISE":
                    return "Applications:\n" +
                           "• RC circuit simulation\n" +
                           "• Capacitor charging modeling\n" +
                           "• Step response testing\n" +
                           "• Power supply turn-on characterization\n" +
                           "• Natural growth processes simulation";

                case "EXPONENTIAL FALL":
                case "EXPFALL":
                    return "Applications:\n" +
                           "• RC circuit discharge simulation\n" +
                           "• Decay process modeling\n" +
                           "• Transient response testing\n" +
                           "• Sensor response characterization\n" +
                           "• Natural decay phenomena simulation";

                // Add more application descriptions...

                default:
                    return "Applications include mathematical modeling, scientific simulations, and educational demonstrations of fundamental principles.";
            }
        }

        public string GetParameterHelp(string waveformName, int paramNumber)
        {
            string paramKey = $"{waveformName.ToUpper()}_PARAM{paramNumber}";

            switch (paramKey)
            {
                case "SINC_PARAM1":
                    return "Zero Crossings: Controls the number of zero crossings (or lobes) visible in the sinc function. " +
                           "Higher values create more oscillations within the same period, producing a more detailed " +
                           "representation of the ideal low-pass filter response in the frequency domain.";

                case "GAUSSIAN_PARAM1":
                case "GAUSS_PARAM1":
                case "LORENTZ_PARAM1":
                    return "Width Parameter: Controls the width of the pulse as a percentage of the full scale. " +
                           "Values range from 1-100%. Lower values create sharper, narrower pulses with higher peak amplitudes. " +
                           "This corresponds to the standard deviation (σ) in a Gaussian distribution.";

                case "GAUSSIAN_PARAM2":
                case "GAUSS_PARAM2":
                case "LORENTZ_PARAM2":
                    return "Center Parameter: Sets the center position of the pulse within the period as a percentage. " +
                           "Values range from 0-100% of the total waveform period. This corresponds to the mean (μ) " +
                           "in a Gaussian distribution, shifting the curve horizontally.";

                case "EXPRISE_PARAM1":
                case "EXPONENTIAL RISE_PARAM1":
                    return "Time Constant: Controls how quickly the exponential rise reaches its final value. " +
                           "Lower values create steeper, faster rises, simulating circuits with smaller RC time constants. " +
                           "Values represent percentage of the total period.";

                case "EXPFALL_PARAM1":
                case "EXPONENTIAL FALL_PARAM1":
                    return "Time Constant: Controls how quickly the exponential decay approaches zero. " +
                           "Lower values create steeper, faster decays, simulating circuits with smaller RC time constants. " +
                           "Values represent percentage of the total period.";

                // Add more parameter-specific help text...

                default:
                    return "Adjust this parameter to modify the mathematical function's characteristics.";
            }
        }
    }
}