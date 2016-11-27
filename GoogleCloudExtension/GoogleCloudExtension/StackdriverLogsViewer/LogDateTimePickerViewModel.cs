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
            public bool DescendingOrder;
        }

        private State goodState = new State() { TimeStamp = DateTime.Now, DescendingOrder = true };

        public LogDateTimePickerViewModel()
        {
            orderRadioGroup.Add(new RadioClass { Header = "Newest Log First", CheckedProperty = true });
            orderRadioGroup.Add(new RadioClass { Header = "Oldest Log First", CheckedProperty = false });

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


        public void OnChangedDateTime()
        {
            if (goodState.TimeStamp != _date || goodState.DescendingOrder != orderRadioGroup[0].CheckedProperty)
            {
                Debug.WriteLine("OnChangedDateTime , changed");
                goodState.TimeStamp = _date;
                goodState.DescendingOrder = orderRadioGroup[0].CheckedProperty;
                DateTimeFilterChange?.Invoke(this, new EventArgs());
            }
            else
            {
                Debug.WriteLine("OnChangedDateTime, No Change");
            }

            IsDropDownOpen = false;
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
            get
            {
                Debug.WriteLine($"Get IsDecendingOrder returns {OrderRadioGroup[0].CheckedProperty}");
                return goodState.DescendingOrder;
            }

            set
            {
                Debug.WriteLine($"Set IsDecendingOrder {value}");

                goodState.DescendingOrder = value;

                orderRadioGroup[0].CheckedProperty = value;
                orderRadioGroup[1].CheckedProperty = !value;
            }
        }

        private DateTime _date = DateTime.Now.AddDays(-30);
        public DateTime FilterDateTime
        {
            get
            {
                return goodState.TimeStamp;
            }

            set
            {
                goodState.TimeStamp = value;
                _date = value;

                RaisePropertyChanged(nameof(Date));
                RaisePropertyChanged(nameof(Time));
            }
        }

        private void OnTimeSpanCommand(TimeSpanCommand sender)
        {
            Debug.WriteLine($"{sender.Name} is clicked");
            _date = DateTime.Now - sender.Span;
            OnChangedDateTime();
        }

        public class RadioClass : Model
        {
            public string Header { get; set; }
            private bool isChecked;
            public bool CheckedProperty
            {
                get
                {
                    return isChecked;
                }
                set
                {
                    Debug.WriteLine($"Begin Set {Header} {value}");
                    SetValueAndRaise(ref isChecked, value);
                    Debug.WriteLine($"After Set {Header} {isChecked}");
                }
            }

        }

        public class TimeSpanCommand
        {
            public TimeSpan Span { get; set; }
            public string Name { get; set; }
            public ICommand SetTimeSpan { get; set; } 
        }

        private List<TimeSpanCommand> timeSpanCommands = new List<TimeSpanCommand>();
        public IList<TimeSpanCommand> TimeSpanCommands => timeSpanCommands;
        private List<RadioClass> orderRadioGroup = new List<RadioClass>();
        public List<RadioClass> OrderRadioGroup => orderRadioGroup;

        public string ComboBoxText => "Jump To Time";


        private DateTime ConvertTime(string timeString)
        {
            DateTime dt = DateTime.Parse(timeString);
            return TimeZoneInfo.ConvertTime(dt, destinationTimeZone: TimeZoneInfo.Local);
        }

        public DateTime Date
        {
            get
            {
                return _date.Date;
            }

            // Called by TextBox control
            // Do not set from code, set FilterDateTime instead.
            set
            {
                var timeString = value.Date.ToString("s");
                _date = ConvertTime(timeString) + _date.TimeOfDay;
                RaisePropertyChanged(nameof(Date));
                RaisePropertyChanged(nameof(Time));
            }
        }

        public string Time
        {
            get
            {
                return _date.ToString("t");
            }

            // Called by TextBox control
            // Do not set from code, set FilterDateTime instead.
            set
            {
                // Do not change for now.
            }
        }

        private bool isDropDownOpen = false;
        public bool IsDropDownOpen
        {
            get
            {
                Debug.WriteLine($"Get IsDropDownOpen {isDropDownOpen}");

                return isDropDownOpen;
            }
            set
            {
                Debug.WriteLine($"Set IsDropDownOpen {value}");

                // Open
                if (value)
                {
                    _date = goodState.TimeStamp;
                    RaisePropertyChanged(nameof(Date));
                    RaisePropertyChanged(nameof(Time));
                    orderRadioGroup[0].CheckedProperty = goodState.DescendingOrder;
                    orderRadioGroup[1].CheckedProperty = !goodState.DescendingOrder;
                }
                else   // Close
                {
                    // Do nothing
                }

                SetValueAndRaise(ref isDropDownOpen, value);
            }
        }
    }
}
