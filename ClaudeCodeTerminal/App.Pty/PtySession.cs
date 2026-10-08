using Porta.Pty;

namespace ClaudeCodeTerminal.App.Pty;

public sealed class PtySession : IDisposable
{
    private readonly IPtyConnection _connection;
    private readonly object _writeGate = new();
    private readonly object _exitGate = new();
    private readonly CancellationTokenSource _readLoopCts = new();
    private Task? _readLoopTask;
    private bool _disposed;
    private bool _readingStarted;
    private int? _exitCode;
    private EventHandler<int>? _processExited;

    public event EventHandler<int>? ProcessExited
    {
        add
        {
            bool alreadyExited;
            int code;
            lock (_exitGate)
            {
                alreadyExited = _exitCode.HasValue;
                code = _exitCode ?? 0;
                if (!alreadyExited)
                    _processExited += value;
            }

            if (alreadyExited)
                value?.Invoke(this, code);
        }
        remove
        {
            lock (_exitGate)
            {
                _processExited -= value;
            }
        }
    }

    public event EventHandler<ReadOnlyMemory<byte>>? DataReceived;

    private PtySession(IPtyConnection connection)
    {
        _connection = connection;
        _connection.ProcessExited += OnProcessExited;
    }

    public static async Task<PtySession> StartAsync(
        string executable,
        IReadOnlyList<string> args,
        string workingDirectory,
        int rows,
        int cols,
        CancellationToken cancellationToken = default)
    {
        var options = new PtyOptions
        {
            Name = "xterm-256color",
            App = executable,
            CommandLine = args.ToArray(),
            Cwd = workingDirectory,
            Rows = rows,
            Cols = cols,
        };

        var connection = await PtyProvider.SpawnAsync(options, cancellationToken).ConfigureAwait(false);
        return new PtySession(connection);
    }

    public void BeginReading()
    {
        if (_readingStarted)
            return;
        _readingStarted = true;

        StartReadLoop();
    }

    private void StartReadLoop()
    {
        _readLoopTask = Task.Run(async () =>
        {
            var buffer = new byte[4096];
            try
            {
                while (!_readLoopCts.IsCancellationRequested)
                {
                    var bytesRead = await _connection.ReaderStream
                        .ReadAsync(buffer.AsMemory(), _readLoopCts.Token)
                        .ConfigureAwait(false);

                    if (bytesRead == 0)
                        break;

                    DataReceived?.Invoke(this, new ReadOnlyMemory<byte>(buffer, 0, bytesRead));
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (IOException)
            {
            }
        }, _readLoopCts.Token);
    }

    private void OnProcessExited(object? sender, PtyExitedEventArgs e)
    {
        EventHandler<int>? handlers;
        lock (_exitGate)
        {
            _exitCode = e.ExitCode;
            handlers = _processExited;
            _processExited = null;
        }

        handlers?.Invoke(this, e.ExitCode);
    }

    public void Resize(int rows, int cols)
    {
        _connection.Resize(cols, rows);
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        if (_disposed)
            return;

        lock (_writeGate)
        {
            _connection.WriterStream.Write(data);
            _connection.WriterStream.Flush();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _connection.ProcessExited -= OnProcessExited;
        _readLoopCts.Cancel();

        try
        {
            _connection.Dispose();
        }
        finally
        {
            _readLoopCts.Dispose();
        }
    }
}
