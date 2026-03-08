using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Newtonsoft.Json;

namespace FolderStyleEditorForWindows.Services
{
    [SupportedOSPlatform("windows")]
    public sealed class ElevatedHelperController : IAsyncDisposable
    {
        private readonly SemaphoreSlim _sync = new(1, 1);
        private readonly IToastService _toastService;
        private readonly ElevationSessionState _elevationSessionState;
        private NamedPipeServerStream? _pipeServer;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private Process? _helperProcess;
        private string? _pipeName;
        private string? _token;

        public ElevatedHelperController(IToastService toastService, ElevationSessionState elevationSessionState)
        {
            _toastService = toastService;
            _elevationSessionState = elevationSessionState;
        }

        public bool IsConnected => _pipeServer is { IsConnected: true };

        public async Task<FolderStyleMutationResult> EnsureConnectedAsync()
        {
            await _sync.WaitAsync();
            try
            {
                if (IsConnected)
                {
                    return new FolderStyleMutationResult
                    {
                        Status = FolderStyleMutationStatus.Success,
                        Message = "Connected"
                    };
                }

                await DisposeConnectionAsync();

                _pipeName = $"FolderStyleEditorForWindows-{Environment.ProcessId}-{Guid.NewGuid():N}";
                _token = Guid.NewGuid().ToString("N");

                _pipeServer = CreateServer(_pipeName);

                var processPath = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(processPath) || !File.Exists(processPath))
                {
                    return new FolderStyleMutationResult
                    {
                        Status = FolderStyleMutationStatus.UnexpectedError,
                        Message = "无法定位当前程序路径。"
                    };
                }

                try
                {
                    _helperProcess = Process.Start(new ProcessStartInfo
                    {
                        FileName = processPath,
                        Arguments = $"--elevated-helper --pipe-name \"{_pipeName}\" --session-token \"{_token}\" --parent-pid {Environment.ProcessId}",
                        UseShellExecute = true,
                        Verb = "runas",
                        WorkingDirectory = AppContext.BaseDirectory
                    });
                }
                catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
                {
                    await DisposeConnectionAsync();
                    return new FolderStyleMutationResult
                    {
                        Status = FolderStyleMutationStatus.CanceledByUser,
                        Message = LocalizationManager.Instance["Dialog_Elevation_Canceled_Message"],
                        Details = ex.ToString()
                    };
                }

                if (_helperProcess == null)
                {
                    await DisposeConnectionAsync();
                    return new FolderStyleMutationResult
                    {
                        Status = FolderStyleMutationStatus.UnexpectedError,
                        Message = LocalizationManager.Instance["Dialog_Elevation_StartFailed_Message"]
                    };
                }

                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                await _pipeServer.WaitForConnectionAsync(timeoutCts.Token);

                _reader = new StreamReader(_pipeServer, Encoding.UTF8, false, 4096, leaveOpen: true);
                _writer = new StreamWriter(_pipeServer, new UTF8Encoding(false), 4096, leaveOpen: true)
                {
                    AutoFlush = true
                };

                var handshake = await _reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(handshake))
                {
                    await DisposeConnectionAsync();
                    return new FolderStyleMutationResult
                    {
                        Status = FolderStyleMutationStatus.UnexpectedError,
                        Message = LocalizationManager.Instance["Dialog_Elevation_ConnectFailed_Message"]
                    };
                }

                _elevationSessionState.IsElevatedSessionActive = true;
                _toastService.Show(LocalizationManager.Instance["Toast_ElevationSuccess"], new SolidColorBrush(Color.Parse("#F0B24A")));

                return new FolderStyleMutationResult
                {
                    Status = FolderStyleMutationStatus.Success,
                    Message = "Connected"
                };
            }
            catch (OperationCanceledException ex)
            {
                await DisposeConnectionAsync();
                return new FolderStyleMutationResult
                {
                    Status = FolderStyleMutationStatus.UnexpectedError,
                    Message = LocalizationManager.Instance["Dialog_Elevation_ConnectTimeout_Message"],
                    Details = ex.ToString()
                };
            }
            catch (Exception ex)
            {
                await DisposeConnectionAsync();
                return new FolderStyleMutationResult
                {
                    Status = FolderStyleMutationStatus.UnexpectedError,
                    Message = LocalizationManager.Instance["Dialog_Elevation_StartFailed_Message"],
                    Details = ex.ToString()
                };
            }
            finally
            {
                _sync.Release();
            }
        }

        public async Task<FolderStyleMutationResult> SaveAsync(FolderStyleMutationRequest request)
        {
            await _sync.WaitAsync();
            try
            {
                if (!IsConnected || _writer == null || _reader == null || string.IsNullOrWhiteSpace(_token))
                {
                    return new FolderStyleMutationResult
                    {
                        Status = FolderStyleMutationStatus.UnexpectedError,
                        Message = LocalizationManager.Instance["Dialog_Elevation_ConnectFailed_Message"]
                    };
                }

                var payload = new ElevatedHelperRequest
                {
                    Command = ElevatedHelperCommand.SaveFolderStyle,
                    Token = _token,
                    ProtocolVersion = 1,
                    Payload = request
                };

                await _writer.WriteLineAsync(JsonConvert.SerializeObject(payload));
                var responseLine = await _reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(responseLine))
                {
                    await DisposeConnectionAsync();
                    return new FolderStyleMutationResult
                    {
                        Status = FolderStyleMutationStatus.UnexpectedError,
                        Message = LocalizationManager.Instance["Dialog_Elevation_ConnectFailed_Message"]
                    };
                }

                var response = JsonConvert.DeserializeObject<ElevatedHelperResponse>(responseLine);
                if (response?.Result == null)
                {
                    return new FolderStyleMutationResult
                    {
                        Status = FolderStyleMutationStatus.UnexpectedError,
                        Message = response?.Message ?? LocalizationManager.Instance["Dialog_Elevation_ConnectFailed_Message"]
                    };
                }

                return response.Result;
            }
            catch (Exception ex)
            {
                await DisposeConnectionAsync();
                return new FolderStyleMutationResult
                {
                    Status = FolderStyleMutationStatus.UnexpectedError,
                    Message = LocalizationManager.Instance["Dialog_Elevation_ConnectFailed_Message"],
                    Details = ex.ToString()
                };
            }
            finally
            {
                _sync.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await DisposeConnectionAsync();
            _sync.Dispose();
        }

        public async Task ResetConnectionAsync()
        {
            await _sync.WaitAsync();
            try
            {
                await DisposeConnectionAsync();
            }
            finally
            {
                _sync.Release();
            }
        }

        private NamedPipeServerStream CreateServer(string pipeName)
        {
            var security = new PipeSecurity();
            var sid = WindowsIdentity.GetCurrent().User;
            if (sid != null)
            {
                security.AddAccessRule(new PipeAccessRule(sid, PipeAccessRights.FullControl, System.Security.AccessControl.AccessControlType.Allow));
            }

            return NamedPipeServerStreamAcl.Create(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                4096,
                4096,
                security);
        }

        private async Task DisposeConnectionAsync()
        {
            _elevationSessionState.IsElevatedSessionActive = false;

            if (_writer != null && IsConnected && !string.IsNullOrWhiteSpace(_token))
            {
                try
                {
                    await _writer.WriteLineAsync(JsonConvert.SerializeObject(new ElevatedHelperRequest
                    {
                        Command = ElevatedHelperCommand.Shutdown,
                        Token = _token,
                        ProtocolVersion = 1
                    }));
                }
                catch
                {
                }
            }

            _reader?.Dispose();
            _writer?.Dispose();
            _pipeServer?.Dispose();
            _reader = null;
            _writer = null;
            _pipeServer = null;
            _pipeName = null;
            _token = null;
            _helperProcess = null;
        }
    }
}
