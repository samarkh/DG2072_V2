using System;

namespace DG2072_USB_Control.Continuous.ArbitraryWaveform.Descriptions
{
    public interface IWaveformDescription
    {
        bool SupportsWaveform(string waveformName);
        string GetBasicInfo(string waveformName);
        string GetParameterInfo(string waveformName);
        string GetApplicationInfo(string waveformName);
        string GetParameterHelp(string waveformName, int paramNumber);
    }
}