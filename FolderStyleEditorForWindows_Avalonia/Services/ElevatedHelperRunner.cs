using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace FolderStyleEditorForWindows.Services
{
    [SupportedOSPlatform("windows")]
    public static class ElevatedHelperRunner
    {
        public static async Task<int> RunAsync(string pipeName, string token, int parentPid)
        {
            try
            {
                using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await pipe.ConnectAsync(10000);

                using var reader = new StreamReader(pipe, Encoding.UTF8, false, 4096, leaveOpen: true);
                using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, leaveOpen: true)
                {
                    AutoFlush = true
                };

                var engine = new FolderStyleMutationEngine();

                await writer.WriteLineAsync(JsonConvert.SerializeObject(new ElevatedHelperResponse
                {
                    Ok = true,
                    Message = "connected"
                }));

                while (pipe.IsConnected)
                {
                    if (!IsParentAlive(parentPid))
                    {
                        break;
                    }

                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        break;
                    }

                    var request = JsonConvert.DeserializeObject<ElevatedHelperRequest>(line);
                    if (request == null || request.Token != token || request.ProtocolVersion != 1)
                    {
                        await writer.WriteLineAsync(JsonConvert.SerializeObject(new ElevatedHelperResponse
                        {
                            Ok = false,
                            Message = "invalid-request"
                        }));
                        continue;
                    }

                    switch (request.Command)
                    {
                        case ElevatedHelperCommand.Ping:
                            await writer.WriteLineAsync(JsonConvert.SerializeObject(new ElevatedHelperResponse
                            {
                                Ok = true,
                                Message = "pong"
                            }));
                            break;
                        case ElevatedHelperCommand.Shutdown:
                            await writer.WriteLineAsync(JsonConvert.SerializeObject(new ElevatedHelperResponse
                            {
                                Ok = true,
                                Message = "bye"
                            }));
                            return 0;
                        case ElevatedHelperCommand.SaveFolderStyle:
                            var result = await engine.SaveAsync(request.Payload ?? new FolderStyleMutationRequest());
                            await writer.WriteLineAsync(JsonConvert.SerializeObject(new ElevatedHelperResponse
                            {
                                Ok = result.IsSuccess,
                                Message = result.Message,
                                Result = result
                            }));
                            break;
                        default:
                            await writer.WriteLineAsync(JsonConvert.SerializeObject(new ElevatedHelperResponse
                            {
                                Ok = false,
                                Message = "unsupported-command"
                            }));
                            break;
                    }
                }

                return 0;
            }
            catch
            {
                return 1;
            }
        }

        private static bool IsParentAlive(int parentPid)
        {
            try
            {
                var process = Process.GetProcessById(parentPid);
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }
    }
}
