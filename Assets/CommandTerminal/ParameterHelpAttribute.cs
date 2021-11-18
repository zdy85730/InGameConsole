using System;
using System.Reflection;

namespace CommandTerminal
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public class ParameterHelpAttribute : Attribute
    {
        public string Content { get; set; }

        public ParameterHelpAttribute(string content = null) {
            Content = content;
        }
    }
}
