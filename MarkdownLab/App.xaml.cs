using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Security.Principal;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MarkdownLab;

public partial class App : Application
{
    private Mutex? instanceMutex;
    private bool ownsMutex;
    private readonly CancellationTokenSource stop = new();
    private string pipeName = "";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var sid = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        var scope = "Sealsworn.MarkdownLab." + sid + "." + Process.GetCurrentProcess().SessionId;
        pipeName = scope;
        var paths = e.Args.Select(TryFullPath).Where(p => p != null).Cast<string>().Take(64).ToArray();
        instanceMutex = new Mutex(false, @"Local\" + scope);
        try { ownsMutex = instanceMutex.WaitOne(0); }
        catch (AbandonedMutexException) { ownsMutex = true; }
        if (!ownsMutex)
        {
            try
            {
                // Let the existing window request foreground activation.
                AllowSetForegroundWindow(-1);
                using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly);
                client.Connect(4000);
                using var writer = new StreamWriter(client, new UTF8Encoding(false));
                writer.WriteLine(JsonSerializer.Serialize(paths));
                writer.Flush();
                Shutdown();
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo contactar con la ventana abierta de MarkdownLab. No se ha abierto una segunda instancia.\n" + ex.Message, "MarkdownLab");
                Shutdown(1);
                return;
            }
        }

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
        window.OpenExternalDocuments(paths);
        _ = ListenAsync(window, stop.Token);
    }

    private static string? TryFullPath(string value)
    {
        try
        {
            var path = Path.GetFullPath(value);
            return path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase) ? path : null;
        }
        catch { return null; }
    }

    private async Task ListenAsync(MainWindow window, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(token).ConfigureAwait(false);
                using var reader = new StreamReader(server, Encoding.UTF8);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(TimeSpan.FromSeconds(5));
                var buffer = new char[4096];
                var message = new StringBuilder();
                int count;
                bool complete = false;
                while ((count = await reader.ReadAsync(buffer.AsMemory(), timeout.Token).ConfigureAwait(false)) > 0)
                {
                    int newline = Array.IndexOf(buffer, '\n', 0, count);
                    message.Append(buffer, 0, newline >= 0 ? newline : count);
                    if (message.Length > 262144) break;
                    if (newline >= 0) { complete = true; break; }
                }
                if (!complete || message.Length > 262144) continue;
                var paths = JsonSerializer.Deserialize<string[]>(message.ToString()) ?? Array.Empty<string>();
                await Dispatcher.InvokeAsync(() => window.OpenExternalDocuments(paths.Take(64)), System.Windows.Threading.DispatcherPriority.Normal, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (Exception) when (!token.IsCancellationRequested)
            {
                try { await Task.Delay(100, token).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
            }
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        stop.Cancel();
        if (ownsMutex) instanceMutex?.ReleaseMutex();
        instanceMutex?.Dispose();
        base.OnExit(e);
    }

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int processId);
}
