using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;

namespace CommandTerminal
{
    public class CommandInfo
    {
        public enum ArgType
        {
            Invalid,
            Int,
            Float,
            Bool
        }

        public MethodInfo methodInfo;
        public ArgType[] argTypes;
        public string help;
        public bool isArrayArgs;   //是否是直接处理CommandArg[]为参数的函数

        private static object ParseArgByType(ArgType type, CommandArg arg)
        {
            switch (type)
            {
                case ArgType.Bool:
                    return arg.Bool;
                case ArgType.Int:
                    return arg.Int;
                case ArgType.Float:
                    return arg.Float;
            }
            return null;
        }

        public static ArgType ParseType(Type type)
        {
            if(type == typeof(int))
            {
                return ArgType.Int;
            }
            else if(type == typeof(bool))
            {
                return ArgType.Bool;
            }
            else if(type == typeof(float))
            {
                return ArgType.Float;
            }
            return ArgType.Invalid;
        }

        public object[] ParseParameters(params CommandArg[] args)
        {
            if (isArrayArgs)
            {
                return new object[1] { args };
            }

            if(args == null || args.Length == 0)
            {
                return null;
            }

            if (args.Length != argTypes.Length)
            {
                Terminal.Shell.IssueErrorMessage(
                    "Incorrect args num {0}, expected <{1}>",
                    args.Length, argTypes.Length
                );
                return null;
            }

            object[] parameters = new object[args.Length];
            for (int i = 0; i < args.Length; ++i)
            {
                parameters[i] = ParseArgByType(argTypes[i], args[i]);
                if (Terminal.IssuedError)
                {
                    return null;
                }
            }

            return parameters;
        }

        public void Invoke(object[] parameters)
        {
            methodInfo.Invoke(null, parameters);
        }

        public string[] GetParamtersHelp()
        {
            ParameterInfo[] parameterInfos = methodInfo.GetParameters();
            string[] result = new string[parameterInfos.Length];
            if (isArrayArgs)
            {
                ParameterHelpAttribute attr = parameterInfos[0].GetCustomAttribute<ParameterHelpAttribute>();
                if(attr != null && string.IsNullOrWhiteSpace(attr.Content))
                {
                    result[0] = string.Format("{0} CommandArg[] {1}", parameterInfos[0].Name, attr.Content);
                }
                result[0] = string.Format("{0} CommandArg[]", parameterInfos[0].Name);
                return result;
            }

            StringBuilder sb = new StringBuilder();
            for(int i = 0; i < parameterInfos.Length; ++i)
            {
                ParameterHelpAttribute attr = parameterInfos[0].GetCustomAttribute<ParameterHelpAttribute>();
                if (attr != null && string.IsNullOrWhiteSpace(attr.Content))
                {
                    result[i] = string.Format("{0} {1} {2}", parameterInfos[i].Name, argTypes[i].ToString(), attr.Content);
                }
                result[i] = string.Format("{0} {1}", parameterInfos[i].Name, argTypes[i].ToString());
            }
            return result;
        }
    }

    public struct CommandArg
    {
        public string String { get; set; }

        public int Int {
            get {
                int int_value;

                if (int.TryParse(String, out int_value)) {
                    return int_value;
                }

                TypeError("int");
                return 0;
            }
        }

        public float Float {
            get {
                float float_value;

                if (float.TryParse(String, out float_value)) {
                    return float_value;
                }

                TypeError("float");
                return 0;
            }
        }

        public bool Bool {
            get {
                if (string.Compare(String, "TRUE", ignoreCase: true) == 0) {
                    return true;
                }

                if (string.Compare(String, "FALSE", ignoreCase: true) == 0) {
                    return false;
                }

                TypeError("bool");
                return false;
            }
        }

        public override string ToString() {
            return String;
        }

        void TypeError(string expected_type) {
            Terminal.Shell.IssueErrorMessage(
                "Incorrect type for {0}, expected <{1}>",
                String, expected_type
            );
        }
    }

    public class CommandShell
    {
        Dictionary<string, CommandInfo> commands = new Dictionary<string, CommandInfo>();
        List<CommandArg> arguments = new List<CommandArg>(); // Cache for performance

        public string IssuedErrorMessage { get; private set; }

        public Dictionary<string, CommandInfo> Commands {
            get { return commands; }
        }

        /// <summary>
        /// Uses reflection to find all RegisterCommand attributes
        /// and adds them to the commands dictionary.
        /// </summary>
        public void RegisterCommands() {
            var rejected_commands = new Dictionary<string, CommandInfo>();
            var method_flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var type in Assembly.GetExecutingAssembly().GetTypes()) {
                foreach (var method in type.GetMethods(method_flags)) {
                    var attribute = Attribute.GetCustomAttribute(
                        method, typeof(RegisterCommandAttribute)) as RegisterCommandAttribute;

                    if (attribute == null) {
                        continue;
                    }

                    string command_name = method.Name;

                    if (attribute.Name != null) {
                        command_name = attribute.Name;
                    }

                   AddCommand(command_name, method, attribute.Help);
                }
            }
        }

        /// <summary>
        /// Parses an input line into a command and runs that command.
        /// </summary>
        public void RunCommand(string line) {
            string remaining = line;
            IssuedErrorMessage = null;
            arguments.Clear();

            while (remaining != "") {
                var argument = EatArgument(ref remaining);

                if (argument.String != "") {
                    arguments.Add(argument);
                }
            }

            if (arguments.Count == 0) {
                // Nothing to run
                return;
            }

            string command_name = arguments[0].String.ToLower();
            arguments.RemoveAt(0); // Remove command name from arguments

            if (!commands.ContainsKey(command_name)) {
                IssueErrorMessage("Command {0} could not be found", command_name);
                return;
            }

            CommandInfo command = commands[command_name];
            object[] paramters = command.ParseParameters(arguments.ToArray());
            if (Terminal.IssuedError)
            {
                return;
            }

            command.Invoke(paramters);
        }

        public void AddCommand(string name, CommandInfo info) {
            name = name.ToLower();

            if (commands.ContainsKey(name)) {
                IssueErrorMessage("Command {0} is already defined.", name);
                return;
            }

            commands.Add(name, info);
        }

        public void AddCommand(string name, MethodInfo methodInfo, string help = "") {
            if (commands.ContainsKey(name))
            {
                IssueErrorMessage("Command {0} is already defined.", name);
                return;
            }

            ParameterInfo[] parameterInfos = methodInfo.GetParameters();
            bool isArrayArgs = false;

            if(parameterInfos.Length == 1 && parameterInfos[0].ParameterType == typeof(CommandArg[]))
            {
                isArrayArgs = true;
            }

            CommandInfo.ArgType[] argTypes = null;
            if (!isArrayArgs)
            {
                argTypes = new CommandInfo.ArgType[parameterInfos.Length];
                for (int i = 0; i < parameterInfos.Length; ++i)
                {
                    argTypes[i] = CommandInfo.ParseType(parameterInfos[i].ParameterType);
                }
            }

            var info = new CommandInfo() {
                argTypes = argTypes,
                methodInfo = methodInfo,
                isArrayArgs = isArrayArgs,
                help = help
            };

            AddCommand(name, info);
        }

        public void IssueErrorMessage(string format, params object[] message) {
            IssuedErrorMessage = string.Format(format, message);
        }

        string InferCommandName(string method_name) {
            string command_name;
            int index = method_name.IndexOf("COMMAND", StringComparison.CurrentCultureIgnoreCase);

            if (index >= 0) {
                // Method is prefixed, suffixed with, or contains "COMMAND".
                command_name = method_name.Remove(index, 7);
            } else {
                command_name = method_name;
            }

            return command_name;
        }

        string InferFrontCommandName(string method_name) {
            int index = method_name.IndexOf("FRONT", StringComparison.CurrentCultureIgnoreCase);
            return index >= 0 ? method_name.Remove(index, 5) : null;
        }

        CommandArg EatArgument(ref string s) {
            var arg = new CommandArg();
            int space_index = s.IndexOf(' ');

            if (space_index >= 0) {
                arg.String = s.Substring(0, space_index);
                s = s.Substring(space_index + 1); // Remaining
            } else {
                arg.String = s;
                s = "";
            }

            return arg;
        }
    }
}
