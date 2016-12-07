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
using GoogleCloudExtension.Accounts;
using GoogleCloudExtension.DataSources;
using GoogleCloudExtension.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace GoogleCloudExtension.StackdriverLogsViewer
{
    internal enum LogSeverity
    {
        DEBUG,
        INFO,
        WARNING,
        ERROR,
        EMERGENCY,
        CRITICAL,
        FATAL
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
        public DateTime Time => _timestamp;
        public LogEntry Entry { get; private set; }

        private string ComposePayloadMessage(IDictionary<string, object> dictPayload)
        {
            Debug.Assert(dictPayload != null);
            if (null == dictPayload)
            {
                return string.Empty;
            }

            StringBuilder text = new StringBuilder();
            foreach (var kv in dictPayload)
            {
                text.Append($"{kv.Key}: {kv.Value}  ");
            }

            var s = text.ToString().Replace(Environment.NewLine, "\\n");
            return s.Replace("\t", "\\t");
        }

        private string ComposeMessage()
        {
            if (Entry?.JsonPayload != null)
            {
                if (Entry.JsonPayload.ContainsKey("message"))
                {
                    return Entry.JsonPayload["message"].ToString();
                }
                else
                {
                    return ComposePayloadMessage(Entry.JsonPayload);
                }
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

        public string SeverityTip => string.IsNullOrWhiteSpace(Entry?.Severity) ? "Any" : Entry.Severity;

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
                    case LogSeverity.CRITICAL:
                    case LogSeverity.FATAL:
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

            _timestamp = _timestamp.ToLocalTime();
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

        private const string ShuffleIconPath = "StackdriverLogsViewer/Resources/shuffle.png";
        private static readonly Lazy<ImageSource> s_shuffle_icon =
            new Lazy<ImageSource>(() => ResourceUtils.LoadImage(ShuffleIconPath));
        public ImageSource ShuffleImage => s_shuffle_icon.Value;

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

        private bool descendingOrder;

        /// <summary>
        /// Replace the current log entries with the new set.
        /// </summary>
        /// <param name="logEntries">The log entries list.</param>
        /// <param name="descending">True: Descending order by TimeStamp, False: Ascending order by TimeStamp </param>
        public void SetLogs(IList<LogEntry> logEntries, bool descending)
        {
            descendingOrder = descending;
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
                    var sorted = descendingOrder ? _logs.OrderByDescending(x => x.Time) : _logs.OrderBy(x => x.Time);
                    var sorted_collection = new ObservableCollection<LogItem>(sorted);                    
                    _collectionView = new ListCollectionView(sorted_collection);
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
        //private const string GoogleCloudLogoPath = "Theming/Resources/GCP_logo_horizontal.png";
        //private static readonly Lazy<ImageSource> s_logo = 
        //    new Lazy<ImageSource>(() => ResourceUtils.LoadImage(GoogleCloudLogoPath));
        //public ImageSource LogoImage => s_logo.Value;

        private const string CloudLogo20Path = "StackdriverLogsViewer/Resources/logo_cloud.png";
        private const string SearchIconPath = "StackdriverLogsViewer/Resources/search-icon.png";

        private static readonly Lazy<ImageSource> s_cloud_logo_icon =
            new Lazy<ImageSource>(() => ResourceUtils.LoadImage(CloudLogo20Path));
        private static readonly Lazy<ImageSource> s_search_icon =
            new Lazy<ImageSource>(() => ResourceUtils.LoadImage(SearchIconPath));


        public ImageSource CloudLogo => s_cloud_logo_icon.Value;


        private string _nextPageToken;
        private string _loadingProgress;
        private Lazy<LoggingDataSource> _dataSource;
        private ProtectedCommand _cancelLoadingCommand;
        private ProtectedCommand _toggleExpandAllCommand;
        private DataGridRowDetailsVisibilityMode _expandAll = DataGridRowDetailsVisibilityMode.Collapsed;

        public LogEntriesViewModel LogEntriesViewModel { get; private set; }
        public LogsFilterViewModel FilterViewModel { get; private set; }

        private bool _canCallNextPage = false;
        public ICommand CancelLoadingCommand => _cancelLoadingCommand;
        private Visibility _cancelLoadingVisible = Visibility.Collapsed;
        public Visibility CancelLoadingVisibility
        {
            get
            {
                return _cancelLoadingVisible;
            }

            set
            {
                SetValueAndRaise(ref _cancelLoadingVisible, value);
            }
        }

        private Visibility _messageBoradVisibility = Visibility.Collapsed;
        public Visibility ProgressErrorMessageVisibility
        {
            get
            {
                return _messageBoradVisibility;
            }

            set
            {
                SetValueAndRaise(ref _messageBoradVisibility, value);
            }
        }

        private Visibility _loadingBlockVisibility = Visibility.Collapsed;
        public Visibility LoadingBlockVisibility
        {
            get
            {
                return _loadingBlockVisibility;
            }

            set
            {
                SetValueAndRaise(ref _loadingBlockVisibility, value);
            }
        }

        public ICommand ToggleExpandAllCommand => _toggleExpandAllCommand;

        /// <summary>
        /// Initializes the class.
        /// </summary>
        public LogsViewerViewModel()
        {
            _toggleExpandAllCommand = new ProtectedCommand(ToggleExpandAll, canExecuteCommand: true);
            _cancelLoadingCommand = new ProtectedCommand(() => 
            {
                Debug.WriteLine("Cancel is called");
                LogLoddingProgress = "Cancelling . . .";
                CancelLoadingVisibility = Visibility.Collapsed;
                _cancelled = true;
            });

            _dataSource = new Lazy<LoggingDataSource>(CreateDataSource);
            
            LogEntriesViewModel = new LogEntriesViewModel();
            LogEntriesViewModel.MessageFilterChanged += (sender, e) => _canCallNextPage = false;
            FilterViewModel = new LogsFilterViewModel();
            FilterViewModel.FilterChanged += (sender, e) => Reload();
        }

        public async void LoadOnStartup()
        {
            RaiseAllPropertyChanged();

            if (string.IsNullOrWhiteSpace(CredentialsStore.Default?.CurrentAccount?.AccountName) || 
                string.IsNullOrWhiteSpace(CredentialsStore.Default?.CurrentProjectId))
            {
                return;
            }

            FilterViewModel.ResourceDescriptors = await _dataSource.Value.GetResourceDescriptorsAsync();
            if (FilterViewModel.SelectedResource != null)
            {
                // Reload();
                FilterOutResource();
            }
            else
            {
                // TODO: add error handling here
            }
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
                if (string.IsNullOrWhiteSpace(CredentialsStore.Default?.CurrentAccount?.AccountName))
                {
                    return null;
                }
                else
                {
                    return CredentialsStore.Default?.CurrentAccount?.AccountName;
                }
            }
        }

        public string Project
        {
            get
            {
                if (string.IsNullOrWhiteSpace(CredentialsStore.Default?.CurrentProjectId))
                {
                    return Account == null ? null : "Go to Google Cloud Explore to choose an account";
                }
                else
                {
                    return CredentialsStore.Default.CurrentProjectId;
                }
            }
        }

        private string _errorMessage;
        public string ErrorMessage
        {
            get
            {
                return _errorMessage;
            }

            private set
            {
                SetValueAndRaise(ref _errorMessage, value);
                if (string.IsNullOrWhiteSpace(value))
                {
                    ProgressErrorMessageVisibility = Visibility.Collapsed;
                }
                else
                {
                    ProgressErrorMessageVisibility = Visibility.Visible;
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
                if (string.IsNullOrWhiteSpace(value))
                {
                    LoadingBlockVisibility = Visibility.Collapsed;
                }
                else
                {
                    LoadingBlockVisibility = Visibility.Visible;
                }
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
                var result =  await _dataSource.Value.ListLogEntriesAsync(filter, pageSize: 1);
                return result?.LogEntries != null && result.LogEntries.Count > 0;
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

        private object _isLoadingLockObj = new object();
        private bool _isLoading = false;
        private async Task LogLoaddingWrapper(Func<Task> callback)
        {
            lock (_isLoadingLockObj)
            {
                if (_isLoading)
                {
                    Debug.WriteLine($"_isLoading is true.  Fatal error. fix the code.");
                    return;
                }

                ErrorMessage = null;
                _isLoading = true;
            }


            try
            {
                _canCallNextPage = false;
                FilterViewModel.RefreshCommand.CanExecuteCommand = false;
                //// TODO: using ... animation or adding it to Resources.
                //LogLoddingProgress = "Loading ... ";

                await callback();
                LogLoddingProgress = string.Empty;
                ErrorMessage = string.Empty;
            }
            catch (DataSourceException ex)
            {
                ErrorMessage = ex.Message;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.ToString();
            }
            finally
            {
                _isLoading = false;
                CancelLoadingVisibility = Visibility.Collapsed;
                LogLoddingProgress = string.Empty;
                // Disable fetching next page if cancelled or _nextPageToken is empty
                // This is critical otherwise cancelling a "fetch" won't work
                // Because at the time "Cancelled", a scroll down to the bottom event is raised and triggers
                // another automatic NextPage call.
                _canCallNextPage = (!_cancelled && !string.IsNullOrWhiteSpace(_nextPageToken));
                FilterViewModel.RefreshCommand.CanExecuteCommand = true;
            }
        }


        private string CurrentFilter()
        {
            var finalFilter = $"{FilterViewModel.Filter} {LogEntriesViewModel.MessageFilter}";
            return string.IsNullOrWhiteSpace(finalFilter) ? null : finalFilter;
        }

        /////// <summary>
        /////// LogEntry request parameters.
        /////// </summary>
        ////public class LogEntryRequestParams
        ////{
        ////    /// <summary>
        ////    /// Optional
        ////    /// Refert to https://cloud.google.com/logging/docs/view/advanced_filters. 
        ////    /// </summary>
        ////    public string Filter;

        ////    /// <summary>
        ////    /// Optional
        ////    /// If page size is not specified, a server side default value is used. 
        ////    /// </summary>
        ////    public int? PageSize;

        ////    /// <summary>
        ////    /// Optional "timestamp desc" or "timestamp asc"
        ////    /// </summary>
        ////    public string OrderBy;
        ////}

        //private LogEntryRequestParams CurrentRequestParameters()
        //{
        //    LogEntryRequestParams reqParams = new LogEntryRequestParams();
        //    var finalFilter = $"{FilterViewModel.Filter} {LogEntriesViewModel.MessageFilter}";
        //    reqParams.Filter = string.IsNullOrWhiteSpace(finalFilter) ? null : finalFilter;
        //    reqParams.OrderBy = 
        //        FilterViewModel.DateTimePickerViewModel.IsDecendingOrder ? "timestamp desc" : "timestamp asc";
        //    reqParams.PageSize = _defaultPageSize;
        //    return reqParams;
        //}

        private static readonly int _defaultPageSize = 100;
        private bool _cancelled = false;
        private async Task LoadLogs(bool firstPage)
        {

            int count = 0;
            _cancelled = false;
            //var reqParams = CurrentRequestParameters();

            var finalFilter = $"{FilterViewModel.Filter} {LogEntriesViewModel.MessageFilter}";
            string filter = string.IsNullOrWhiteSpace(finalFilter) ? null : finalFilter;
            var order = FilterViewModel.DateTimePickerViewModel.IsDecendingOrder ? "timestamp desc" : "timestamp asc";

            while (count < _defaultPageSize && !_cancelled)
            {
                Debug.WriteLine($"LoadLogs, count={count}, firstPage={firstPage}");

                CancelLoadingVisibility = Visibility.Visible;
                LogLoddingProgress = "Loading . . .";

                if (firstPage)
                {
                    firstPage = false;
                    _nextPageToken = null;
                    LogEntriesViewModel.SetLogs(null, FilterViewModel.DateTimePickerViewModel.IsDecendingOrder);
                }

                var results = await _dataSource.Value.ListLogEntriesAsync(filter, order, _defaultPageSize, _nextPageToken);
                LogEntriesViewModel.AddLogs(results?.LogEntries);
                FilterViewModel.UpdateFilterWithLogEntries(results?.LogEntries);
                _nextPageToken = results.NextPageToken;
                if (results?.LogEntries != null)
                {
                    count += results.LogEntries.Count;
                }

                if (string.IsNullOrWhiteSpace(_nextPageToken))
                {
                    _nextPageToken = null;
                    break;
                }
            }            

            if (count == 0 && !_cancelled)
            {
                FilterViewModel.TryToRemoveEmptyLogName();
            }
        }



        private async void Reload()
        {
            if (Project == null)
            {
                return;
            }

            await LogLoaddingWrapper(async () => {
                await LoadLogs(firstPage: true);
            });
        }


        public async void LoadNextPage()
        {
            if (!_canCallNextPage || string.IsNullOrWhiteSpace(_nextPageToken))
            {
                return;
            }

            await LogLoaddingWrapper(async () =>
            {
                await LoadLogs(firstPage: false);
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
