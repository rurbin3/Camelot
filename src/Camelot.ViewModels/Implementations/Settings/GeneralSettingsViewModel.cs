﻿using Camelot.ViewModels.Implementations.Settings.General;
using Camelot.ViewModels.Interfaces.Settings;

namespace Camelot.ViewModels.Implementations.Settings
{
    public class GeneralSettingsViewModel : ViewModelBase, ISettingsViewModel
    {
        public LanguageSettingsViewModel LanguageSettingsViewModel { get; }

        public GeneralSettingsViewModel(LanguageSettingsViewModel languageSettingsViewModel)
        {
            LanguageSettingsViewModel = languageSettingsViewModel;
        }

        public bool IsChanged => LanguageSettingsViewModel.IsChanged;

        public void Activate()
        {
            LanguageSettingsViewModel.Activate();
        }

        public void SaveChanges()
        {
            LanguageSettingsViewModel.SaveChanges();
        }
    }
}