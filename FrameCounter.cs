using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Timers;

namespace Formulatrix.GrabTheFrame;
public interface IFrameCallback
{
    void FrameReceived(IntPtr pFrame, int pixelWidth, int pixelHeight);
}

public interface IValueReporter
{
    void Report(double value);
}

public class FrameProcessor
{
    private readonly IValueReporter _reporter;
    private readonly ConcurrentQueue<Frame> _receivedFrames = new();
    private readonly System.Timers.Timer _timer;

    public FrameProcessor(FrameGrabber fg, IValueReporter reporter)
    {
        fg.OnFrameUpdate += HandleFrameUpdate;  // Subscribe to frame updates
        _reporter = reporter;

        // Set up timer to process frames at 30 FPS
        _timer = new System.Timers.Timer(1000 / 30);
        _timer.Elapsed += OnTimerElapsed;
    }

    private void HandleFrameUpdate(Frame frame)
    {
        _receivedFrames.Enqueue(frame);
    }

    private void OnTimerElapsed(object sender, ElapsedEventArgs e)
    {
        if (_receivedFrames.TryDequeue(out Frame frame))
        {
            byte[] raw = frame.GetRawData();
            int sum = 0;

            // Calculate the arithmetic mean
            foreach (var b in raw)
                sum += b;

            int average = sum / raw.Length;
            _reporter.Report(average);

            frame.Dispose(); // Dispose of the frame after processing
        }
    }

    public void StartStreaming() => _timer.Start();
}

public class FrameGrabber : IFrameCallback
{
    private byte[]? _buffer;
    public event Action<Frame>? OnFrameUpdate;

    public void FrameReceived(IntPtr pFrame, int width, int height)
    {
        int bufferSize = width * height * 4;
        if (_buffer == null || _buffer.Length != bufferSize)
            _buffer = new byte[bufferSize];

        Marshal.Copy(pFrame, _buffer, 0, bufferSize);

        var frameCopy = new byte[bufferSize];
        Array.Copy(_buffer, frameCopy, bufferSize); // Avoid direct reuse of _buffer
        OnFrameUpdate?.Invoke(new Frame(frameCopy));
    }
}

public class Frame(byte[] raw) : IDisposable
{
    private bool _disposed;
    private byte[]? _rawBuffer = raw;

    public byte[]? GetRawData()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Frame), "The buffer has been disposed.");

        return _rawBuffer;
    }

    public void Dispose()
    {
        _disposed = true;
        _rawBuffer = null;
    }
}
