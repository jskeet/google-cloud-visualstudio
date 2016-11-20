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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.ComponentModel;
using System;


namespace GoogleCloudExtension.StackdriverLogsViewer
{
    /// <summary>
    /// Interaction logic for LogsViewerToolWindowControl.
    /// </summary>
    public partial class LogsViewerToolWindowControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LogsViewerToolWindowControl"/> class.
        /// </summary>
        public LogsViewerToolWindowControl()
        {
            this.InitializeComponent();
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

        private void dg_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            dg.UnselectAll();
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