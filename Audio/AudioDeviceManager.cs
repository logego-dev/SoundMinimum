using NAudio.Wave;

namespace SoundMinimum.Audio;

public class AudioDeviceManager
{
    public List<string> GetDevices()
    {
        var devices = new List<string>();
        for (int i = 0; i < WaveOut.DeviceCount; i++)
            devices.Add(WaveOut.GetCapabilities(i).ProductName);
        return devices;
    }

    public int GetDeviceId(string? deviceName)
    {
        if (string.IsNullOrEmpty(deviceName)) return -1;
        for (int i = 0; i < WaveOut.DeviceCount; i++)
            if (WaveOut.GetCapabilities(i).ProductName == deviceName)
                return i;
        return -1;
    }
}
