using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Windows;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Infrastructure;

public static class SingleInstanceHandoffService
{
    private const string PipeName = "QubiCostElectrical_SingleInstance_Handoff";
    private static CancellationTokenSource? cancellationTokenSource;
    private static Func<string, Task>? openProjectHandler;
    private static readonly IProjectFileFormatDetector formatDetector = new ProjectFileFormatDetector();

    public static void Start(Func<string, Task> handler)
    {
        openProjectHandler = handler ?? throw new ArgumentNullException(nameof(handler));
        cancellationTokenSource = new CancellationTokenSource();
        _ = Task.Run(() => ListenAsync(cancellationTokenSource.Token));
    }

    public static void Stop()
    {
        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();
        cancellationTokenSource = null;
        openProjectHandler = null;
    }

    public static bool TryExtractOpenProjectPath(string[] args, out string path)
    {
        foreach (var arg in args ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(arg))
                continue;

            if (!File.Exists(arg))
                continue;

            if (formatDetector.Detect(arg) != ProjectFileFormat.Unknown || arg.EndsWith(".qcreq", StringComparison.OrdinalIgnoreCase))
            {
                path = arg;
                return true;
            }
        }

        path = string.Empty;
        return false;
    }

    public static bool TrySendOpenProjectRequest(string path, int timeoutMilliseconds = 1500)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.None);
            client.Connect(timeoutMilliseconds);

            using var writer = new StreamWriter(client, Encoding.UTF8) { AutoFlush = true };
            writer.WriteLine(path);
            writer.Flush();
            client.WaitForPipeDrain();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using var server = new NamedPipeServerStream(
                PipeName,
                PipeDirection.In,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            try
            {
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                using var reader = new StreamReader(server, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: false);
                var path = (await reader.ReadToEndAsync().ConfigureAwait(false)).Trim();
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                var handler = openProjectHandler;
                if (handler == null || Application.Current?.Dispatcher == null)
                    continue;

                var operation = Application.Current.Dispatcher.InvokeAsync(() => handler(path));
                var task = await operation.Task.ConfigureAwait(false);
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                // Best-effort handoff. Errors fall back to bringing the existing window forward.
            }
        }
    }
}
