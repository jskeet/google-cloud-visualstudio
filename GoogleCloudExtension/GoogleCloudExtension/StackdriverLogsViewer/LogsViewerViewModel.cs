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
using System;
using System.Threading.Tasks;

using System.Diagnostics;
using System.Text;

namespace GoogleCloudExtension.StackdriverLogsViewer
{
    internal enum LogSeverity
    {
        DEBUG,
        INFO,
        WARNING,
        ERROR,
        CRITICAL,
    }

    internal class LogItem
    {
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

        public string SeverityLevel
        {
            get
            {
                LogSeverity logLevel;
                if (string.IsNullOrWhiteSpace(Entry?.Severity) || 
                    !Enum.TryParse<LogSeverity>(Entry?.Severity, out logLevel))
                {
                    return string.Empty;
                }

                switch (logLevel)
                {
                    case LogSeverity.CRITICAL:
                        return "C";
                    case LogSeverity.DEBUG:
                        return "D";
                    case LogSeverity.ERROR:
                        return "E";
                    case LogSeverity.INFO:
                        return "I";
                    case LogSeverity.WARNING:
                        return "W";
                }

                return string.Empty;
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


        public LogEntriesViewModel()
        {
        }

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
                Debug.Assert(false, "Does not expect a get");
                return "";
            }

            set
            {
                Debug.WriteLine($"MessageFilter is called {value}");
                lock (_collectionViewLock)
                {
                    _collectionView.Filter = new Predicate<object>(item => ((LogItem)item).Message.Contains(value));
                }
            }
        }
    }

    /// <summary>
    /// The view model for LogsViewerToolWindow.
    /// </summary>
    public class LogsViewerViewModel : ViewModelBase
    {
        private string _loadingProgress;
        private Lazy<LoggingDataSource> _dataSource;
        private ProtectedCommand _loadNextPageCommand;
        private ProtectedCommand _refreshCommand;
        private ProtectedCommand _toggleExpandAllCommand;
        private DataGridRowDetailsVisibilityMode _expandAll = DataGridRowDetailsVisibilityMode.Collapsed;

        public LogEntriesViewModel LogEntriesViewModel { get; private set; }
        public LogsFilterViewModel FilterViewModel { get; private set; }

        public ICommand RefreshCommand => _refreshCommand;
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
            FilterViewModel = new LogsFilterViewModel();
            FilterViewModel.FilterChanged += (sender, e) => Reload(e.Filter);
            LoadOnStartup();
        }

        private async void LoadOnStartup()
        {
            FilterViewModel.ResourceDescriptors = await _dataSource.Value.GetResourceDescriptorAsync();
            Reload(null);
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

        public string Project
        {
            get
            {
                return CredentialsStore.Default.CurrentProjectId;
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
                _dataSource.Value.PageSize = 1;
                var result =  await _dataSource.Value.GetLogEntryListAsync(filter);
                return result != null && result.Count > 0;
            }
            catch
            {
                // If exception happens. Keep the type.
                return true;
            }
            finally
            {
                _dataSource.Value.PageSize = null;
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
            _loadNextPageCommand.CanExecuteCommand = true;
        }

        private async void Reload(string filter)
        {
            await LogLoaddingWrapper(async () => {
                var logs = await _dataSource.Value.GetLogEntryListAsync(filter);
                LogEntriesViewModel.SetLogs(logs);
                FilterViewModel.UpdateFilterWithLogEntries(logs);
            });
        }

        private void OnRefreshCommand()
        {
            Reload(null);
        }

        private async void LoadNextPage()
        {
            await LogLoaddingWrapper(async () =>
            {
                var logs = await _dataSource.Value.GetNextPageLogEntryListAsync();
                LogEntriesViewModel.AddLogs(logs);
                FilterViewModel.UpdateFilterWithLogEntries(logs);
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
