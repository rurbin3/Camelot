using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Camelot.DataAccess.Models;
using Camelot.Extensions;
using Camelot.Services.Abstractions;
using Camelot.ViewModels.Configuration;
using Camelot.ViewModels.Factories.Interfaces;
using Camelot.ViewModels.Interfaces.MainWindow.FilePanels;
using ReactiveUI;

namespace Camelot.ViewModels.Implementations.MainWindow.FilePanels
{
    public class TabsListViewModel : ViewModelBase, ITabsListViewModel
    {
        private readonly IFilesPanelStateService _filesPanelStateService;
        private readonly IDirectoryService _directoryService;
        private readonly ITabViewModelFactory _tabViewModelFactory;
        private readonly ObservableCollection<ITabViewModel> _tabs;

        private ITabViewModel _selectedTab;

        public IEnumerable<ITabViewModel> Tabs => _tabs;

        public ITabViewModel SelectedTab
        {
            get => _selectedTab;
            set => this.RaiseAndSetIfChanged(ref _selectedTab, value);
        }

        public event EventHandler<EventArgs> SelectedTabChanged;

        public TabsListViewModel(
            IFilesPanelStateService filesPanelStateService,
            IDirectoryService directoryService,
            ITabViewModelFactory tabViewModelFactory,
            FilePanelConfiguration filePanelConfiguration)
        {
            _filesPanelStateService = filesPanelStateService;
            _directoryService = directoryService;
            _tabViewModelFactory = tabViewModelFactory;

            _tabs = SetupTabs();

            this.WhenAnyValue(x => x.SelectedTab.CurrentDirectory, x => x.SelectedTab)
                .Throttle(TimeSpan.FromMilliseconds(filePanelConfiguration.SaveTimeoutMs))
                .Subscribe(_ => SaveState());
        }

        public void CreateNewTab() => CreateNewTab(SelectedTab);

        public void CloseActiveTab() => CloseTab(SelectedTab);

        public void SaveState() =>
            Task.Factory.StartNew(() =>
            {
                var tabs = _tabs.Select(CreateFrom).ToList();
                var selectedTabIndex = _tabs.IndexOf(_selectedTab);
                var state = new PanelModel
                {
                    Tabs = tabs,
                    SelectedTabIndex = selectedTabIndex
                };

                _filesPanelStateService.SavePanelState(state);
            }, TaskCreationOptions.LongRunning);

        private ObservableCollection<ITabViewModel> SetupTabs()
        {
            var state = _filesPanelStateService.GetPanelState();
            if (!state.Tabs.Any())
            {
                state.Tabs = GetDefaultTabs();
            }

            var tabs = GetInitialTabs(state.Tabs);
            tabs.CollectionChanged += TabsOnCollectionChanged;

            var index = state.SelectedTabIndex >= tabs.Count ? ^1 : state.SelectedTabIndex;
            SelectTab(tabs[index]);

            return tabs;
        }

        private void SelectTab(ITabViewModel tabViewModel)
        {
            if (SelectedTab != null)
            {
                SelectedTab.IsActive = SelectedTab.IsGloballyActive = false;
            }

            tabViewModel.IsActive = tabViewModel.IsGloballyActive = true;
            SelectedTab = tabViewModel;

            SelectedTabChanged.Raise(this, EventArgs.Empty);
        }

        private ObservableCollection<ITabViewModel> GetInitialTabs(IEnumerable<TabModel> tabModels)
        {
            var tabs = tabModels
                .Where(tm => _directoryService.CheckIfExists(tm.Directory))
                .ToList();
            if (!tabs.Any())
            {
                tabs = GetDefaultTabs();
            }

            return new ObservableCollection<ITabViewModel>(tabs.Select(CreateViewModelFrom));
        }

        private List<TabModel> GetDefaultTabs()
        {
            var rootDirectoryTab = new TabModel
            {
                Directory = _directoryService.GetAppRootDirectory()
            };

            return new List<TabModel> {rootDirectoryTab};
        }

        private ITabViewModel CreateViewModelFrom(string directory)
        {
            var tabModel = new TabModel
            {
                Directory = directory,
                SortingSettings = GetSortingSettings(SelectedTab)
            };

            return CreateViewModelFrom(tabModel);
        }

        private ITabViewModel CreateViewModelFrom(TabModel tabModel)
        {
            var tabViewModel = _tabViewModelFactory.Create(tabModel);
            SubscribeToEvents(tabViewModel);

            return tabViewModel;
        }

        private void Remove(ITabViewModel tabViewModel)
        {
            UnsubscribeFromEvents(tabViewModel);

            _tabs.Remove(tabViewModel);

            if (SelectedTab == tabViewModel)
            {
                SelectTab(_tabs.First());
            }
        }

        private void SubscribeToEvents(ITabViewModel tabViewModel)
        {
            tabViewModel.ActivationRequested += TabViewModelOnActivationRequested;
            tabViewModel.NewTabRequested += TabViewModelOnNewTabRequested;
            tabViewModel.CloseRequested += TabViewModelOnCloseRequested;
            tabViewModel.ClosingTabsToTheLeftRequested += TabViewModelOnClosingTabsToTheLeftRequested;
            tabViewModel.ClosingTabsToTheRightRequested += TabViewModelOnClosingTabsToTheRightRequested;
            tabViewModel.ClosingAllTabsButThisRequested += TabViewModelOnClosingAllTabsButThisRequested;
        }

        private void UnsubscribeFromEvents(ITabViewModel tabViewModel)
        {
            tabViewModel.ActivationRequested -= TabViewModelOnActivationRequested;
            tabViewModel.NewTabRequested -= TabViewModelOnNewTabRequested;
            tabViewModel.CloseRequested -= TabViewModelOnCloseRequested;
            tabViewModel.ClosingTabsToTheLeftRequested -= TabViewModelOnClosingTabsToTheLeftRequested;
            tabViewModel.ClosingTabsToTheRightRequested -= TabViewModelOnClosingTabsToTheRightRequested;
            tabViewModel.ClosingAllTabsButThisRequested -= TabViewModelOnClosingAllTabsButThisRequested;
        }

        private void TabViewModelOnActivationRequested(object sender, EventArgs e) => SelectTab((ITabViewModel) sender);

        private void TabViewModelOnNewTabRequested(object sender, EventArgs e) => CreateNewTab((ITabViewModel) sender);

        private void TabViewModelOnCloseRequested(object sender, EventArgs e) => CloseTab((ITabViewModel) sender);

        private void TabViewModelOnClosingTabsToTheLeftRequested(object sender, EventArgs e)
        {
            var tabViewModel = (ITabViewModel) sender;
            var tabPosition = _tabs.IndexOf(tabViewModel);
            var tabsToClose = _tabs.Take(tabPosition).ToArray();

            tabsToClose.ForEach(Remove);
        }

        private void TabViewModelOnClosingTabsToTheRightRequested(object sender, EventArgs e)
        {
            var tabViewModel = (ITabViewModel) sender;
            var tabPosition = _tabs.IndexOf(tabViewModel);
            var tabsToClose = _tabs.Skip(tabPosition + 1).ToArray();

            tabsToClose.ForEach(Remove);
        }

        private void TabsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) => SaveState();

        private void TabViewModelOnClosingAllTabsButThisRequested(object sender, EventArgs e)
        {
            var tabViewModel = (ITabViewModel) sender;
            var tabsToClose = _tabs.Where(t => t != tabViewModel).ToArray();

            tabsToClose.ForEach(Remove);
        }

        private void CreateNewTab(ITabViewModel tabViewModel)
        {
            var tabPosition = _tabs.IndexOf(tabViewModel);
            var newTabViewModel = CreateViewModelFrom(tabViewModel.CurrentDirectory);

            _tabs.Insert(tabPosition, newTabViewModel);
        }

        private void CloseTab(ITabViewModel tabViewModel)
        {
            if (_tabs.Count > 1)
            {
                Remove(tabViewModel);
            }
        }

        private static TabModel CreateFrom(ITabViewModel tabViewModel) =>
            new TabModel
            {
                Directory = tabViewModel.CurrentDirectory,
                SortingSettings = GetSortingSettings(tabViewModel)
            };

        private static SortingSettings GetSortingSettings(ITabViewModel tabViewModel) =>
            new SortingSettings
            {
                IsAscending = tabViewModel.SortingViewModel.IsSortingByAscendingEnabled,
                SortingMode = (int) tabViewModel.SortingViewModel.SortingColumn
            };
    }
}