using DG2072_USB_Control;
using DG2072_USB_Control.Continuous.Noise;
using DG2072_USB_Control.Services;
using System;
using System.Windows;

public class NoiseGen : WaveformGenerator, INoiseEventHandler
{
    public NoiseGen(RigolDG2072 device, int channel, Window mainWindow)
        : base(device, channel, mainWindow)
    {
    }

    /// <summary>
    /// Apply noise waveform parameters
    /// </summary>
    public override void ApplyParameters()
    {
        if (!IsDeviceConnected()) return;

        try
        {
            // Get current parameters
            double amplitude = GetAmplitudeFromUI();
            double offset = GetOffsetFromUI();

            // Apply noise waveform with current amplitude and offset
            Device.SendCommand($":SOURCE{ActiveChannel}:APPLY:NOIS {amplitude},{offset}");
            Log($"Applied Noise waveform to CH{ActiveChannel} with " +
                $"Amp={UnitConversionUtility.FormatWithMinimumDecimals(amplitude)}Vpp, " +
                $"Offset={UnitConversionUtility.FormatWithMinimumDecimals(offset)}V");
        }
        catch (Exception ex)
        {
            Log($"Error applying noise parameters: {ex.Message}");
        }
    }

    /// <summary>
    /// Refresh the noise parameters from the device
    /// </summary>
    public override void RefreshParameters()
    {
        if (!IsDeviceConnected()) return;

        try
        {
            // For Noise waveforms, we only have amplitude and offset parameters
            // which are already handled by MainWindow.
            Log($"Refreshed Noise parameters for CH{ActiveChannel}");
        }
        catch (Exception ex)
        {
            Log($"Error refreshing noise parameters: {ex.Message}");
        }
    }
}