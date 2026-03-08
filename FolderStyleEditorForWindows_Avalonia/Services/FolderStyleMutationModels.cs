using System;

namespace FolderStyleEditorForWindows.Services
{
    public enum FolderStyleMutationStatus
    {
        Success,
        AccessDenied,
        CanceledByUser,
        IoFailure,
        ShellFailure,
        ValidationFailure,
        UnexpectedError
    }

    public sealed class FolderStyleMutationRequest
    {
        public string FolderPath { get; init; } = string.Empty;
        public string Alias { get; init; } = string.Empty;
        public bool IsAliasPlaceholder { get; init; }
        public string IconPath { get; init; } = string.Empty;
    }

    public sealed class FolderStyleMutationResult
    {
        public FolderStyleMutationStatus Status { get; init; }
        public string Message { get; init; } = string.Empty;
        public string? Details { get; init; }
        public bool HistoryShouldBeWritten { get; init; }

        public bool IsSuccess => Status == FolderStyleMutationStatus.Success;
        public bool IsAccessDenied => Status == FolderStyleMutationStatus.AccessDenied;
    }

    public sealed class FolderStyleSaveOutcome
    {
        public FolderStyleMutationResult Result { get; init; } = new()
        {
            Status = FolderStyleMutationStatus.UnexpectedError,
            Message = "Unexpected result."
        };

        public bool ShouldNavigateHome { get; init; }
    }

    public sealed class FolderStyleAccessPreparationOutcome
    {
        public bool CanContinue { get; init; }
        public bool ShouldNavigateHome { get; init; }
        public bool RequiresElevation { get; init; }
    }

    public sealed class FolderStyleMutationException : Exception
    {
        public FolderStyleMutationStatus Status { get; }

        public FolderStyleMutationException(FolderStyleMutationStatus status, string message, Exception? innerException = null)
            : base(message, innerException)
        {
            Status = status;
        }
    }
}
