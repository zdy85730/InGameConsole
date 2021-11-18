using System.Text;
using System.Diagnostics;
using UnityEngine;

namespace CommandTerminal
{
    public static class BuiltinCommands
    {
        [RegisterCommand(Help = "Clears the Command Console")]
        static void Clear() 
        {
            Terminal.Buffer.Clear();
        }

        [RegisterCommand(Help = "Lists all Commands or displays help documentation of a Command")]
        static void Help(CommandArg[] args) 
        {
            string[] parameterHelps;
            if (args.Length == 0) {
                foreach (var pair in Terminal.Shell.Commands) {
                    Terminal.Log("{0}: {1}\n", pair.Key.PadRight(16), pair.Value.help);
                    parameterHelps = pair.Value.GetParamtersHelp();
                    foreach(string helpContent in parameterHelps)
                    {
                        Terminal.Log("{0}{1}\n", "".PadRight(17), helpContent);
                    }
                }
                return;
            }

            string command_name = args[0].String.ToLower();

            if (!Terminal.Shell.Commands.ContainsKey(command_name)) {
                Terminal.Shell.IssueErrorMessage("Command {0} could not be found.", command_name);
                return;
            }

            CommandInfo command = Terminal.Shell.Commands[command_name];
            if (string.IsNullOrWhiteSpace(command.help))
            {
                Terminal.Log("{0} does not provide any help documentation.", command_name);
            }
            else
            {
                Terminal.Log(command.help);
            }
            parameterHelps = command.GetParamtersHelp();
            foreach (string helpContent in parameterHelps)
            {
                Terminal.Log(helpContent);
            }
        }

        [RegisterCommand(Help = "Times the execution of a Command")]
        static void Time(CommandArg[] args) {
            var sw = new Stopwatch();
            string subCmd = JoinArguments(args);
            sw.Start();

            Terminal.Shell.RunCommand(subCmd);

            sw.Stop();
            Terminal.Log("Time: {0}ms", (double)sw.ElapsedTicks / 10000);
        }

        [RegisterCommand(Help = "Outputs message")]
        static void Print(CommandArg[] args) {
            Terminal.Log(JoinArguments(args));
        }

    #if DEBUG
        [RegisterCommand(Help = "Outputs the StackTrace of the previous message")]
        static void Trace(CommandArg[] args) {
            int log_count = Terminal.Buffer.Logs.Count;

            if (log_count - 2 <  0) {
                Terminal.Log("Nothing to trace.");
                return;
            }

            var log_item = Terminal.Buffer.Logs[log_count - 2];

            if (log_item.stack_trace == "") {
                Terminal.Log("{0} (no trace)", log_item.message);
            } else {
                Terminal.Log(log_item.stack_trace);
            }
        }
    #endif

        [RegisterCommand(Help = "Quits running Application")]
        static void Quit(CommandArg[] args) {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
        }

        [RegisterCommand(Help = "Add a and b")]
        public static void Add(int a, int b)
        {
            Terminal.Log("{0} + {1} = {2}", a, b, a + b);
        }

        static string JoinArguments(CommandArg[] args) {
            var sb = new StringBuilder();
            int arg_length = args.Length;

            for (int i = 0; i < arg_length; i++) {
                sb.Append(args[i].String);

                if (i < arg_length - 1) {
                    sb.Append(" ");
                }
            }

            return sb.ToString();
        }
    }
}
