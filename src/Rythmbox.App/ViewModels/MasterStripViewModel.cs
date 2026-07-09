using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.Core.Engine;
using SoundFlow.Structs;

namespace Rythmbox.App.ViewModels;

public sealed partial class MasterStripViewModel : ViewModelBase, IDisposable
{
    private readonly PlaybackEngine _engine;
    private readonly DispatcherTimer _meterTimer;
    private bool _suppressDeviceChange;

    public MasterStripViewModel(PlaybackEngine engine)
    {
        _engine = engine;
        RefreshDevices();

        _meterTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _meterTimer.Tick += (_, _) => PollLevels();
        _meterTimer.Start();
    }

    public ObservableCollection<DeviceInfo> OutputDevices { get; } = new();

    [ObservableProperty]
    private DeviceInfo? _selectedOutputDevice;

    [ObservableProperty]
    private double _masterVolume = 1.0;

    [ObservableProperty]
    private double _rmsLevel;

    [ObservableProperty]
    private double _peakLevel;

    partial void OnMasterVolumeChanged(double value) => _engine.MasterMixer.Volume = (float)value;

    partial void OnSelectedOutputDeviceChanged(DeviceInfo? value)
    {
        if (_suppressDeviceChange || value is not { } device)
        {
            return;
        }

        if (_engine.IsRunning
            && _engine.CurrentDevice is { } active
            && string.Equals(active.Name, device.Name, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _engine.SetOutputDevice(device);
    }

    [RelayCommand]
    private void RefreshDevices()
    {
        _engine.RefreshDevices();

        _suppressDeviceChange = true;
        OutputDevices.Clear();
        foreach (var device in _engine.PlaybackDevices)
        {
            OutputDevices.Add(device);
        }

        SelectedOutputDevice = _engine.CurrentDevice ?? OutputDevices.FirstOrDefault();
        _suppressDeviceChange = false;
    }

    private void PollLevels()
    {
        if (!_engine.IsRunning)
        {
            return;
        }

        RmsLevel = Math.Clamp(_engine.MasterLevelMeter.Rms, 0f, 1f);
        PeakLevel = Math.Clamp(_engine.MasterLevelMeter.Peak, 0f, 1f);
    }

    public void Dispose() => _meterTimer.Stop();
}
