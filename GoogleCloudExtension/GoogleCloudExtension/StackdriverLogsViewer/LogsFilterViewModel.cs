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
using System;
using System.Collections.Generic;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Reflection;
using System.Diagnostics;
using System.Text;
using System.Linq;

namespace GoogleCloudExtension.StackdriverLogsViewer
{
    public class DateTimeRangeModelView : ViewModelBase
    {
        public DateTime Start = DateTime.Now.AddDays(-30);
        public DateTime End = DateTime.MaxValue;

        public DateTimeRangeModelView()
        {
            FiveDayRangeCommand = new ProtectedCommand(()=> {
                StartDateTime = DateTime.Now.AddDays(-5).ToString("O");
            });
        }

        private DateTime ConvertTime(string timeString)
        {
            DateTime dt = DateTime.Parse(timeString);
            return TimeZoneInfo.ConvertTime(dt, destinationTimeZone: TimeZoneInfo.Local);
        }

        public ProtectedCommand FiveDayRangeCommand { get; private set; }

        public string StartDateTime
        {
            get
            {
                return Start.ToString("O");
            }

            set
            {
                Start = ConvertTime(value);
                RaisePropertyChanged(nameof(StartDateTime));
            }
        }

        public string EndtDateTime
        {
            get
            {
                return End.ToString("O");
            }

            set
            {
                End = ConvertTime(value);
                RaisePropertyChanged(nameof(EndtDateTime));
            }
        }
    }


    public class FilterEventArg : EventArgs
    {
        public string Filter { get; private set; }

        public FilterEventArg(string filter)
        {
            Filter = filter;
        }
    }

    public class LogsFilterViewModel : ViewModelBase
    {
        public LogsFilterViewModel()
        {
            ResetLogIDs();
            _dateTimeRangeCommand = new ProtectedCommand(() =>
            {
                IsPopupOpen = !IsPopupOpen;
            });
            _setRangeCommand = new ProtectedCommand(SetDateTimeRange);
            _startDateTime = DateTimeRangePicker.Start;
            _endDateTime = DateTimeRangePicker.End;
        }

        #region Filter change 
        public event EventHandler<FilterEventArg> FilterChanged;
        public void NotifyFilterChanged() => FilterChanged?.Invoke(this, new FilterEventArg(Filter));

        private string Filter
        {
            get
            {
                StringBuilder filter = new StringBuilder();
                if (_selectedResource != null)
                {
                    filter.AppendLine($"resource.type=\"{_selectedResource.Type}\"");
                }

                if (_selectedLogID != null && _selectedLogID != _allLogIdsText)
                {
                    if (!_logShortNameToIdLookup.ContainsKey(_selectedLogID))
                    {
                        Debug.Assert(false, $"_logShortNameToIdLookup does not find {_selectedLogID}.");
                    }
                    else
                    {
                        filter.AppendLine($"logName=\"{_logShortNameToIdLookup[_selectedLogID]}\"");
                    }
                }

                // _logSeverityList[0] is All Log Levels
                if (_selectedLogSeverity != null && _selectedLogSeverity != _logSeverityList[0])
                {
                    filter.AppendLine($"severity={_selectedLogSeverity}");
                }

                if (_startDateTime > DateTime.MinValue)
                {
                    filter.AppendLine($"timestamp>=\"{_startDateTime.ToString("O")}\"");
                }

                if (_endDateTime < DateTime.Now.AddMinutes(30))
                {
                    filter.AppendLine($"timestamp<=\"{_endDateTime.ToString("O")}\"");
                }

                return filter.Length > 0 ? filter.ToString() : null;
            }
        }

        /// <summary>
        /// When a new batch of Entry is fetched from data source, update the filter options.
        /// </summary>
        public void UpdateFilterWithLogEntries(IList<LogEntry> logs)
        {
            if (logs == null)
            {
                return;
            }

            bool addedId = false;
            try
            {
                foreach (LogEntry log in logs)
                {
                    string logName = log?.LogName;
                    if (logName == null)
                    {
                        continue;
                    }

                    if (_logIDs.ContainsKey(logName.ToLower()))
                    {
                        continue;
                    }

                    var splits = logName.Split(new string[] { "/", "%2F", "%2f" }, StringSplitOptions.RemoveEmptyEntries);
                    string shortName = splits[splits.Length - 1];
                    _logIDs[logName.ToLower()] = shortName;
                    if (_logShortNameToIdLookup.ContainsKey(shortName))
                    {
                        Debug.Assert(false, 
                            $"Found same short name of {_logShortNameToIdLookup[shortName]} and {logName}");
                        continue;
                    }

                    _logShortNameToIdLookup[shortName] = logName;
                    addedId = true;
                }
            }
            catch (Exception ex)
            {
                Debug.Assert(false, ex.ToString());
            }
            finally
            {
                if (addedId)
                {
                    RaisePropertyChanged(nameof(LogIDs));
                }
            }
        }
        #endregion

        #region Resource ComboBox
        private MonitoredResourceDescriptor _selectedResource;
        private IList<MonitoredResourceDescriptor> _resourceDescriptors;
        public IEnumerable ResourcesSelection
        {
            get
            {
                return _resourceDescriptors;
            }
            
        }

        public string ResourcesComboBoxTip => Resources.LogViewerResourcesComboBoxTip;

        public MonitoredResourceDescriptor SelectedResource
        {
            get
            {
                return _selectedResource;
            }

            set
            {
                if (value != null && _selectedResource != value)
                {
                    _selectedResource = value;
                    ResetLogIDs();
                    try
                    {
                        NotifyFilterChanged();
                    }
                    catch (Exception ex)
                    {
                        Debug.Assert(false, ex.ToString());
                    }
                }
            }
        }

        public IList<MonitoredResourceDescriptor> ResourceDescriptors
        {
            get
            {
                return _resourceDescriptors;
            }

            set
            {
                _resourceDescriptors = value;
                RaisePropertyChanged(nameof(ResourcesSelection));
                RaisePropertyChanged(nameof(SelectedResource));
            }
        }

        #endregion

        #region Log name ComboBox
        private string _allLogIdsText = "All Logs";
        private string _selectedLogID;
        private Dictionary<string, string> _logIDs = new Dictionary<string, string>();
        private Dictionary<string, string> _logShortNameToIdLookup = new Dictionary<string, string>();

        public void ResetLogIDs()
        {
            _logIDs.Clear();
            _logIDs.Add(_allLogIdsText, _allLogIdsText);
            _selectedLogID = _allLogIdsText;
            _logShortNameToIdLookup.Clear();
            RaisePropertyChanged(nameof(SelectedLogID));
            RaisePropertyChanged(nameof(LogIDs));
        }

        public ObservableCollection<string> LogIDs
        {
            get
            {
                return new ObservableCollection<string>(_logIDs.Values);
            }
        }

        public string SelectedLogID
        {
            get
            {
                return _selectedLogID;
            }

            set
            {
                if (_selectedLogID != value)
                {
                    _selectedLogID = value;
                    NotifyFilterChanged();
                }
            }
        }
        #endregion

        #region Log Severity Filter ComboBox
        private string _selectedLogSeverity;
        private string[] _logSeverityList = 
            new string[] {"Any Log Level", "DEBUG", "INFO", "WARNING", "ERROR", "CRITICAL" };
        public IEnumerable<string> LogSeverityList => _logSeverityList;
        public string SelectedLogSeverity
        {
            get
            {
                return _selectedLogSeverity;
            }

            set
            {
                if (value != null && _selectedLogSeverity != value)
                {
                    _selectedLogSeverity = value;
                    NotifyFilterChanged();
                }
            }
        }
        #endregion

        #region Date time range 
        private DateTimeRangeModelView _dateTimeRangePicker = new DateTimeRangeModelView();
        private string _dateTimeRange = "This is the current range";
        private ProtectedCommand _dateTimeRangeCommand;
        private ProtectedCommand _setRangeCommand;
        private bool _isPopupOpen = false;
        public ICommand DateTimeRangeCommand => _dateTimeRangeCommand;
        public ICommand SetRangeCommand => _setRangeCommand;
        private DateTime _startDateTime;
        private DateTime _endDateTime;

        void SetDateTimeRange()
        {
            IsPopupOpen = false;
            Debug.Assert(DateTimeRangePicker.Start <= DateTimeRangePicker.End, 
                "Start Date Time is greater than End DateTime" );
            if (DateTimeRangePicker.Start != _startDateTime || 
                DateTimeRangePicker.End != _endDateTime)
            {
                _startDateTime = DateTimeRangePicker.Start;
                _endDateTime = DateTimeRangePicker.End;
                DateTimeRange = _startDateTime.ToString() + " -- "
                    + (_endDateTime > DateTime.Now.AddMinutes(5) ? "Present" : _endDateTime.ToString());
                NotifyFilterChanged();
            }

        }

        public string DateTimeRange
        {
            get
            {
                return _dateTimeRange;
            }

            private set
            {
                SetValueAndRaise(ref _dateTimeRange, value);
            }
        }

        public bool IsPopupOpen
        {
            get
            {
                return _isPopupOpen;
            }

            set
            {
                SetValueAndRaise(ref _isPopupOpen, value);
            }
        }

        public DateTimeRangeModelView DateTimeRangePicker => _dateTimeRangePicker;


        #endregion

    }

}
