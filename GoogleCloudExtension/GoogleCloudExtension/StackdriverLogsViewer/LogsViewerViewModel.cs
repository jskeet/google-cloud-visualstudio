// Copyright 2016 Google Inc. All Rights Reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Google.Apis.Logging.v2.Data;
using GoogleCloudExtension.DataSources;
using GoogleCloudExtension.Accounts;
using GoogleCloudExtension.Utils;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System;
using System.Threading.Tasks;

using System.Diagnostics;

namespace GoogleCloudExtension.StackdriverLogsViewer
{
    internal enum LogSeverity
    {
        DEBUG,
        INFO,
        WARNING,
        ERROR,
        EMERGENCY,
    }

    internal class LogItem
    {
        private const string AnyIconPath = "StackdriverLogsViewer/Resources/ic_log_level_any.png";
        private const string DebugIconPath = "StackdriverLogsViewer/Resources/ic_log_level_debug.png";
        private const string ErrorIconPath = "StackdriverLogsViewer/Resources/ic_log_level_error.png";
        private const string FatalIconPath = "StackdriverLogsViewer/Resources/ic_log_level_fatal.png";
        private const string InfoIconPath = "StackdriverLogsViewer/Resources/ic_log_level_info.png";
        private const string WarningIconPath = "StackdriverLogsViewer/Resources/ic_log_level_warning.png";

        private static readonly Lazy<ImageSource> s_any_icon =
            new Lazy<ImageSource>(() => ResourceUtils.LoadImage(AnyIconPath));
        private static readonly Lazy<ImageSource> s_debug_icon =
            new Lazy<ImageSource>(() => ResourceUtils.LoadImage(DebugIconPath));
        private static readonly Lazy<ImageSource> s_error_icon =
            new Lazy<ImageSource>(() => ResourceUtils.LoadImage(ErrorIconPath));
        private static readonly Lazy<ImageSource> s_fatal_icon =
            new Lazy<ImageSource>(() => ResourceUtils.LoadImage(FatalIconPath));
        private static readonly Lazy<ImageSource> s_info_icon =
            new Lazy<ImageSource>(() => ResourceUtils.LoadImage(InfoIconPath));
        private static readonly Lazy<ImageSource> s_warning_icon =
            new Lazy<ImageSource>(() => ResourceUtils.LoadImage(WarningIconPath));


        private DateTime _timestamp;
        private string _message;

        public LogItem(LogEntry logEntry)
        {
            Entry = logEntry;
            ConvertTimestamp(logEntry.Timestamp);
            _message = ComposeMessage();
        }
        
        public string Date => _timestamp.ToShortDateString();
        public string Time => _timestamp.ToLongTimeString();
        public LogEntry Entry { get; private set; }

        private string ComposePayloadMessage(IDictionary<string, object> dictPayload)
        {
            Debug.Assert(dictPayload != null);
            if (null == dictPayload)
            {
                return string.Empty;
            }

            return string.Join(";", dictPayload.Values).Replace(Environment.NewLine, " ");
        }

        private string ComposeMessage()
        {
            if (Entry?.JsonPayload != null)
            {
                return ComposePayloadMessage(Entry.JsonPayload);
            }

            if (Entry?.ProtoPayload != null)
            {
                return ComposePayloadMessage(Entry.ProtoPayload);
            }

            if (Entry?.TextPayload != null)
            {
                return Entry.TextPayload.Replace(Environment.NewLine, " ");
            }

            if (Entry?.Labels != null)
            {
                return string.Join(";", Entry?.Labels.Values).Replace(Environment.NewLine, " ");
            }

            if (Entry?.Resource?.Labels != null)
            {
                return string.Join(";", Entry?.Resource.Labels).Replace(Environment.NewLine, " ");
            }

            return string.Empty;
        }

        public string Message
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_message))
                {
                    // TODO: make sure what makes sense if there is no payload.
                    return "The log does not contain valid payload";
                }
                else
                {
                    return _message;
                }
            }
        }

        public ImageSource SeverityLevel
        {
            get
            {
                LogSeverity logLevel;
                if (string.IsNullOrWhiteSpace(Entry?.Severity) || 
                    !Enum.TryParse<LogSeverity>(Entry?.Severity, out logLevel))
                {
                    return s_any_icon.Value;
                }

                switch (logLevel)
                {
                    case LogSeverity.EMERGENCY:
                        return s_fatal_icon.Value;
                    case LogSeverity.DEBUG:
                        return s_debug_icon.Value;
                    case LogSeverity.ERROR:
                        return s_error_icon.Value;
                    case LogSeverity.INFO:
                        return s_info_icon.Value;
                    case LogSeverity.WARNING:
                        return s_warning_icon.Value;
                }

                return s_any_icon.Value;
            }
        }

        private void ConvertTimestamp(object timestamp)
        {
            if (timestamp == null)
            {
                Debug.Assert(false, "Entry Timestamp is null");
                _timestamp = DateTime.MaxValue;
            }
            else if (timestamp is DateTime)
            {
                _timestamp = (DateTime)timestamp;
            }
            else
            {
                if (!DateTime.TryParse(timestamp.ToString(), out _timestamp))
                {
                    Debug.Assert(false, "Failed to parse Entry Timestamp");
                    _timestamp = DateTime.MaxValue;
                }
            }
        }

    }

    [System.Windows.Markup.MarkupExtensionReturnType(typeof(IValueConverter))]
    public class VisbilityToBooleanConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (Visibility)value == Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (bool)value ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion
    }


    public class LogEntriesViewModel : ViewModelBase
    {
        private ObservableCollection<LogItem> _logs = new ObservableCollection<LogItem>();
        ListCollectionView _collectionView;
        private Object _collectionViewLock = new Object();
        private string _filter;

        public LogEntriesViewModel()
        {
        }

        public event EventHandler MessageFilterChanged;

        /// <summary>
        /// Append a set of log entries.
        /// </summary>
        public void AddLogs(IList<LogEntry> logEntries)
        {
            if (logEntries == null)
            {
                return;
            }

            foreach (var log in logEntries)
            {
                _logs.Add(new LogItem(log));
                // _collectionView.AddNewItem(new LogItem(log));
            }

            RaisePropertyChanged(nameof(LogEntryList));
        }

        /// <summary>
        /// Replace the current log entries with the new set.
        /// </summary>
        public void SetLogs(IList<LogEntry> logEntries)
        {
            _logs.Clear();
            _collectionView = new ListCollectionView(new List<LogItem>());
            if (logEntries == null)
            {
                RaisePropertyChanged(nameof(LogEntryList));
                return;
            }

            AddLogs(logEntries);
        }

        public ListCollectionView LogEntryList
        {
            get
            {
                lock (_collectionViewLock)
                {
                    _collectionView = new ListCollectionView(_logs);
                    _collectionView.GroupDescriptions.Add(new PropertyGroupDescription("Date"));
                    return _collectionView;
                }
            }
        }

        public string MessageFilter
        {
            get
            {
                return _filter; 
            }

            set
            {
                _filter = value;
                if (string.IsNullOrWhiteSpace(_filter))
                {
                    _collectionView.Filter = null;
                    return;
                }

                Debug.WriteLine($"MessageFilter is called {_filter}");
                lock (_collectionViewLock)
                {
                    var splits = _filter.Split(new Char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    _collectionView.Filter = new Predicate<object>(item => {
                        foreach (var subFilter in splits)
                        {
                            if (((LogItem)item).Message.Contains(subFilter))
                            {
                                return true;
                            }
                        }

                        return false;
                    });

                    // TODO: Add filter changed event handler
                    // So that LogsViewerViewModel can disable the next page button.
                    MessageFilterChanged?.Invoke(this, new EventArgs());
                }
            }
        }
    }

    /// <summary>
    /// The view model for LogsViewerToolWindow.
    /// </summary>
    public class LogsViewerViewModel : ViewModelBase
    {
        private const string GoogleCloudLogoPath = "Theming/Resources/GCP_logo_horizontal.png";
        private static readonly Lazy<ImageSource> s_logo = 
            new Lazy<ImageSource>(() => ResourceUtils.LoadImage(GoogleCloudLogoPath));
        public ImageSource LogoImage => s_logo.Value;

        private string _nextPageToken;
        private string _loadingProgress;
        private Lazy<LoggingDataSource> _dataSource;
        private ProtectedCommand _loadNextPageCommand;
        private ProtectedCommand _refreshCommand;
        private ProtectedCommand _toggleExpandAllCommand;
        private DataGridRowDetailsVisibilityMode _expandAll = DataGridRowDetailsVisibilityMode.Collapsed;

        public LogEntriesViewModel LogEntriesViewModel { get; private set; }
        public LogsFilterViewModel FilterViewModel { get; private set; }

        public ICommand RefreshCommand => _refreshCommand;
        public string RefreshCommandToolTip => "Get newest log (descending order)";
        public ICommand LoadNextPageCommand => _loadNextPageCommand;
        public ICommand ToggleExpandAllCommand => _toggleExpandAllCommand;

        /// <summary>
        /// Initializes the class.
        /// </summary>
        public LogsViewerViewModel()
        {
            _toggleExpandAllCommand = new ProtectedCommand(ToggleExpandAll, canExecuteCommand: true);
            _refreshCommand = new ProtectedCommand(OnRefreshCommand, canExecuteCommand: false);
            _loadNextPageCommand = new ProtectedCommand(LoadNextPage, canExecuteCommand: false);
            _dataSource = new Lazy<LoggingDataSource>(CreateDataSource);
            
            LogEntriesViewModel = new LogEntriesViewModel();
            LogEntriesViewModel.MessageFilterChanged += (sender, e) => _loadNextPageCommand.CanExecuteCommand = false;
            FilterViewModel = new LogsFilterViewModel();
            FilterViewModel.FilterChanged += (sender, e) => Reload();
            LoadOnStartup();
        }

        private async void LoadOnStartup()
        {
            FilterViewModel.ResourceDescriptors = await _dataSource.Value.GetResourceDescriptorAsync();
            Reload();
            FilterOutResource();
        }

        public DataGridRowDetailsVisibilityMode ToggleExpandHideAll
        {
            get
            {
                return _expandAll;
            }
            set
            {
                SetValueAndRaise(ref _expandAll, value);
            }
        } 

        public string Account
        {
            get
            {
                if (CredentialsStore.Default?.CurrentAccount?.AccountName != null)
                {
                    return CredentialsStore.Default?.CurrentAccount?.AccountName;
                }
                else
                {
                    return "Setup Account Is Needed";
                }
            }
        }

        public string Project
        {
            get
            {
                if (CredentialsStore.Default?.CurrentProjectId == null)
                {
                    return "";
                }
                else
                {
                    return CredentialsStore.Default.CurrentProjectId;
                }
            }
        }

        public string LogLoddingProgress
        {
            get
            {
                return _loadingProgress;
            }

            private set
            {
                SetValueAndRaise(ref _loadingProgress, value);
            }
        }


        private async Task<bool> ShouldKeepResourceType(MonitoredResourceDescriptor resourceDescriptor)
        {
            if (resourceDescriptor == null)
            {
                Debug.Assert(false);
                return false;
            }

            string filter = $"resource.type=\"{resourceDescriptor.Type}\"";
            try
            {
                var result =  await _dataSource.Value.GetLogEntryListAsync(filter, pageSize:1);
                return result?.Item1 != null && result.Item1.Count > 0;
            }
            catch (Exception ex)
            {
                // If exception happens. Keep the type.
                Debug.WriteLine($"Check Resource Type Log Entry failed {ex.ToString()}");
                return true;
            }
        }

        private async void FilterOutResource()
        {
            List<MonitoredResourceDescriptor> resources = new List<MonitoredResourceDescriptor>();
            foreach (var resourceType in FilterViewModel.ResourceDescriptors)
            {
                if (await ShouldKeepResourceType(resourceType))
                {
                    resources.Add(resourceType);
                }
            }

            FilterViewModel.ResourceDescriptors = resources;
        }

        private async Task LogLoaddingWrapper(Func<Task> callback)
        {
            _loadNextPageCommand.CanExecuteCommand = false;
            _refreshCommand.CanExecuteCommand = false;
            // TODO: using ... animation or adding it to Resources.
            LogLoddingProgress = "Loading ... ";

            try
            {
                await callback();
                LogLoddingProgress = string.Empty;
            }
            catch (Exception ex)
            {
                LogLoddingProgress = ex.ToString();
            }

            _refreshCommand.CanExecuteCommand = true;
        }


        private string CurrentFilter()
        {
            var finalFilter = $"{FilterViewModel.Filter} {LogEntriesViewModel.MessageFilter}";
            return string.IsNullOrWhiteSpace(finalFilter) ? null : finalFilter;
        }

        private async void Reload()
        {
            await LogLoaddingWrapper(async () => {
                var result = await _dataSource.Value.GetLogEntryListAsync(CurrentFilter(), 
                    descending:FilterViewModel.DateTimePickerViewModel.IsDecendingOrder);
                LogEntriesViewModel.SetLogs(result?.Item1);
                _nextPageToken = result?.Item2;
                _loadNextPageCommand.CanExecuteCommand = !string.IsNullOrWhiteSpace(_nextPageToken);
                FilterViewModel.UpdateFilterWithLogEntries(result?.Item1);
            });
        }

        private void OnRefreshCommand()
        {
            Reload();
        }

        private async void LoadNextPage()
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(_nextPageToken));
            if (string.IsNullOrWhiteSpace(_nextPageToken))
            {
                return;
            }

            await LogLoaddingWrapper(async () =>
            {
                var results = await _dataSource.Value.GetNextPageLogEntryListAsync(_nextPageToken, CurrentFilter());
                LogEntriesViewModel.AddLogs(results?.Item1);
                FilterViewModel.UpdateFilterWithLogEntries(results?.Item1);
            });
        }

        private void ToggleExpandAll()
        {
            if (ToggleExpandHideAll == DataGridRowDetailsVisibilityMode.Collapsed)
            {
                ToggleExpandHideAll = DataGridRowDetailsVisibilityMode.Visible;
            }
            else
            {
                ToggleExpandHideAll = DataGridRowDetailsVisibilityMode.Collapsed;
            }
        }

        private LoggingDataSource CreateDataSource()
        {
            if (CredentialsStore.Default.CurrentProjectId != null)
            {
                return new LoggingDataSource(
                    CredentialsStore.Default.CurrentProjectId,
                    CredentialsStore.Default.CurrentGoogleCredential,
                    GoogleCloudExtensionPackage.VersionedApplicationName);
            }
            else
            {
                return null;
            }
        }
    }
}
