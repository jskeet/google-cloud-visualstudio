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
using System.Windows.Data;
using System.Windows.Input;
using System;

namespace GoogleCloudExtension.StackdriverLogsViewer
{
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

    public class LogDetailViewModel : ViewModelBase
    {
        public LogEntry Log { get; set; }
    }

    public class LogEntriesViewModel : ViewModelBase
    {
        private ObservableCollection<LogEntry> _logs = new ObservableCollection<LogEntry>();
        ListCollectionView _collectionView;

        public ObservableCollection<LogEntry> Logs
        {
            set {
                _logs = value;
                RaisePropertyChanged(nameof(LogEntryList));
            }
        }

        public ListCollectionView LogEntryList
        {
            get
            {
                ListCollectionView collectionView = new ListCollectionView(_logs);
                collectionView.GroupDescriptions.Add(new PropertyGroupDescription("Timestamp"));
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

        public LogEntriesViewModel LogEntries { get; private set; }
        public ICommand RefreshCommand { get; } 
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
            RefreshCommand = new ProtectedCommand(OnRefreshCommand);
            _dataSource = new Lazy<LoggingDataSource>(CreateDataSource);
            LogEntries = new LogEntriesViewModel();
        }

        private async void OnRefreshCommand()
        {
            var logs = await _dataSource.Value.GetLogEntryListAsync(null);
            LogEntries.Logs = new ObservableCollection<LogEntry>(logs);
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
