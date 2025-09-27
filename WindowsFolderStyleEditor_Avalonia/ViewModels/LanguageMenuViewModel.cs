using System.Collections.ObjectModel;
using System.Windows.Input;
using WindowsFolderStyleEditor_Avalonia.Models;
using WindowsFolderStyleEditor_Avalonia.Services;

namespace WindowsFolderStyleEditor_Avalonia.ViewModels
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