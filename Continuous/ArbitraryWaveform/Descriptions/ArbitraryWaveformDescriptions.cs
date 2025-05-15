using DG2072_USB_Control.Continuous.ArbitraryWaveform.Descriptions;
using System.Collections.Generic;
using System.Linq;

public static class ArbitraryWaveformDescriptions
{
    private static readonly List<IWaveformDescription> _descriptionProviders = new List<IWaveformDescription>
    {
        new EngineeringDescriptions(),
        new MathematicalDescriptions(),
        new MedicalDescriptions(),
        new AutomotiveDescriptions()
    };

    /// <summary>
    /// Gets a comprehensive description of the specified waveform
    /// </summary>
    public static string GetDetailedDescription(string waveformName)
    {
        // Find the appropriate provider for this waveform
        IWaveformDescription provider = GetProviderForWaveform(waveformName);

        if (provider != null)
        {
            string baseInfo = provider.GetBasicInfo(waveformName);
            string parameters = provider.GetParameterInfo(waveformName);
            string applications = provider.GetApplicationInfo(waveformName);

            return string.Join("\n\n", new[] { baseInfo, parameters, applications }
                .Where(s => !string.IsNullOrEmpty(s)));
        }

        // Default description if no provider handles this waveform
        return $"The {waveformName} waveform is available as a built-in arbitrary function. " +
               "Use the standard controls to adjust frequency, amplitude, offset, and phase.";
    }

    /// <summary>
    /// Gets basic information about the specified waveform
    /// </summary>
    public static string GetBasicInfo(string waveformName)
    {
        IWaveformDescription provider = GetProviderForWaveform(waveformName);
        return provider != null ? provider.GetBasicInfo(waveformName) :
            $"The {waveformName} waveform is available as a built-in arbitrary function.";
    }

    /// <summary>
    /// Gets parameter information about the specified waveform
    /// </summary>
    public static string GetParameterInfo(string waveformName)
    {
        IWaveformDescription provider = GetProviderForWaveform(waveformName);
        return provider != null ? provider.GetParameterInfo(waveformName) :
            "Use the standard controls to adjust frequency, amplitude, offset, and phase.";
    }

    /// <summary>
    /// Gets application information about the specified waveform
    /// </summary>
    public static string GetApplicationInfo(string waveformName)
    {
        IWaveformDescription provider = GetProviderForWaveform(waveformName);
        return provider != null ? provider.GetApplicationInfo(waveformName) :
            "Common applications include signal processing, testing, and system analysis.";
    }

    /// <summary>
    /// Gets help text for a specific parameter of a waveform
    /// </summary>
    public static string GetParameterHelp(string waveformName, int paramNumber)
    {
        // Find the appropriate provider for this waveform
        IWaveformDescription provider = GetProviderForWaveform(waveformName);

        if (provider != null)
        {
            return provider.GetParameterHelp(waveformName, paramNumber);
        }

        // Default parameter help if no provider handles this waveform
        return $"Parameter {paramNumber}: Adjusts characteristics of the {waveformName} waveform.";
    }

    /// <summary>
    /// Finds the appropriate description provider for a given waveform
    /// </summary>
    private static IWaveformDescription GetProviderForWaveform(string waveformName)
    {
        foreach (var provider in _descriptionProviders)
        {
            if (provider.SupportsWaveform(waveformName))
            {
                return provider;
            }
        }

        return null;
    }
}