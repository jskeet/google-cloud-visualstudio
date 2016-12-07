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
        //private const string ToggleButtonImagePath = "StackdriverLogsViewer/Resources/ToggleButton.bmp";

        //private static readonly Lazy<ImageSource> s_toggleButton = 
        //    new Lazy<ImageSource>(() => ResourceUtils.LoadImage(ToggleButtonImagePath));
        //public ImageSource ToggleButtonImage => s_toggleButton.Value;

        private const string FilterHelpIconPath = "StackdriverLogsViewer/Resources/advanced-filter-help.png";
        private static readonly Lazy<ImageSource> s_filter_help_icon =
            new Lazy<ImageSource>(() => ResourceUtils.LoadImage(FilterHelpIconPath));
        public ImageSource FilterHelpImage => s_filter_help_icon.Value;


        public LogsFilterViewModel()
        {
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
        public void NotifyFilterChanged()
        {
            // Adding _basicFilter is important:
            // When the main page fetches next page, it comes to get the filter again. 
            // This is required to keep the filter same.
            if (_showBasicFilter)
            {
                _filter = BasicFilter;
            }
            else
            {
                _filter = AdvancedFilter;
            }

            Debug.WriteLine("NotifyFilterChanged");
            FilterChanged?.Invoke(this, new EventArgs());
        }

        public string Filter
        {
            get
            {
                return _filter;
            }
        }

        private string _filter;
        private string BasicFilter
        {
            get
            {
                StringBuilder filter = new StringBuilder();
                if (_selectedResource != null)
                {
                    filter.AppendLine($"resource.type=\"{_selectedResource.Type}\"");
                }

                // _logSeverityList[0] is All Log Levels
                if (_selectedLogSeverity != null && _selectedLogSeverity != _logSeverityList[0])
                {
                    filter.AppendLine($"severity={_selectedLogSeverity}");
                }

                if (!string.IsNullOrWhiteSpace(_logNameFilter?.Filter))
                {
                    filter.AppendLine(_logNameFilter.Filter);
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

            _logNameFilter._donotShowNotInTheList = false;

            bool addedId = false;
            try
            {
                foreach (LogEntry log in logs)
                {
                    addedId = addedId || _logNameFilter.TryAddLogID(log);
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
                    Debug.WriteLine("UpdateFilterWithLogEntries RaisePropertyChanged(nameof(LogIDs));");
                    RaisePropertyChanged(nameof(LogIDs));
                }
            }

            _logNameFilter.AddEmptySelection();
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
                MonitoredResourceDescriptor nextSelection = _selectedResource;
                if (nextSelection != null)
                {
                    bool selectedFilteredOut = true;
                    foreach (var descriptor in _resourceDescriptors)
                    {
                        if (descriptor.Type == nextSelection.Type)
                        {
                            selectedFilteredOut = false;
                            break;
                        }
                    }

                    if (selectedFilteredOut)
                    {
                        nextSelection = null;
                    }
                }

                if (nextSelection == null)
                {
                    foreach (var descriptor in _resourceDescriptors)
                    {
                        if (descriptor.Type.ToLower() == "global")
                        {
                            nextSelection = descriptor;
                            break;
                        }
                    }
                }

                if (nextSelection == null)
                {
                    foreach (var descriptor in _resourceDescriptors)
                    {
                        if (descriptor.Type.ToLower() == "gce_instance")
                        {
                            nextSelection = descriptor;
                            break;
                        }
                    }
                }

                if (nextSelection == null)
                {
                    nextSelection = _resourceDescriptors?[0];
                }

                SelectedResource = nextSelection;
                RaisePropertyChanged(nameof(ResourcesSelection));
            }
        }

        #endregion

        #region Log name ComboBox
        private LogNameFilter _logNameFilter;
        private Dictionary<string, LogNameFilter> _logNameFilterCache = new Dictionary<string, LogNameFilter>();
        private class LogNameFilter
        {
            public string _selectedLogID;
            public Dictionary<string, string> _logIDs = new Dictionary<string, string>();
            public Dictionary<string, string> _logShortNameToIdLookup = new Dictionary<string, string>();
            public List<string> _logNameCollection = new List<string>();

            public LogNameFilter(Action notifySelectedChange, Action notifyNameCollectionChange)
            {
                _logIDs.Clear();
                _logNameCollection.Clear();
                _logNameCollection.Add(_allLogIdsText);
                _selectedLogID = _allLogIdsText;
                _logShortNameToIdLookup.Clear();
                _notifySelectedIDChange = notifySelectedChange;
                _notifyLogNameCollectionChange = notifyNameCollectionChange;
            }

            private Action _notifySelectedIDChange;
            private Action _notifyLogNameCollectionChange;

            public const string _logNameNotListed = "Not On The List";
            public const string _allLogIdsText = "All Logs";
            public const string _emptySelection = "Select ... ";
            private string _filter;
            public bool _donotShowNotInTheList = false;

            public string Filter => _filter;

            /// <summary>
            /// When there is no 
            /// </summary>
            public void TryToRemoveNotOnTheList()
            {
                AddEmptySelection();
                _donotShowNotInTheList = true;
                _notifyLogNameCollectionChange();
            }

            public void RemoveEmptySelection()
            {
                Debug.WriteLine("RemoveEmptySelection is called");
                if (_logNameCollection[0] == _emptySelection)
                {
                    _logNameCollection.RemoveAt(0);
                    _notifyLogNameCollectionChange();
                }
            }

            public void AddEmptySelection()
            {
                // After user selected the "Not On The List",
                // Set the selection to "Empty" that remindes the user to make another selection if they like.
                if (_selectedLogID == _logNameNotListed)
                {
                    Debug.WriteLine("AddEmptySelection is called");
                    if (_logNameCollection[0] != _emptySelection)
                    {
                        _logNameCollection.Insert(0, _emptySelection);
                        _notifyLogNameCollectionChange();
                    }

                    _selectedLogID = _emptySelection;
                    _notifySelectedIDChange();
                }
            }

            public bool TryAddLogID(LogEntry log)
            {
                string logName = log?.LogName;
                if (logName == null)
                {
                    return false;
                }

                if (_logIDs.ContainsKey(logName.ToLower()))
                {
                    return false;
                }

                var splits = logName.Split(new string[] { "/", "%2F", "%2f" }, StringSplitOptions.RemoveEmptyEntries);
                string shortName = splits[splits.Length - 1];
                _logIDs[logName.ToLower()] = shortName;
                if (_logShortNameToIdLookup.ContainsKey(shortName))
                {
                    Debug.Assert(false,
                        $"Found same short name of {_logShortNameToIdLookup[shortName]} and {logName}");
                    return false;
                }

                _logNameCollection.Add(shortName);
                _logShortNameToIdLookup[shortName] = logName;
                return true;
            }

            public ObservableCollection<string> LogIDs
            {
                get
                {
                    var collection = new ObservableCollection<string>(_logNameCollection);
                    if (collection.Count > 1 && !_donotShowNotInTheList)
                    {
                        collection.Add(_logNameNotListed);
                    }

                    return collection;
                }
            }

            public void SetLogNameFilter()
            {
                StringBuilder filter = new StringBuilder();
                if (_selectedLogID != null && _selectedLogID != _allLogIdsText)
                {
                    if (_selectedLogID == _logNameNotListed)
                    {
                        filter.Append("logName=(");
                        foreach (string logId in _logShortNameToIdLookup.Values)
                        {
                            filter.Append($" NOT {logId} ");
                        }
                        filter.Append(")");
                    }
                    else if (!_logShortNameToIdLookup.ContainsKey(_selectedLogID))
                    {
                        Debug.Assert(false, $"_logShortNameToIdLookup does not find {_selectedLogID}.");
                    }
                    else
                    {
                        filter.Append($"logName=\"{_logShortNameToIdLookup[_selectedLogID]}\"");
                    }
                }

                _filter = filter.ToString();
            }
        }

        public void TryToRemoveEmptyLogName()
        { 
            _logNameFilter.TryToRemoveNotOnTheList();
        }

        public void ResetLogIDs()
        {
            Debug.WriteLine($"ResetLogIDs {_selectedResource.Type}");
            Debug.Assert(_selectedResource != null);
            if (!_logNameFilterCache.ContainsKey(_selectedResource.Type))
            {
                var newFilter = new LogNameFilter( 
                    () => RaisePropertyChanged(nameof(SelectedLogID)), 
                    () => RaisePropertyChanged(nameof(LogIDs)));
                _logNameFilterCache.Add(_selectedResource.Type, newFilter);
                _logNameFilter = newFilter;
            }
            else
            {
                _logNameFilter = _logNameFilterCache[_selectedResource.Type];
            }

            // Turn out the order is critical when both LogIDs and SelectedLogID changes.
            // If reverse the order, the selected ends up as empty.
            RaisePropertyChanged(nameof(SelectedLogID));
            RaisePropertyChanged(nameof(LogIDs));
        }

        public ObservableCollection<string> LogIDs
        {
            get
            {
                return _logNameFilter?.LogIDs;
            }
        }

        public string SelectedLogID
        {
            get
            {
                Debug.WriteLine($"SelectedLogID get {_logNameFilter?._selectedLogID}");
                return _logNameFilter?._selectedLogID;
            }

            // Trick:  Do not set by programatically.                
            set
            {
                Debug.Assert(_logNameFilter != null);
                Debug.WriteLine($"SelectedLogID changed new Value {value}, existing value {_logNameFilter._selectedLogID}");
                if (value == null)
                {
                    // It happens when LogIDs change
                    // Ignore it
                    return;
                }

                if (_logNameFilter._selectedLogID != value)
                {
                    _logNameFilter._selectedLogID = value;
                    if (value == LogNameFilter._emptySelection)
                    {
                        // Empty value means no change. 
                        // Do Nothing
                    }
                    else
                    {
                        _logNameFilter.SetLogNameFilter();
                        NotifyFilterChanged();
                        _logNameFilter.RemoveEmptySelection();
                    }
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
                return _showBasicFilter ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public Visibility AdvancedFilterVisibility
        {
            get
            {
                return _showBasicFilter ? Visibility.Collapsed : Visibility.Visible;
            }
        }
        #endregion
    }

}
