using System.IO;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace OptiSync.Shared.Ipc;

public sealed class AsyncPipeChannel : IAsyncDisposable
{
    private readonly PipeStream _pipeStream;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    private readonly SemaphoreSlim _pipeWriteLock = new(1, 1);

    public AsyncPipeChannel(PipeStream pipeStream)
    {
        _pipeStream = pipeStream;
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        _reader = new StreamReader(_pipeStream, encoding, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
        _writer = new StreamWriter(_pipeStream, encoding, bufferSize: 4096, leaveOpen: true) { AutoFlush = false };
    }

    public bool IsConnected => _pipeStream.IsConnected;

    public Task SendAsync(IpcEnvelope envelope, CancellationToken ct = default) =>
        SendRawAsync(envelope.MessageType, JsonSerializer.Serialize(envelope.Payload, envelope.Payload.GetType(), IpcJson.Options), ct);

    public Task SendAsync<T>(string type, T payload, CancellationToken ct = default) =>
        SendRawAsync(type, JsonSerializer.Serialize(payload, IpcJson.Options), ct);

    public async IAsyncEnumerable<IpcMessage> ReadMessagesAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested && _pipeStream.IsConnected)
        {
            string? line;
            try
            {
                line = await _reader.ReadLineAsync(ct).ConfigureAwait(false);
            }
            catch (IOException)
            {
                yield break;
            }

            if (line is null)
            {
                yield break;
            }

            if (line.Length == 0)
            {
                continue;
            }

            var message = JsonSerializer.Deserialize<IpcMessage>(line, IpcJson.Options);
            if (message is not null)
            {
                yield return message;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _reader.Dispose();
        try
        {
            await _writer.DisposeAsync().ConfigureAwait(false);
        }
        catch (IOException)
        {
        }

        await _pipeStream.DisposeAsync().ConfigureAwait(false);
        _pipeWriteLock.Dispose();
    }

    private async Task SendRawAsync(string type, string jsonPayload, CancellationToken ct)
    {
        string line = JsonSerializer.Serialize(new IpcMessage(type, jsonPayload), IpcJson.Options);

        await _pipeWriteLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _writer.WriteLineAsync(line.AsMemory(), ct).ConfigureAwait(false);
            await _writer.FlushAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _pipeWriteLock.Release();
        }
    }
}
