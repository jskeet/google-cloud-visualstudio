using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace JsonViewerDemo.TemplateSelectors
{
    public sealed class JPropertyDataTemplateSelector : DataTemplateSelector
    {
        public DataTemplate PrimitivePropertyTemplate { get; set; }
        public DataTemplate ComplexPropertyTemplate { get; set; }
        public DataTemplate ArrayPropertyTemplate { get; set; }
        public DataTemplate ObjectPropertyTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if(item == null)
                return null;

            var frameworkElement = container as FrameworkElement;
            if(frameworkElement == null)
                return null;

            var type = item.GetType();
            if (type == typeof(JProperty))
            {
                var jProperty = item as JProperty;
                switch (jProperty.Value.Type)
                {
                    case JTokenType.Object:
                        return frameworkElement.FindResource("ObjectPropertyTemplate") as DataTemplate;
                    case JTokenType.Array:
                        return frameworkElement.FindResource("ArrayPropertyTemplate") as DataTemplate;
                    default:
                        return frameworkElement.FindResource("PrimitivePropertyTemplate") as DataTemplate;

                }
            }

            if (type.Name == "KeyValuePair`2")
            {

                try
                {
                    object value = ((dynamic)item).Value;
                    // var keyValuePair = (System.Collections.Generic.KeyValuePair<string, object>)item;
                    if (value.GetType().Namespace.StartsWith("Newtonsoft.Json"))
                    {
                        return frameworkElement.FindResource("KeyValuePairJObject") as DataTemplate;
                    }
                    else if (value is string || value.GetType().IsValueType)
                    {
                        return frameworkElement.FindResource("KeyValuePair") as DataTemplate;
                    }
                    else
                    {
                        Debug.WriteLine($"type.Name value.GetType()");
                    }
                }
                catch
                {
                    Debug.WriteLine("Exception at converting keyValuePair");

                }

            }

            var key = new DataTemplateKey(type);
            try
            {
                return frameworkElement.FindResource(key) as DataTemplate;
            }
            catch
            {
                return null;
            }
        }
    }
}
