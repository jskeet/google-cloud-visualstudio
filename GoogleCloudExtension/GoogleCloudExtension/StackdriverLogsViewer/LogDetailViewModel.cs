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
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Reflection;
using System.Diagnostics;


namespace GoogleCloudExtension.StackdriverLogsViewer
{

    public class VisibilityToNullableBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility)
            {
                return (((Visibility)value) == Visibility.Visible);
            }
            else
            {
                return Binding.DoNothing;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool?)
            {
                return (((bool?)value) == true ? Visibility.Visible : Visibility.Collapsed);
            }
            else if (value is bool)
            {
                return (((bool)value) == true ? Visibility.Visible : Visibility.Collapsed);
            }
            else
            {
                return Binding.DoNothing;
            }
        }
    }

    public class Payload
    {
        public Payload(string name, object value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }
        public object Value { get; }
    }

    public sealed class LogItemConverter : IValueConverter
    {
        private void AddPayload(ObservableCollection<object> lists, string name, object obj)
        {
            if (obj == null)
            {
                return;
            }

            lists.Add(new Payload(name, obj));
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            ////List<ObjectNode> nodes = new List<ObjectNode>();
            ////nodes.Add(new ObjectNode("result", value));
            ////return nodes;

            if (!(value is LogItem))
            {
                throw new NotSupportedException("GetType().Name can only converte value type of LogItem");
            }

            LogItem log = value as LogItem;
            var objNode = new ObjectNode(log.Entry);
            AddPayload(objNode.Children, nameof(log.Entry.JsonPayload), log.Entry.JsonPayload);
            AddPayload(objNode.Children, nameof(log.Entry.Labels), log.Entry.Labels);
            AddPayload(objNode.Children, nameof(log.Entry.ProtoPayload), log.Entry.ProtoPayload);
            return objNode.Children;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException(GetType().Name + " can only be used for one way conversion.");
        }
    }

    internal class ObjectNode
    {
        #region Private Properties

        private string _name;
        private object _value;
        private Type _type;

        #endregion

        #region Constructor

        public ObjectNode(object value)
        {
            ParseObjectTree("root", value, value.GetType());
        }

        public ObjectNode(string name, object value)
        {
            ParseObjectTree(name, value, value.GetType());
        }

        public ObjectNode(object value, Type t)
        {
            ParseObjectTree("root", value, t);
        }

        public ObjectNode(string name, object value, Type t)
        {
            ParseObjectTree(name, value, t);
        }

        #endregion

        #region Public Properties

        public string Name
        {
            get
            {
                return _name;
            }
        }

        public object Value
        {
            get
            {
                return _value;
            }
        }

        public Type Type
        {
            get
            {
                return _type;
            }
        }

        public ObservableCollection<object> Children { get; set; }

        #endregion

        #region Private Methods

        private bool IsList(object o)
        {
            if (o == null) return false;
            return o is IList &&
                   o.GetType().IsGenericType &&
                   o.GetType().GetGenericTypeDefinition().IsAssignableFrom(typeof(List<>));
        }

        private bool IsDictionary(object o)
        {
            if (o == null) return false;
            return o is IDictionary &&
                   o.GetType().IsGenericType &&
                   o.GetType().GetGenericTypeDefinition().IsAssignableFrom(typeof(Dictionary<,>));
        }

        private void ParseObjectTree(string name, object value, Type type)
        {
            Children = new ObservableCollection<object>();

            _type = type;
            _name = name;

            if (value == null)
            {
                return;
            }

            if (IsDictionary(value) || IsList(value))
            {
                _value = type.Name;
                return;
            }

            if (value != null)
            {
                if (value is string && type != typeof(object))
                {
                    if (value != null)
                    {
                        _value = "\"" + value + "\"";
                    }
                }
                else if (value is double || value is bool || value is int || value is float || value is long || value is decimal)
                {
                    _value = value;
                }
                else
                {
                    _value = "{" + value.ToString() + "}";
                }
            }

            PropertyInfo[] props = type.GetProperties();

            if (props.Length == 0 && type.IsClass && value is IEnumerable && !(value is string))
            {
                IEnumerable arr = value as IEnumerable;

                if (arr != null)
                {
                    int i = 0;
                    foreach (object element in arr)
                    {
                        Children.Add(new ObjectNode("[" + i + "]", element, element.GetType()));
                        i++;
                    }

                }
            }

            foreach (PropertyInfo p in props)
            {
                if (!p.PropertyType.IsPublic)
                {
                    continue;
                }

                if (p.PropertyType.IsClass || p.PropertyType.IsArray)
                {
                    if (p.PropertyType.IsArray)
                    {
                        try
                        {
                            object v = p.GetValue(value);
                            IEnumerable arr = v as IEnumerable;

                            ObjectNode arrayNode = new ObjectNode(p.Name, arr.ToString(), typeof(object));

                            if (arr != null)
                            {
                                int i = 0, k = 0;
                                ObjectNode arrayNode2;

                                foreach (object element in arr)
                                {
                                    //Handle 2D arrays
                                    if (element is IEnumerable && !(element is string))
                                    {
                                        arrayNode2 = new ObjectNode("[" + i + "]", element.ToString(), typeof(object));

                                        IEnumerable arr2 = element as IEnumerable;
                                        k = 0;

                                        foreach (object e in arr2)
                                        {
                                            arrayNode2.Children.Add(new ObjectNode("[" + k + "]", e, e.GetType()));
                                            k++;
                                        }

                                        arrayNode.Children.Add(arrayNode2);
                                    }
                                    else
                                    {
                                        arrayNode.Children.Add(new ObjectNode("[" + i + "]", element, element.GetType()));
                                    }
                                    i++;
                                }

                            }

                            Children.Add(arrayNode);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex.ToString());
                        }
                    }
                    else
                    {
                        try
                        {
                            object v = p.GetValue(value, null);
                            if (v != null)
                            {
                                Children.Add(new ObjectNode(p.Name, v, p.PropertyType));
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex.ToString());
                        }
                    }
                }
                else if (p.PropertyType.IsValueType && !(value is string))
                {
                    try
                    {
                        object v = p.GetValue(value);

                        if (v != null)
                        {
                            Children.Add(new ObjectNode(p.Name, v, p.PropertyType));
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.ToString());
                    }
                }
                else if (p.PropertyType.IsGenericType)
                {
                    try
                    {
                        object v = p.GetValue(value);

                        if (v != null)
                        {
                            // var objNode = new ObjectNode(p.Name, v, p.PropertyType);
                            // Children.Add(v);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.ToString());
                    }
                }
            }
        }

        #endregion
    }

}
