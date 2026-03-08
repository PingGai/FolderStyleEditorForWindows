using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Versioning;
using System.Security;
using System.Threading.Tasks;

namespace FolderStyleEditorForWindows.Services
{
    [SupportedOSPlatform("windows")]
    public sealed class FolderStyleMutationEngine
    {
        public async Task<FolderStyleMutationResult> SaveAsync(FolderStyleMutationRequest request)
        {
            DateTime? originalLastWriteTimeUtc = null;
            try
            {
                if (string.IsNullOrWhiteSpace(request.FolderPath) || !Directory.Exists(request.FolderPath))
                {
                    return new FolderStyleMutationResult
                    {
                        Status = FolderStyleMutationStatus.ValidationFailure,
                        Message = LocalizationManager.Instance["Edit_Directory_Invalid"],
                        Details = request.FolderPath
                    };
                }

                originalLastWriteTimeUtc = Directory.GetLastWriteTimeUtc(request.FolderPath);

                DesktopIniHelper.WriteValue(
                    request.FolderPath,
                    "LocalizedResourceName",
                    request.IsAliasPlaceholder ? string.Empty : request.Alias);

                if (!string.IsNullOrWhiteSpace(request.IconPath))
                {
                    var iconSettings = await PathHelper.ProcessIconPathAsync(request.FolderPath, request.IconPath);

                    DesktopIniHelper.WriteValue(request.FolderPath, "IconResource", null);
                    DesktopIniHelper.WriteValue(request.FolderPath, "IconFile", null);
                    DesktopIniHelper.WriteValue(request.FolderPath, "IconIndex", null);

                    var finalIconPathForRefresh = string.Empty;
                    foreach (var (key, value) in iconSettings)
                    {
                        DesktopIniHelper.WriteValue(request.FolderPath, key, value);
                        if (key == "IconResource" || key == "IconFile")
                        {
                            finalIconPathForRefresh = value;
                        }
                    }

                    ShellHelper.SetFolderIcon(request.FolderPath, finalIconPathForRefresh);
                }
                else
                {
                    DesktopIniHelper.WriteValue(request.FolderPath, "IconResource", null);
                    DesktopIniHelper.WriteValue(request.FolderPath, "IconFile", null);
                    DesktopIniHelper.WriteValue(request.FolderPath, "IconIndex", null);
                    ShellHelper.RemoveFolderIcon(request.FolderPath);
                }

                RestoreFolderTimestamp(request.FolderPath, originalLastWriteTimeUtc);

                return new FolderStyleMutationResult
                {
                    Status = FolderStyleMutationStatus.Success,
                    Message = LocalizationManager.Instance["Toast_SaveSuccess"],
                    HistoryShouldBeWritten = true
                };
            }
            catch (UnauthorizedAccessException ex)
            {
                return AccessDenied(ex);
            }
            catch (SecurityException ex)
            {
                return AccessDenied(ex);
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 5)
            {
                return AccessDenied(ex);
            }
            catch (FolderStyleMutationException ex)
            {
                return new FolderStyleMutationResult
                {
                    Status = ex.Status,
                    Message = ex.Message,
                    Details = ex.ToString()
                };
            }
            catch (IOException ex)
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

        private static FolderStyleMutationResult AccessDenied(Exception ex)
        {
            return new FolderStyleMutationResult
            {
                Status = FolderStyleMutationStatus.AccessDenied,
                Message = LocalizationManager.Instance["Error_AdminRequired"],
                Details = ex.ToString()
            };
        }

        private static void RestoreFolderTimestamp(string folderPath, DateTime? originalLastWriteTimeUtc)
        {
            if (originalLastWriteTimeUtc == null)
            {
                return;
            }

            try
            {
                Directory.SetLastWriteTimeUtc(folderPath, originalLastWriteTimeUtc.Value);
            }
            catch
            {
                // 保持非阻塞。目录时间戳恢复失败不应覆盖核心保存结果。
            }
        }
    }
}
