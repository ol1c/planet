using System.IO;
using Planet.Core.IO;

namespace Planet.App.Services;

public interface IFrameRecorder : IDisposable
{
    string Target { get; }

    int FrameCount { get; }

    void AddFrame(byte[] indices, int width, int height);
}

public sealed class PngSequenceRecorder(string folder) : IFrameRecorder
{
    public string Target { get; } = folder;
    public int FrameCount { get; private set; }

    public void AddFrame(byte[] indices, int width, int height)
    {
        FrameCount++;
        var path = Path.Combine(Target, $"klatka_{FrameCount:D5}.png");
        BoardImages.SavePng(BoardImages.CreateBitmap(indices, width, height), path);
    }

    public void Dispose()
    {
    }
}

public sealed class GifRecorder : IFrameRecorder
{
    private readonly GifWriter _writer;

    public GifRecorder(string path, int width, int height, int delayCentiseconds)
    {
        Target = path;
        _writer = new GifWriter(File.Create(path), width, height, BoardImages.PaletteRgb(), delayCentiseconds);
    }

    public string Target { get; }
    public int FrameCount => _writer.FrameCount;

    public void AddFrame(byte[] indices, int width, int height) => _writer.AddFrame(indices);

    public void Dispose() => _writer.Dispose();
}
