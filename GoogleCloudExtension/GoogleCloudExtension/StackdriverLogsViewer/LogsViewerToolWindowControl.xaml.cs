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

using GoogleCloudExtension.Utils;
using Google.Apis.Logging.v2.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.ComponentModel;
using System;
using System.Diagnostics;


namespace GoogleCloudExtension.StackdriverLogsViewer
{
    /// <summary>
    /// Interaction logic for LogsViewerToolWindowControl.
    /// </summary>
    public partial class LogsViewerToolWindowControl : UserControl
    {
        private const string RefreshImagePath = "StackdriverLogsViewer/Resources/refresh.png";
        private const string RefreshMouseOverImagePath = "StackdriverLogsViewer/Resources/refresh-mouseover.png";
        private const string RefreshMouseDownImagePath = "StackdriverLogsViewer/Resources/refresh-mouse-down.png";
        private static readonly Lazy<ImageSource> s_refreshImage =
            new Lazy<ImageSource>(() => ResourceUtils.LoadImage(RefreshImagePath));
        private static readonly Lazy<ImageSource> s_refreshMouseOverImage =
            new Lazy<ImageSource>(() => ResourceUtils.LoadImage(RefreshMouseOverImagePath));
        private static readonly Lazy<ImageSource> s_refreshMouseDownImage =
            new Lazy<ImageSource>(() => ResourceUtils.LoadImage(RefreshMouseDownImagePath));

        bool _do_logging = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="LogsViewerToolWindowControl"/> class.
        /// </summary>
        public LogsViewerToolWindowControl()
        {
            this.InitializeComponent();
            refreshImage.Source = s_refreshImage.Value;
        }


        private void TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox t = (TextBox)sender;
            string filter = t.Text;
            ICollectionView cv = CollectionViewSource.GetDefaultView(dg.ItemsSource);
            if (filter == "")
                cv.Filter = null;
            else
            {
                cv.Filter = o =>
                {
                    LogEntry p = o as LogEntry;
                    if (p == null)
                    {
                        return true;  // TODO replace it with correct logic.
                    }
                    switch(t.Name)
                    {
                        case "txtTime":
                            return FilterTime(p, filter);
                        case "txtMessage":
                            return FilterText(p, filter);
                        default:
                            return true;
                    }
                };
            }
        }

        private bool FilterText(LogEntry log, string filter)
        {
            if (log.TextPayload == null)
            {
                return false;
            }

            return log.TextPayload.ToUpper().Contains(filter);
        }

        private bool FilterTime(LogEntry log, string filter)
        {
            return true;
        }

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            for (var vis = sender as Visual; vis != null; vis = VisualTreeHelper.GetParent(vis) as Visual)
                if (vis is DataGridRow)
                {
                    var row = (DataGridRow)vis;
                    row.DetailsVisibility = row.DetailsVisibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                    break;
                }
        }

        private void Expander_Collapsed(object sender, RoutedEventArgs e)
        {
            for (var vis = sender as Visual; vis != null; vis = VisualTreeHelper.GetParent(vis) as Visual)
                if (vis is DataGridRow)
                {
                    var row = (DataGridRow)vis;
                    row.DetailsVisibility = row.DetailsVisibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                    break;
                }
        }

        private DataGridRow preSelected;
        private int preSelectedRowInex;

        private void dg_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            if (dg.SelectedIndex >= 0)
            { 
                DataGridRow row = (DataGridRow)dg.ItemContainerGenerator.ContainerFromIndex(dg.SelectedIndex);
                preSelected = row;
                preSelectedRowInex = dg.SelectedIndex;
            }
            //if (row != null)
            //{
            //    row.DetailsVisibility = row.DetailsVisibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            //}

            if (_do_logging)
            {
                Debug.WriteLine($"dg_selectionchanged {dg.SelectedIndex}");
            }
            //SelectedRow()?.InvalidateVisual();

            // This is necessary to fix:
            // By default DataGrid opens detail view on selected row. 
            // It automatically opens detail view on mouse move
            if (_do_logging)
            {
                Debug.WriteLine($"dg_selectionchanged UnselectAll");
            }
            dg.UnselectAll();
        }

        private void dg_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            DependencyObject dep = (DependencyObject)e.OriginalSource;

            // iteratively traverse the visual tree
            while ((dep != null) && !(dep is DataGridCell))
            {
                dep = VisualTreeHelper.GetParent(dep);
            }

            if (dep == null)
                return;


            DataGridCell cell = dep as DataGridCell;
            // do something

            // navigate further up the tree
            while ((dep != null) && !(dep is DataGridRow))
            {
                dep = VisualTreeHelper.GetParent(dep);
            }

            DataGridRow row = dep as DataGridRow;
            if (row != null)
            {
                row.DetailsVisibility = row.DetailsVisibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            }
        }


        private void btnExpandAll_Click(object sender, RoutedEventArgs e)
        {
            if (dg.RowDetailsVisibilityMode == DataGridRowDetailsVisibilityMode.Collapsed)
            {
                dg.RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.Visible;
            }
            else
            {
                dg.RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.Collapsed;
            }
        }

        private void Expander_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            btnExpandAll_Click(null, null);
        }

        private void allExpander_Expanded(object sender, RoutedEventArgs e)
        {
            dg.RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.Visible;
        }

        private void allExpander_Collapsed(object sender, RoutedEventArgs e)
        {
            dg.RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.Collapsed;
        }

        private void btnRefresh_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                refreshImage.Source = s_refreshMouseDownImage.Value;
            }
        }

        private void btnRefresh_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Released)
            {
                refreshImage.Source = s_refreshMouseOverImage.Value;
            }
        }

        private void btnRefresh_MouseLeave(object sender, MouseEventArgs e)
        {
            refreshImage.Source = s_refreshImage.Value;
        }

        private void btnRefresh_MouseEnter(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Released)
            {
                refreshImage.Source = s_refreshMouseOverImage.Value;
            }
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                refreshImage.Source = s_refreshMouseDownImage.Value;
            }

        }

        private void ComboBox_Loaded(Object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            var comboBoxTemplate = comboBox.Template;
            var toggleButton = comboBoxTemplate.FindName("toggleButton", comboBox) as ToggleButton;
            var toggleButtonTemplate = toggleButton.Template;
            var border = toggleButtonTemplate.FindName("templateRoot", toggleButton) as Border;

            border.Background = new SolidColorBrush(Colors.White);
        }

        private DataGridRow SelectedRow()
        {
            return (DataGridRow)dg.ItemContainerGenerator.ContainerFromIndex(dg.SelectedIndex);
        }

        private DataGridRow previousHighlighted;

        private DataGridRow MouseOverRow(MouseEventArgs e)
        {
            // Use HitTest to resolve the row under the cursor
            var ele = dg.InputHitTest(e.GetPosition(null));
            if (_do_logging)
            {
                Debug.WriteLine($"InputHitTest element {ele?.GetType()}");
            }
            return ele as DataGridRow;

            //// If there was no DataGridViewRow under the cursor, return
            //if (rowIndex == -1) { return; }

            //// Clear all other selections before making a new selection
            //dgv.ClearSelection();

            //// Select the found DataGridViewRow
            //dgv.Rows[rowIndex].Selected = true;
        }

        /// <summary>
        /// Somehow this is neccessary to change the seleted item
        /// Otherwise the "SelectedItem" become white blank.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dg_MouseMove(object sender, MouseEventArgs e)
        {
            //Debug.WriteLine("dg_MouseMove Unselected All");
            //dg.UnselectAll();

            // MouseOverRow(e);

            if (true)
            {
                DependencyObject dep = (DependencyObject)e.OriginalSource;

                // iteratively traverse the visual tree
                while ((dep != null) && !(dep is DataGridCell))
                {
                    dep = VisualTreeHelper.GetParent(dep);
                }

                if (dep == null)
                    return;


                DataGridCell cell = dep as DataGridCell;
                // do something

                // navigate further up the tree
                while ((dep != null) && !(dep is DataGridRow))
                {
                    dep = VisualTreeHelper.GetParent(dep);
                }

                DataGridRow row = dep as DataGridRow;
                if (row != null)
                {
                    int rowIndex = dg.ItemContainerGenerator.IndexFromContainer(row);
                    if (_do_logging)
                    {
                        Debug.WriteLine($"Set Selected to row {rowIndex}, previous selected {dg.SelectedIndex} ");
                    }
                    if (previousHighlighted != row)
                    {
                        previousHighlighted?.InvalidateVisual();
                        previousHighlighted = row;
                    }
                    
                    if (preSelected != row)
                    {
                        if (_do_logging)
                        {
                            Debug.WriteLine($"pre selected row {preSelectedRowInex} {preSelected}");
                        }
                        preSelected?.InvalidateVisual();
                        preSelected?.InvalidateMeasure();
                    }

                    //var preSelectedRow = SelectedRow();
                    object item = dg.Items[rowIndex];
                    dg.SelectedItem = item;
                    //preSelectedRow?.InvalidateVisual();

                    //dg.ScrollIntoView(item);
                    //row.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                }
            }
        }
    

        ////#region JsonView
        ////private void JValue_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        ////{
        ////    if (e.ClickCount != 2)
        ////        return;

        ////    var tb = sender as TextBlock;
        ////    if (tb != null)
        ////    {
        ////        Clipboard.SetText(tb.Text);
        ////    }
        ////}

        //private void ExpandAll(object sender, RoutedEventArgs e)
        //{
        //    ToggleItems(true);
        //}

        //private void CollapseAll(object sender, RoutedEventArgs e)
        //{
        //    ToggleItems(false);
        //}

        //private void ToggleItems(bool isExpanded)
        //{
        //    if (JsonTreeView.Items.IsEmpty)
        //        return;

        //    var prevCursor = Cursor;
        //    //System.Windows.Controls.DockPanel.Opacity = 0.2;
        //    //System.Windows.Controls.DockPanel.IsEnabled = false;
        //    Cursor = Cursors.Wait;
        //    _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Normal, delegate
        //    {
        //        ToggleItems(JsonTreeView, JsonTreeView.Items, isExpanded);
        //        //System.Windows.Controls.DockPanel.Opacity = 1.0;
        //        //System.Windows.Controls.DockPanel.IsEnabled = true;
        //        _timer.Stop();
        //        Cursor = prevCursor;
        //    }, Application.Current.Dispatcher);
        //    _timer.Start();
        //}

        //private void ToggleItems(ItemsControl parentContainer, ItemCollection items, bool isExpanded)
        //{
        //    var itemGen = parentContainer.ItemContainerGenerator;
        //    if (itemGen.Status == Generated)
        //    {
        //        Recurse(items, isExpanded, itemGen);
        //    }
        //    else
        //    {
        //        itemGen.StatusChanged += delegate
        //        {
        //            Recurse(items, isExpanded, itemGen);
        //        };
        //    }
        //}

        //private void Recurse(ItemCollection items, bool isExpanded, ItemContainerGenerator itemGen)
        //{
        //    if (itemGen.Status != Generated)
        //        return;

        //    foreach (var item in items)
        //    {
        //        var tvi = itemGen.ContainerFromItem(item) as TreeViewItem;
        //        tvi.IsExpanded = isExpanded;
        //        ToggleItems(tvi, tvi.Items, isExpanded);
        //    }
        ////}
        //#endregion
    }
}