using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia.Media;

namespace FolderStyleEditorForWindows.Services
{
    [SupportedOSPlatform("windows")]
    public sealed class FolderStyleSaveCoordinator
    {
        private readonly FolderStyleMutationEngine _mutationEngine;
        private readonly ElevatedHelperController _helperController;
        private readonly InterruptDialogService _dialogService;
        private readonly IToastService _toastService;
        private readonly HashSet<string> _elevatedFolderCache = new(StringComparer.OrdinalIgnoreCase);

        public FolderStyleSaveCoordinator(
            FolderStyleMutationEngine mutationEngine,
            ElevatedHelperController helperController,
            InterruptDialogService dialogService,
            IToastService toastService)
        {
            _mutationEngine = mutationEngine;
            _helperController = helperController;
            _dialogService = dialogService;
            _toastService = toastService;
        }

        public bool IsElevationSessionActive => _helperController.IsConnected;

        public async Task DisableElevationSessionAsync()
        {
            await _helperController.ResetConnectionAsync();
        }

        public async Task<FolderStyleAccessPreparationOutcome> EnsureElevationSessionAsync()
        {
            if (await EnsureElevationSessionWithPromptAsync())
            {
                return new FolderStyleAccessPreparationOutcome
                {
                    CanContinue = true,
                    RequiresElevation = true
                };
            }

            return new FolderStyleAccessPreparationOutcome
            {
                CanContinue = false,
                ShouldNavigateHome = true,
                RequiresElevation = true
            };
        }

        public async Task<FolderStyleAccessPreparationOutcome> PrepareAccessForFolderAsync(string folderPath)
        {
            if (_helperController.IsConnected || !FolderProtectionPolicy.RequiresElevation(folderPath))
            {
                return new FolderStyleAccessPreparationOutcome
                {
                    CanContinue = true,
                    RequiresElevation = _helperController.IsConnected
                };
            }

            if (await EnsureElevationSessionWithPromptAsync())
            {
                _elevatedFolderCache.Add(folderPath);
                return new FolderStyleAccessPreparationOutcome
                {
                    CanContinue = true,
                    RequiresElevation = true
                };
            }

            return new FolderStyleAccessPreparationOutcome
            {
                CanContinue = false,
                ShouldNavigateHome = true,
                RequiresElevation = true
            };
        }

        public async Task<FolderStyleSaveOutcome> SaveAsync(FolderStyleMutationRequest request)
        {
            var normalizedFolder = request.FolderPath ?? string.Empty;
            var requiresElevation = FolderProtectionPolicy.RequiresElevation(normalizedFolder);

            if (_elevatedFolderCache.Contains(normalizedFolder))
            {
                return await SaveViaHelperAsync(request, addFolderOnSuccess: false);
            }

            if (_helperController.IsConnected && requiresElevation)
            {
                return await SaveViaHelperAsync(request, addFolderOnSuccess: true);
            }

            var localResult = await _mutationEngine.SaveAsync(request);
            if (!localResult.IsAccessDenied)
            {
                return new FolderStyleSaveOutcome { Result = localResult };
            }

            if (ConfigManager.Config.Features.PermissionPrompt.SuppressElevationPrompt)
            {
                return await SaveViaHelperAsync(request, addFolderOnSuccess: true);
            }

            var prompt = await _dialogService.ShowElevationPromptAsync();
            if (prompt.Result != InterruptDialogResult.Primary)
            {
                _toastService.Show(LocalizationManager.Instance["Toast_ElevationDeclined"], new SolidColorBrush(Color.Parse("#D7A85B")));
                return new FolderStyleSaveOutcome
                {
                    Result = new FolderStyleMutationResult
                    {
                        Status = FolderStyleMutationStatus.CanceledByUser,
                        Message = LocalizationManager.Instance["Dialog_Elevation_Declined_Message"]
                    },
                    ShouldNavigateHome = true
                };
            }

            if (prompt.IsCheckboxChecked)
            {
                ConfigManager.Config.Features.PermissionPrompt.SuppressElevationPrompt = true;
                ConfigManager.SaveConfig();
            }

            return await SaveViaHelperAsync(request, addFolderOnSuccess: true);
        }

        private async Task<FolderStyleSaveOutcome> SaveViaHelperAsync(FolderStyleMutationRequest request, bool addFolderOnSuccess)
        {
            var ensure = await _helperController.EnsureConnectedAsync();
            if (!ensure.IsSuccess)
            {
                await _dialogService.ShowFailureAsync(
                    LocalizationManager.Instance["Dialog_Elevation_Title"],
                    LocalizationManager.Instance["Dialog_Elevation_RequiredHeadline"],
                    ensure.Message,
                    ensure.Details);

                return new FolderStyleSaveOutcome { Result = ensure };
            }

            var elevatedResult = await _helperController.SaveAsync(request);
            if (elevatedResult.IsSuccess)
            {
                if (addFolderOnSuccess)
                {
                    _elevatedFolderCache.Add(request.FolderPath);
                }

                return new FolderStyleSaveOutcome { Result = elevatedResult };
            }

            await _dialogService.ShowFailureAsync(
                LocalizationManager.Instance["Dialog_SaveFailed_Title"],
                LocalizationManager.Instance["Dialog_SaveFailed_Headline"],
                elevatedResult.Message,
                elevatedResult.Details);

            return new FolderStyleSaveOutcome { Result = elevatedResult };
        }

        private async Task<bool> EnsureElevationSessionWithPromptAsync()
        {
            if (ConfigManager.Config.Features.PermissionPrompt.SuppressElevationPrompt)
            {
                var directEnsure = await _helperController.EnsureConnectedAsync();
                if (!directEnsure.IsSuccess)
                {
                    await _dialogService.ShowFailureAsync(
                        LocalizationManager.Instance["Dialog_Elevation_Title"],
                        LocalizationManager.Instance["Dialog_Elevation_RequiredHeadline"],
                        directEnsure.Message,
                        directEnsure.Details);
                    return false;
                }

                return true;
            }

            var prompt = await _dialogService.ShowElevationPromptAsync();
            if (prompt.Result != InterruptDialogResult.Primary)
            {
                _toastService.Show(LocalizationManager.Instance["Toast_ElevationDeclined"], new SolidColorBrush(Color.Parse("#D7A85B")));
                return false;
            }

            if (prompt.IsCheckboxChecked)
            {
                ConfigManager.Config.Features.PermissionPrompt.SuppressElevationPrompt = true;
                ConfigManager.SaveConfig();
            }

            var ensure = await _helperController.EnsureConnectedAsync();
            if (!ensure.IsSuccess)
            {
                await _dialogService.ShowFailureAsync(
                    LocalizationManager.Instance["Dialog_Elevation_Title"],
                    LocalizationManager.Instance["Dialog_Elevation_RequiredHeadline"],
                    ensure.Message,
                    ensure.Details);
                return false;
            }

            return true;
        }

    }
}
