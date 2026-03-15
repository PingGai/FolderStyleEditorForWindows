using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Linq;
using System.Security;
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
                        case ElevatedHelperCommand.DeleteDirectory:
                            var deleteResult = DeleteDirectory(request.DirectoryPath);
                            await writer.WriteLineAsync(JsonConvert.SerializeObject(new ElevatedHelperResponse
                            {
                                Ok = deleteResult.IsSuccess,
                                Message = deleteResult.Message,
                                Result = deleteResult
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

        private static FolderStyleMutationResult DeleteDirectory(string? directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return new FolderStyleMutationResult
                {
                    Status = FolderStyleMutationStatus.ValidationFailure,
                    Message = "Directory path is required."
                };
            }

            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    return new FolderStyleMutationResult
                    {
                        Status = FolderStyleMutationStatus.Success,
                        Message = "Directory does not exist."
                    };
                }

                foreach (var filePath in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(filePath, FileAttributes.Normal);
                }

                foreach (var childDirectory in Directory.EnumerateDirectories(directoryPath, "*", SearchOption.AllDirectories)
                    .OrderByDescending(static path => path.Length))
                {
                    File.SetAttributes(childDirectory, FileAttributes.Normal);
                }

                File.SetAttributes(directoryPath, FileAttributes.Normal);
                Directory.Delete(directoryPath, recursive: true);

                return new FolderStyleMutationResult
                {
                    Status = FolderStyleMutationStatus.Success,
                    Message = "Directory deleted."
                };
            }
            catch (UnauthorizedAccessException ex)
            {
                return new FolderStyleMutationResult
                {
                    Status = FolderStyleMutationStatus.AccessDenied,
                    Message = ex.Message,
                    Details = ex.ToString()
                };
            }
            catch (Exception ex) when (ex is IOException or SecurityException)
            {
                return new FolderStyleMutationResult
                {
                    Status = FolderStyleMutationStatus.IoFailure,
                    Message = ex.Message,
                    Details = ex.ToString()
                };
            }
            catch (Exception ex)
            {
                return new FolderStyleMutationResult
                {
                    Status = FolderStyleMutationStatus.UnexpectedError,
                    Message = ex.Message,
                    Details = ex.ToString()
                };
            }
        }
    }
}
