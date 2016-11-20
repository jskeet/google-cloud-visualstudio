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

using System.Diagnostics;

namespace GoogleCloudExtension.StackdriverLogsViewer
{
    internal class LogItem
    {
        private DateTime _timestamp;

        public string Date => _timestamp.ToShortDateString();
        public string Time => _timestamp.ToLongTimeString();
        public LogEntry LogEntry { get; }
        public string Message => LogEntry.TextPayload;

        public LogItem(LogEntry logEntry)
        {
            LogEntry = logEntry;
            ConvertTimestamp(logEntry.Timestamp);
        }


        private void ConvertTimestamp(object timestamp)
        {
            if (timestamp == null)
            {
                Debug.Assert(false, "LogEntry Timestamp is null");
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
                    Debug.Assert(false, "Failed to parse LogEntry Timestamp");
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

        /// <summary>
        /// Append a set of log entries.
        /// </summary>
        public void AddLogs(IList<LogEntry> logEntries)
        {
            foreach (var log in logEntries)
            {
                _logs.Add(new LogItem(log));
            }
            RaisePropertyChanged(nameof(LogEntryList));
        }

        /// <summary>
        /// Replace the current log entries with the new set.
        /// </summary>
        public void SetLogs(IList<LogEntry> logEntries)
        {
            _logs.Clear();
            AddLogs(logEntries);
        }

        public ListCollectionView LogEntryList
        {
            get
            {
                ListCollectionView collectionView = new ListCollectionView(_logs);
                collectionView.GroupDescriptions.Add(new PropertyGroupDescription("Date"));
                return collectionView;
            }
        }

        public LogEntriesViewModel()
        {
        }
    }

    /// <summary>
    /// The view model for LogsViewerToolWindow.
    /// </summary>
    public class LogsViewerViewModel : ViewModelBase
    {
        private Lazy<LoggingDataSource> _dataSource;
        private ProtectedCommand _loadNextPageCommand;
        private ProtectedCommand _refreshCommand;
        private ProtectedCommand _toggleExpandAllCommand;
        private DataGridRowDetailsVisibilityMode _expandAll = DataGridRowDetailsVisibilityMode.Collapsed;

        public LogEntriesViewModel LogEntries { get; private set; }
        public ICommand RefreshCommand => _refreshCommand;
        public ICommand LoadNextPageCommand => _loadNextPageCommand;
        public ICommand ToggleExpandAllCommand => _toggleExpandAllCommand;
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

        /// <summary>
        /// Initializes the class.
        /// </summary>
        public LogsViewerViewModel()
        {
            _toggleExpandAllCommand = new ProtectedCommand(ToggleExpandAll, canExecuteCommand:true);
            _refreshCommand = new ProtectedCommand(OnRefreshCommand);
            _loadNextPageCommand = new ProtectedCommand(LoadNextPage, canExecuteCommand: false);
            _dataSource = new Lazy<LoggingDataSource>(CreateDataSource);
            LogEntries = new LogEntriesViewModel();
            OnRefreshCommand();
        }

        private async void OnRefreshCommand()
        {
            _loadNextPageCommand.CanExecuteCommand = false;
            _refreshCommand.CanExecuteCommand = false;
            var logs = await _dataSource.Value.GetLogEntryListAsync(null);
            LogEntries.SetLogs(logs);
            _refreshCommand.CanExecuteCommand = true;
            _loadNextPageCommand.CanExecuteCommand = true;
        }

        private async void LoadNextPage()
        {
            _loadNextPageCommand.CanExecuteCommand = false;
            _refreshCommand.CanExecuteCommand = false;
            var logs = await _dataSource.Value.GetNextPageLogEntryListAsync();
            LogEntries.AddLogs(logs);
            _refreshCommand.CanExecuteCommand = true;
            _loadNextPageCommand.CanExecuteCommand = true;
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
