﻿// Copyright 2016 Google Inc. All Rights Reserved.
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
using System.Windows.Media;
using System.Windows.Controls;


namespace GoogleCloudExtension.StackdriverLogsViewer
{
    //public class DateTimeRangeModelView : ViewModelBase
    //{
    //    public DateTime Start = DateTime.Now.AddDays(-30);
    //    public DateTime End = DateTime.MaxValue;

    //    public DateTimeRangeModelView()
    //    {
    //        FiveDayRangeCommand = new ProtectedCommand(()=> {
    //            StartDateTime = DateTime.Now.AddDays(-5).ToString("O");
    //        });
    //    }

    //    private DateTime ConvertTime(string timeString)
    //    {
    //        DateTime dt = DateTime.Parse(timeString);
    //        return TimeZoneInfo.ConvertTime(dt, destinationTimeZone: TimeZoneInfo.Local);
    //    }

    //    public ProtectedCommand FiveDayRangeCommand { get; private set; }

    //    public string StartDateTime
    //    {
    //        get
    //        {
    //            return Start.ToString("O");
    //        }

    //        set
    //        {
    //            Start = ConvertTime(value);
    //            RaisePropertyChanged(nameof(StartDateTime));
    //        }
    //    }

    //    public string EndtDateTime
    //    {
    //        get
    //        {
    //            return End.ToString("O");
    //        }

    //        set
    //        {
    //            End = ConvertTime(value);
    //            RaisePropertyChanged(nameof(EndtDateTime));
    //        }
    //    }
    //}


    ////public class FilterEventArg : EventArgs
    ////{
    ////    public string Filter { get; private set; }

    ////    public FilterEventArg(string filter)
    ////    {
    ////        Filter = filter;
    ////    }
    ////}

    public class LogsFilterViewModel : ViewModelBase
    {
        private const string ToggleButtonImagePath = "StackdriverLogsViewer/Resources/ToggleButton.bmp";

        private static readonly Lazy<ImageSource> s_toggleButton = 
            new Lazy<ImageSource>(() => ResourceUtils.LoadImage(ToggleButtonImagePath));

        public LogsFilterViewModel()
        {
            ResetLogIDs();
            //_dateTimeRangeCommand = new ProtectedCommand(() =>
            //{
            //    IsPopupOpen = !IsPopupOpen;
            //});
            //_setRangeCommand = new ProtectedCommand(SetDateTimeRange);
            //_startDateTime = DateTimeRangePicker.Start;
            //_endDateTime = DateTimeRangePicker.End;

            DateTimePickerViewModel = new LogDateTimePickerViewModel();
            DateTimePickerViewModel.DateTimeFilterChange += (sender, e) => {
                NotifyFilterChanged();
            };

            _submitAdvancedFilterComamnd = new ProtectedCommand(() =>
            {
                NotifyFilterChanged();
            });

            _filterSwitchCommand = new ProtectedCommand(SwapFilter);
            _advanceFilterHelpCommand = new ProtectedCommand(ShowAdvancedFilterHelp);
            _refreshCommand = new ProtectedCommand(OnRefreshCommand, canExecuteCommand: false);
        }

        public ImageSource ToggleButtonImage => s_toggleButton.Value;

        #region Refresh Button
        private ProtectedCommand _refreshCommand;
        public ProtectedCommand RefreshCommand => _refreshCommand;
        public string RefreshCommandToolTip => "Get newest log (descending order)";

        private void OnRefreshCommand()
        {
            DateTimePickerViewModel.IsDecendingOrder = true;
            DateTimePickerViewModel.FilterDateTime = DateTime.MaxValue;
            NotifyFilterChanged();
        }

        #endregion

        #region Filter change 
        public event EventHandler FilterChanged;
        public void NotifyFilterChanged() => FilterChanged?.Invoke(this, new EventArgs());

        public string Filter
        {
            get
            {
                if (_showBasicFilter)
                {
                    return BasicFilter;
                }
                else
                {
                    return AdvancedFilter;
                }
            }
        }

        public string BasicFilter
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

                if (DateTimePickerViewModel.IsDecendingOrder)
                {
                    if (DateTimePickerViewModel.FilterDateTime < DateTime.Now)
                    {
                        filter.AppendLine($"timestamp<=\"{DateTimePickerViewModel.FilterDateTime.ToString("O")}\"");
                    }
                }
                else
                {
                    filter.AppendLine($"timestamp>=\"{DateTimePickerViewModel.FilterDateTime.ToString("O")}\"");
                }

                //if (_startDateTime > DateTime.MinValue)
                //{
                //    filter.AppendLine($"timestamp>=\"{_startDateTime.ToString("O")}\"");
                //}

                //if (_endDateTime < DateTime.Now.AddMinutes(30))
                //{
                //    filter.AppendLine($"timestamp<=\"{_endDateTime.ToString("O")}\"");
                //}

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
                    SetValueAndRaise(ref _selectedResource, value);
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

                if (_selectedResource != null)
                {
                    bool selectedFilteredOut = true;
                    foreach (var descriptor in _resourceDescriptors)
                    {
                        if (descriptor.Name == _selectedResource.Name)
                        {
                            selectedFilteredOut = false;
                            break;
                        }
                    }

                    if (selectedFilteredOut)
                    {
                        _selectedResource = null;
                    }
                }

                if (_selectedResource == null)
                {
                    foreach (var descriptor in _resourceDescriptors)
                    {
                        if (descriptor.DisplayName == "Global")
                        {
                            SelectedResource = descriptor;
                            return;
                        }
                    }
                }
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
        //private DateTimeRangeModelView _dateTimeRangePicker = new DateTimeRangeModelView();
        //private string _dateTimeRange = "This is the current range";
        //private ProtectedCommand _dateTimeRangeCommand;
        //private ProtectedCommand _setRangeCommand;
        //private bool _isPopupOpen = false;
        //public ICommand DateTimeRangeCommand => _dateTimeRangeCommand;
        //public ICommand SetRangeCommand => _setRangeCommand;
        //private DateTime _startDateTime;
        //private DateTime _endDateTime;

        //void SetDateTimeRange()
        //{
        //    IsPopupOpen = false;
        //    Debug.Assert(DateTimeRangePicker.Start <= DateTimeRangePicker.End, 
        //        "Start Date Time is greater than End DateTime" );
        //    if (DateTimeRangePicker.Start != _startDateTime || 
        //        DateTimeRangePicker.End != _endDateTime)
        //    {
        //        _startDateTime = DateTimeRangePicker.Start;
        //        _endDateTime = DateTimeRangePicker.End;
        //        DateTimeRange = _startDateTime.ToString() + " -- "
        //            + (_endDateTime > DateTime.Now.AddMinutes(5) ? "Present" : _endDateTime.ToString());
        //        NotifyFilterChanged();
        //    }

        //}

        //public string DateTimeRange
        //{
        //    get
        //    {
        //        return _dateTimeRange;
        //    }

        //    private set
        //    {
        //        SetValueAndRaise(ref _dateTimeRange, value);
        //    }
        //}

        //public bool IsPopupOpen
        //{
        //    get
        //    {
        //        return _isPopupOpen;
        //    }

        //    set
        //    {
        //        SetValueAndRaise(ref _isPopupOpen, value);
        //    }
        //}

        //public DateTimeRangeModelView DateTimeRangePicker => _dateTimeRangePicker;


        #endregion

        public LogDateTimePickerViewModel DateTimePickerViewModel { get; }

        #region Advanced Filter
        private bool _showBasicFilter = true;
        private ProtectedCommand _filterSwitchCommand;
        private ProtectedCommand _advanceFilterHelpCommand;
        private ProtectedCommand _submitAdvancedFilterComamnd;

        public ICommand SubmitAdvancedFilterCommand => _submitAdvancedFilterComamnd;

        private void ShowAdvancedFilterHelp()
        {
            Process.Start(new ProcessStartInfo("https://cloud.google.com/logging/docs/view/advanced_filters"));
        }

        private void SwapFilter()
        {


            IsSwitchFilterDropDownOpen = false;
            _showBasicFilter = !_showBasicFilter;
            if (_showBasicFilter)
            {
                AdvancedFilter = string.Empty;
            }
            else
            {
                AdvancedFilter = BasicFilter;
            }

            RaisePropertyChanged(nameof(BasicFilterVisibility));
            RaisePropertyChanged(nameof(AdvancedFilterVisibility));
            RaisePropertyChanged(nameof(FilterSwitchButtonContent));
        }

        private string _advacedFilter;
        public string AdvancedFilter
        {
            get
            {
                return _advacedFilter;
            }

            set
            {
                SetValueAndRaise(ref _advacedFilter, value);
            }
        }

        private bool _IsDropdownOpen = false;
        public bool IsSwitchFilterDropDownOpen {
            get
            {
                Debug.WriteLine($"get IsSwitchFilterDropDownOpen {_IsDropdownOpen}");
                return _IsDropdownOpen;
            }

            set
            {
                SetValueAndRaise(ref _IsDropdownOpen, value);
            }
        }

        public ICommand FilterSwitchCommand => _filterSwitchCommand;

        public string FilterSwitchButtonToolTip
        {
            get
            {
                return FilterSwitchButtonContent;
            }
        }

        public string FilterSwitchButtonContent
        {
            get
            {
                return _showBasicFilter ? "Convert to advanced filter" : "Clear and return to basic filter";
            }
        }

        public string AdvancedFilterHelpMessage => "Click to show advanced filter syntax help.";

        public ICommand AdvancedFilterHelpCommand => _advanceFilterHelpCommand;

        public Visibility BasicFilterVisibility
        {
            get
            {
                return _showBasicFilter ? Visibility.Visible : Visibility.Hidden;
            }
        }

        public Visibility AdvancedFilterVisibility
        {
            get
            {
                return _showBasicFilter ? Visibility.Hidden : Visibility.Visible;
            }
        }
        #endregion
    }

}