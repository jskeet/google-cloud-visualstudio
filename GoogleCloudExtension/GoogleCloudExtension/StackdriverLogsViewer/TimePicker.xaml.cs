using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace GoogleCloudExtension.Controls
{
    /// <summary>
    /// Interaction logic for TimePicker.xaml
    /// </summary>
    public partial class TimePicker : UserControl
    {
        public TimePicker()
        {
            InitializeComponent();
        }

    }

    [TemplatePart(Name = "PART_HourEditor", Type = typeof(TextBox))]
    [TemplatePart(Name = "PART_MinuteEditor", Type = typeof(TextBox))]
    [TemplatePart(Name = "PART_SecondEditor", Type = typeof(TextBox))]
    [TemplatePart(Name = "PART_UpButton", Type = typeof(RepeatButton))]
    [TemplatePart(Name = "PART_DownButton", Type = typeof(RepeatButton))]
    [TemplatePart(Name = "PART_Time", Type = typeof(ComboBox))]
    public class TimePickerControl : Control
    {
        static TimePickerControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TimePickerControl), new FrameworkPropertyMetadata(typeof(TimePickerControl)));
        }
        static bool HourValidateValue(object value)
        {
            int t = (int)value;
            return !(t < 0 || t > 12);
        }
        static bool MinuteValidateValue(object value)
        {
            int t = (int)value;
            return !(t < 0 || t > 60);
        }
        static bool SecondValidateValue(object value)
        {
            int t = (int)value;
            return !(t < 0 || t > 60);
        }

        public TimeSpan Time
        {
            get
            {
                var h = Hour;
                var hour = TimeType == TimeType.AM ? h : (h % 12) + 12;
                return new TimeSpan(hour, Minute, Second); 
            }

            set
            {
                TimeType = TimeType.AM;
                var h = value.Hours;
                if (h >= 12)
                {
                    h = value.Hours - 12;
                    TimeType = TimeType.PM;
                }
                if (h == 0)
                {
                    h = 12;
                }

                Hour = h;
                Minute = value.Minutes;
                Second = value.Seconds;
            }
        }

        public TimeSpan ControlTime
        {
            get
            {
                return (TimeSpan)GetValue(ControlTimeProperty);
            }

            set
            {
                SetValue(ControlTimeProperty, value);
            }
        }

        public int Hour
        {
            get
            {  
                return (int)GetValue(HourProperty);
            }

            set {
                Debug.WriteLine("Hour changed");
                SetValue(HourProperty, value);
            }
        }

        public int Minute
        {
            get { return (int)GetValue(MinuteProperty); }
            set { SetValue(MinuteProperty, value); }
        }
        public int Second
        {
            get { return (int)GetValue(SecondProperty); }
            set { SetValue(SecondProperty, value); }
        }
        public TimeType TimeType
        {
            get { return (TimeType)GetValue(TimeTypeProperty); }
            set { SetValue(TimeTypeProperty, value); }
        }
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            this.upButton = this.Template.FindName("PART_UpButton", this) as RepeatButton;
            this.downButton = this.Template.FindName("PART_DownButton", this) as RepeatButton;
            this.hourEditor = this.Template.FindName("PART_HourEditor", this) as TextBox;
            this.minuteEditor = this.Template.FindName("PART_MinuteEditor", this) as TextBox;
            this.secondEditor = this.Template.FindName("PART_SecondEditor", this) as TextBox;
            this.upButton.Click += RepeatButton_Click;
            this.downButton.Click += RepeatButton_Click;
            foreach (var textBox in TextBoxParts)
            {
                textBox.PreviewKeyDown += TextBox_PreviewKeyDown;
                textBox.PreviewTextInput += TextBox_PreviewTextInput;
                //textBox.TextChanged += TextBox_TextChanged;
            }
        }


        private int SenderIndex(object sender)
        {
            for (int i = 0; i < TextBoxParts.Length; ++i)
            {
                if (TextBoxParts[i] == sender as TextBox)
                {
                    return i;
                }
            }

            return -1;
        }

        private void MoveToNextBox(object sender)
        {
            int idx = SenderIndex(sender);
            if (idx >= 0 && idx < 2)
            {
                TextBoxParts[idx + 1].Focus();
            }
        }

        private TextBox[] TextBoxParts => new TextBox[] { hourEditor, minuteEditor, secondEditor };

        private static bool IsTextAllowed(string text)
        {
            Regex regex = new Regex("[^0-9.-]+"); //regex that matches disallowed text
            return !regex.IsMatch(text);
        }

        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!IsTextAllowed(e.Text))
            {
                e.Handled = true;
                return;
            }
        }


        //private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        //{
        //    var textBox = sender as TextBox;
        //    if (textBox.Text.Length >= 2)
        //    {
        //        MoveToNextBox(sender);
        //    }
        //}

        private void RepeatButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(hourEditor.IsFocused || minuteEditor.IsFocused || secondEditor.IsFocused))
            {
                hourEditor.Focus();
            }

            DependencyProperty dp = null;
            int maxValue = 60;
            if (this.hourEditor.IsFocused)
            {
                dp = HourProperty;
                maxValue = 12;
            }
            if (this.minuteEditor.IsFocused) dp = MinuteProperty;
            if (this.secondEditor.IsFocused) dp = SecondProperty;
            if (dp == null) return;
            int value = (int)this.GetValue(dp);
            if (e.Source == this.upButton)
                ++value;
            else
                --value;
            if (value < 0 || value > maxValue) return;
            this.SetValue(dp, value);
        }

        private bool _changeTriggeredByControlTime = false;
        private bool _changeTriggeredByTimeParts = false;
        private void OnControlTimeChange(TimeSpan newValue)
        {
            if (_changeTriggeredByTimeParts)
            {
                return;
            }

            _changeTriggeredByControlTime = true;
            try
            {
                Time = newValue;
            }
            finally
            {
                _changeTriggeredByControlTime = false;
            }
        }

        private void OnTimePartsChange()
        {
            if (_changeTriggeredByControlTime)
            {
                return;
            }

            _changeTriggeredByTimeParts = true;
            try
            {
                ControlTime = Time;
            }
            finally
            {
                _changeTriggeredByTimeParts = false;
            }
        }

        private static void OnControlTimePropertyChanged(DependencyObject source,
                DependencyPropertyChangedEventArgs e)
        {
            var control = source as TimePickerControl;
            Debug.WriteLine($"OnControlTimePropertyChanged, old value {e.OldValue}, new value  {e.NewValue}");
            if (e.NewValue != e.OldValue)
            {
                control.OnControlTimeChange((TimeSpan)e.NewValue);
            }
        }

        private static void OnCurrentTimePropertyChanged(DependencyObject source,
                DependencyPropertyChangedEventArgs e)
        {
            var control = source as TimePickerControl;
            Debug.WriteLine($"OnCurrentTimePropertyChanged, {e.Property.Name} old value {e.OldValue}, new value  {e.NewValue}");
            if (e.NewValue != e.OldValue)
            {
                control.OnTimePartsChange();
            }
        }

        public static readonly DependencyProperty ControlTimeProperty =
        DependencyProperty.Register("ControlTime", typeof(TimeSpan),
            typeof(TimePickerControl), 
            new FrameworkPropertyMetadata(TimeSpan.Zero, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnControlTimePropertyChanged));
        public static readonly DependencyProperty TimeTypeProperty =
        DependencyProperty.Register("TimeType", typeof(TimeType), typeof(TimePickerControl), 
            new FrameworkPropertyMetadata(TimeType.AM, OnCurrentTimePropertyChanged));
        public static readonly DependencyProperty HourProperty =
        DependencyProperty.Register("Hour", typeof(int), typeof(TimePickerControl), 
            new FrameworkPropertyMetadata(OnCurrentTimePropertyChanged), HourValidateValue);
        public static readonly DependencyProperty MinuteProperty =
        DependencyProperty.Register("Minute", typeof(int), typeof(TimePickerControl), 
            new FrameworkPropertyMetadata(OnCurrentTimePropertyChanged), MinuteValidateValue);
        public static readonly DependencyProperty SecondProperty =
        DependencyProperty.Register("Second", typeof(int), typeof(TimePickerControl), 
            new FrameworkPropertyMetadata(OnCurrentTimePropertyChanged), SecondValidateValue);
        private RepeatButton upButton, downButton;
        private TextBox hourEditor, minuteEditor, secondEditor;



        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!e.IsDown)
            {
                return;
            }

            var textBox = sender as TextBox;
            int index = SenderIndex(sender);
            if (index < 0)
            {
                return;
            }

            if (e.Key == Key.Down || e.Key == Key.Enter && index < 2)
            {
                TextBoxParts[index + 1].Focus();
            }

            if (e.Key == Key.Up && index > 0)
            {
                TextBoxParts[index - 1].Focus();
            }

            if (e.Key == Key.Left && textBox.CaretIndex == 0)
            {
                if (index > 0)
                {
                    TextBoxParts[index - 1].Focus();
                }
            }

            if (e.Key == Key.Right && textBox.CaretIndex == textBox.Text.Length)
            {
                if (index < 2)
                {
                    TextBoxParts[index + 1].Focus();
                }
            }
        }
    }

    [Serializable]
    public enum TimeType { AM, PM }
}
