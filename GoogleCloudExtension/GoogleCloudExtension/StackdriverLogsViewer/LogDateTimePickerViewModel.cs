using GoogleCloudExtension.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Diagnostics;

namespace GoogleCloudExtension.StackdriverLogsViewer
{
    public class LogDateTimePickerViewModel : ViewModelBase
    {
        private class State
        {
            public DateTime TimeStamp;
            public bool IsDescendingOrder;
        }

        private State goodState = new State();

        private TimeZoneInfo _timeZone;

        public LogDateTimePickerViewModel(TimeZoneInfo timeZone)
        {
            _timeZone = timeZone;
            //orderRadioGroup.Add(new RadioClass { Header = "Newest Log First", CheckedProperty = true });
            //orderRadioGroup.Add(new RadioClass { Header = "Oldest Log First", CheckedProperty = false });

            timeSpanCommands.Add(new TimeSpanCommand() { Span = TimeSpan.FromMinutes(30), Name = "Half Hour" });
            timeSpanCommands.Add(new TimeSpanCommand() { Span = TimeSpan.FromHours(1), Name = "1 Hour" });
            timeSpanCommands.Add(new TimeSpanCommand() { Span = TimeSpan.FromHours(5), Name = "5 Hour" });
            timeSpanCommands.Add(new TimeSpanCommand() { Span = TimeSpan.FromDays(1), Name = "1 Day" });
            timeSpanCommands.Add(new TimeSpanCommand() { Span = TimeSpan.FromDays(5), Name = "5 Days" });
            timeSpanCommands.Add(new TimeSpanCommand() { Span = TimeSpan.FromDays(10), Name = "10 Days" });

            foreach (var timeSpan in timeSpanCommands)
            {
                timeSpan.SetTimeSpan = new ProtectedCommand(() => OnTimeSpanCommand(timeSpan));
            }

            OKCommand = new ProtectedCommand(OnChangedDateTime);
        }

        public event EventHandler DateTimeFilterChange;


        public void ChangeTimeZone(TimeZoneInfo timeZone)
        {
            _timeZone = timeZone;
            goodState.TimeStamp = TimeZoneInfo.ConvertTime(goodState.TimeStamp, timeZone);
        }

        public void OnChangedDateTime()
        {
            DateTime newDateTime = _uiElementDate.Date.Add(_time);
            if (goodState.TimeStamp != newDateTime || goodState.IsDescendingOrder != SelectedDescendingOrder)
            {
                Debug.WriteLine("OnChangedDateTime Invoke DateTimeFilterChange , changed");
                goodState.TimeStamp = newDateTime;
                goodState.IsDescendingOrder = SelectedDescendingOrder;
                DateTimeFilterChange?.Invoke(this, new EventArgs());
            }
            else
            {
                Debug.WriteLine("OnChangedDateTime, No Change");
            }

            IsDropDownOpen = false;
        }

        // before or after
        private int _selectedOrder = 0;
        public int SelectedOrderIndex
        {
            get { return _selectedOrder; }
            set { SetValueAndRaise(ref _selectedOrder, value); }
        }

        private bool SelectedDescendingOrder
        {
            get { return _selectedOrder == 0; }
            set { SelectedOrderIndex = value ? 0 : 1; }
        }


        /// <summary>
        /// The command to execute to accept the changes.
        /// </summary>
        public ICommand OKCommand { get; }

        /// <summary>   
        /// False:  Ascending order 
        /// </summary>
        public bool IsDecendingOrder
        {
            get { return goodState.IsDescendingOrder; }
            set {
                goodState.IsDescendingOrder = value;
                SelectedDescendingOrder = value;
            }

            //get
            //{
            //    Debug.WriteLine($"Get IsDecendingOrder returns {OrderRadioGroup[0].CheckedProperty}");
            //    return goodState.DescendingOrder;
            //}

            //set
            //{
            //    Debug.WriteLine($"Set IsDecendingOrder {value}");

            //    goodState.DescendingOrder = value;

            //    orderRadioGroup[0].CheckedProperty = value;
            //    orderRadioGroup[1].CheckedProperty = !value;
            //}
        }

        private DateTime _uiElementDate;
        private TimeSpan _time;
        public DateTime FilterDateTimeUtc
        {
            get
            {
                return TimeZoneInfo.ConvertTimeToUtc(goodState.TimeStamp);
            }

            set
            {
                DateTime dt = TimeZoneInfo.ConvertTimeFromUtc(value, _timeZone);
                goodState.TimeStamp = dt;
                _uiElementDate = dt.Date;
                _time = dt.TimeOfDay;

                RaisePropertyChanged(nameof(UiElementDate));
                RaisePropertyChanged(nameof(Time));
            }
        }

        private void OnTimeSpanCommand(TimeSpanCommand sender)
        {
            Debug.WriteLine($"{sender.Name} is clicked");
            _uiElementDate = DateTime.Now - sender.Span;
            OnChangedDateTime();
        }

        //public class RadioClass : Model
        //{
        //    public string Header { get; set; }
        //    private bool isChecked;
        //    public bool CheckedProperty
        //    {
        //        get
        //        {
        //            return isChecked;
        //        }
        //        set
        //        {
        //            Debug.WriteLine($"Begin Set {Header} {value}");
        //            SetValueAndRaise(ref isChecked, value);
        //            Debug.WriteLine($"After Set {Header} {isChecked}");
        //        }
        //    }

        //}

        public class TimeSpanCommand
        {
            public TimeSpan Span { get; set; }
            public string Name { get; set; }
            public ICommand SetTimeSpan { get; set; } 
        }

        private List<TimeSpanCommand> timeSpanCommands = new List<TimeSpanCommand>();
        public IList<TimeSpanCommand> TimeSpanCommands => timeSpanCommands;
        //private List<RadioClass> orderRadioGroup = new List<RadioClass>();
        //public List<RadioClass> OrderRadioGroup => orderRadioGroup;

        public string ComboBoxText => "Jump To Time";


        private DateTime Now
        {
            get { return TimeZoneInfo.ConvertTime(DateTime.UtcNow, _timeZone); }
        }

        public DateTime UiElementDate
        {
            get
            {
                if (_uiElementDate > Now)
                {
                    _uiElementDate = Now.Date;
                }
                
                return _uiElementDate;
            }
                
            // Called by TextBox control
            // Do not set from code, set FilterDateTime instead.
            set
            {
                var timeString = value.Date.ToString("s");
                _uiElementDate = DateTime.Parse(timeString);
                RaisePropertyChanged(nameof(UiElementDate));
                // RaisePropertyChanged(nameof(Time));
            }
        }


        public TimeSpan Time
        {
            get
            {
                Debug.WriteLine("LogDateTimePickerViewModel ControlTime Get");
                return _time;
            }

            set
            {
                Debug.WriteLine("LogDateTimePickerViewModel ControlTime Set");
                SetValueAndRaise(ref _time, value);
            }
        }

        private bool _isDropDownOpen = false;
        public bool IsDropDownOpen
        {
            get
            {
                Debug.WriteLine($"Get IsDropDownOpen {_isDropDownOpen}");

                return _isDropDownOpen;
            }
            set
            {
                Debug.WriteLine($"Set IsDropDownOpen {value}");

                // Open
                if (value)
                {
                    _uiElementDate = goodState.TimeStamp.Date;
                    _time = goodState.TimeStamp.TimeOfDay;
                    RaisePropertyChanged(nameof(UiElementDate));
                    RaisePropertyChanged(nameof(Time));
                    SelectedDescendingOrder = goodState.IsDescendingOrder;
                    //orderRadioGroup[0].CheckedProperty = goodState.DescendingOrder;
                    //orderRadioGroup[1].CheckedProperty = !goodState.DescendingOrder;
                }
                else   // Close
                {
                    // Do nothing
                }

                SetValueAndRaise(ref _isDropDownOpen, value);
            }
        }
    }
}
