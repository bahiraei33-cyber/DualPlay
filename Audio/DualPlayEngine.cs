using System;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace DualPlay.Audio;

/// <summary>
/// First-pass prototype: Windows plays the system mix to the default output;
/// WASAPI loopback captures that mix and forwards it to the second output.
/// </summary>
public sealed class DualPlayEngine : IDisposable
{
    private readonly MMDevice _source;
    private readonly MMDevice _target;
    private WasapiLoopbackCapture? _capture;
    private WasapiOut? _output;
    private BufferedWaveProvider? _buffer;

    public bool IsRunning { get; private set; }

    public DualPlayEngine(MMDevice source, MMDevice target)
    {
        _source = source;
        _target = target;
    }

    public void Start()
    {
        if (IsRunning) return;

        _capture = new WasapiLoopbackCapture(_source);
        _buffer = new BufferedWaveProvider(_capture.WaveFormat)
        {
            BufferDuration = TimeSpan.FromMilliseconds(600),
            DiscardOnBufferOverflow = true,
            ReadFully = true
        };

        _output = new WasapiOut(_target, AudioClientShareMode.Shared, true, 100);
        _output.Init(_buffer);
        _capture.DataAvailable += (_, e) =>
        {
            if (e.BytesRecorded > 0)
                _buffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
        };
        _capture.RecordingStopped += (_, e) =>
        {
            if (e.Exception is not null)
                throw e.Exception;
        };

        _output.Play();
        _capture.StartRecording();
        IsRunning = true;
    }

    public void Dispose()
    {
        if (!IsRunning && _capture is null && _output is null) return;

        try { _capture?.StopRecording(); } catch { }
        try { _output?.Stop(); } catch { }
        _capture?.Dispose();
        _output?.Dispose();
        _capture = null;
        _output = null;
        _buffer = null;
        IsRunning = false;
    }
}
