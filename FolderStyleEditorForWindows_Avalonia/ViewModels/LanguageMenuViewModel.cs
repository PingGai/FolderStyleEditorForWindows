using System.Collections.ObjectModel;
using System.Windows.Input;
using FolderStyleEditorForWindows.Models;
using FolderStyleEditorForWindows.Services;

namespace FolderStyleEditorForWindows.ViewModels
{
    public class LanguageMenuViewModel
    {
        public ObservableCollection<LanguageInfo> AvailableLanguages => LocalizationManager.Instance.AvailableLanguages;

        public ICommand SwitchLanguageCommand { get; }

        public LanguageMenuViewModel()
        {
            SwitchLanguageCommand = new RelayCommand<LanguageInfo?>(SwitchLanguage);
        }

        private void SwitchLanguage(LanguageInfo? languageInfo)
        {
            if (languageInfo == null) return;
            LocalizationManager.Instance.SwitchLanguage(languageInfo.Culture);
        }
    }
}