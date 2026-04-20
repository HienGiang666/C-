using System.ComponentModel;
using System.Runtime.CompilerServices;
using TourApp.Mobile.Services;

namespace TourApp.Mobile.Services
{
    public class LocalizationResourceManager : INotifyPropertyChanged
    {
        private static LocalizationResourceManager? _instance;
        public static LocalizationResourceManager Instance => _instance ??= new LocalizationResourceManager();

        public event PropertyChangedEventHandler? PropertyChanged;

        private LocalizationResourceManager()
        {
            LanguageService.LanguageChanged += (s, e) =>
            {
                // Raise for Item[] indexer and null to refresh all bindings
                OnPropertyChanged("Item[]");
                OnPropertyChanged(null);
            };
        }

        public string this[string text] => LanguageService.GetString(text);

        public void OnPropertyChanged(string? propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
