using System;
using System.Reflection;

namespace CommandTerminal
{
    [AttributeUsage(AttributeTargets.Method)]
    public class RegisterCommandAttribute : Attribute
    {
        public string Name { get; set; }
        public string Help { get; set; }

        public RegisterCommandAttribute(string command_name = null) {
            Name = command_name;
        }
    }
}
