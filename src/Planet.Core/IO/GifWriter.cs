using System.Text;

namespace Planet.Core.IO;

public sealed class GifWriter : IDisposable
{
    private const int MaxCodes = 4096;

    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private readonly int _paletteBits;
    private bool _disposed;

    public GifWriter(Stream stream, int width, int height, IReadOnlyList<(byte R, byte G, byte B)> palette,
        int delayCentiseconds, bool leaveOpen = false)
    {
        if (width is < 1 or > ushort.MaxValue || height is < 1 or > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(width), "Niepoprawny rozmiar obrazu.");
        if (palette.Count is < 1 or > 256)
            throw new ArgumentOutOfRangeException(nameof(palette), "Paleta musi mieć od 1 do 256 kolorów.");

        _stream = stream;
        _leaveOpen = leaveOpen;
        Width = width;
        Height = height;
        DelayCentiseconds = Math.Clamp(delayCentiseconds, 2, ushort.MaxValue);

        _paletteBits = 1;
        while (1 << _paletteBits < palette.Count)
            _paletteBits++;

        Write(Encoding.ASCII.GetBytes("GIF89a"));
        WriteUInt16(width);
        WriteUInt16(height);
        _stream.WriteByte((byte)(0x80 | ((_paletteBits - 1) << 4) | (_paletteBits - 1)));
        _stream.WriteByte(0);
        _stream.WriteByte(0);
        for (var i = 0; i < 1 << _paletteBits; i++)
        {
            var (r, g, b) = i < palette.Count ? palette[i] : ((byte)0, (byte)0, (byte)0);
            _stream.WriteByte(r);
            _stream.WriteByte(g);
            _stream.WriteByte(b);
        }

        Write([0x21, 0xFF, 0x0B]);
        Write(Encoding.ASCII.GetBytes("NETSCAPE2.0"));
        Write([0x03, 0x01, 0x00, 0x00, 0x00]);
    }

    public int Width { get; }
    public int Height { get; }
    public int DelayCentiseconds { get; }
    public int FrameCount { get; private set; }

    public void AddFrame(ReadOnlySpan<byte> indices)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (indices.Length != Width * Height)
            throw new ArgumentException($"Klatka musi mieć {Width * Height} pikseli.", nameof(indices));

        Write([0x21, 0xF9, 0x04, 0x00]);
        WriteUInt16(DelayCentiseconds);
        Write([0x00, 0x00]);

        _stream.WriteByte(0x2C);
        WriteUInt16(0);
        WriteUInt16(0);
        WriteUInt16(Width);
        WriteUInt16(Height);
        _stream.WriteByte(0);

        var minCodeSize = Math.Max(2, _paletteBits);
        _stream.WriteByte((byte)minCodeSize);
        WriteSubBlocks(Lzw(indices, minCodeSize));
        _stream.WriteByte(0);
        FrameCount++;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _stream.WriteByte(0x3B);
        _stream.Flush();
        if (!_leaveOpen)
            _stream.Dispose();
    }

    internal static byte[] Lzw(ReadOnlySpan<byte> indices, int minCodeSize)
    {
        int clear = 1 << minCodeSize, end = clear + 1;
        var codeSize = minCodeSize + 1;
        var nextCode = end + 1;
        var table = new Dictionary<int, int>();
        var output = new BitWriter();

        output.Write(clear, codeSize);
        if (indices.IsEmpty)
        {
            output.Write(end, codeSize);
            return output.ToArray();
        }

        int prefix = indices[0];
        for (var i = 1; i < indices.Length; i++)
        {
            int k = indices[i];
            var key = (prefix << 8) | k;
            if (table.TryGetValue(key, out var code))
            {
                prefix = code;
                continue;
            }

            output.Write(prefix, codeSize);
            if (nextCode < MaxCodes)
            {
                table[key] = nextCode++;
                if (nextCode > 1 << codeSize && codeSize < 12)
                    codeSize++;
            }
            else
            {
                output.Write(clear, codeSize);
                table.Clear();
                codeSize = minCodeSize + 1;
                nextCode = end + 1;
            }
            prefix = k;
        }

        output.Write(prefix, codeSize);
        output.Write(end, codeSize);
        return output.ToArray();
    }

    private void WriteSubBlocks(byte[] data)
    {
        for (var offset = 0; offset < data.Length; offset += 255)
        {
            var length = Math.Min(255, data.Length - offset);
            _stream.WriteByte((byte)length);
            _stream.Write(data, offset, length);
        }
    }

    private void Write(ReadOnlySpan<byte> bytes) => _stream.Write(bytes);

    private void WriteUInt16(int value)
    {
        _stream.WriteByte((byte)(value & 0xFF));
        _stream.WriteByte((byte)(value >> 8));
    }

    private sealed class BitWriter
    {
        private readonly List<byte> _bytes = [];
        private int _buffer;
        private int _bits;

        public void Write(int code, int size)
        {
            _buffer |= code << _bits;
            _bits += size;
            while (_bits >= 8)
            {
                _bytes.Add((byte)(_buffer & 0xFF));
                _buffer >>= 8;
                _bits -= 8;
            }
        }

        public byte[] ToArray()
        {
            if (_bits > 0)
                _bytes.Add((byte)(_buffer & 0xFF));
            _buffer = _bits = 0;
            return [.. _bytes];
        }
    }
}
