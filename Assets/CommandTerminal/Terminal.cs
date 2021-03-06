using UnityEngine;
using System.Text;
using System.Collections;
using UnityEngine.Assertions;

namespace CommandTerminal
{
    public enum TerminalState
    {
        Close = 0,
        ShowLog = 0x01,  //只显示日志信息
        ShowInput = 0x10,  //显示输入
        CompleteShow = 0x11
    }

    public class Terminal : MonoBehaviour
    {
        [Header("Window")]
        [Range(0, 1)]
        [SerializeField]
        float MaxHeight = 1f;

        [Range(0, 1)]
        [SerializeField]
        float MiniHeight = 0.3f;

        [SerializeField] string ShowLogHotkey      = "#`";
        [SerializeField] string ShowInputHotkey  = "`";
        [SerializeField] int BufferSize           = 512;

        [Header("Input")]
        [SerializeField] Font ConsoleFont;
        [SerializeField] string InputCaret        = ">";

        [Header("Theme")]
        [Range(0, 1)]
        [SerializeField] float InputContrast;

        [SerializeField] Color BackgroundColor    = Color.black;
        [SerializeField] Color ForegroundColor    = Color.white;
        [SerializeField] Color ShellColor         = Color.white;
        [SerializeField] Color InputColor         = Color.cyan;
        [SerializeField] Color WarningColor       = Color.yellow;
        [SerializeField] Color ErrorColor         = Color.red;

        TerminalState state;
        TextEditor editor_state;
        bool input_fix;
        bool move_cursor;
        bool initial_open; // Used to focus on TextField when console opens
        Rect window;
        float open_target;
        float real_window_size;
        string command_text;
        string cached_command_text;
        Vector2 scroll_position;
        GUIStyle window_style;
        GUIStyle label_style;
        GUIStyle input_style;

        public static CommandLog Buffer { get; private set; }
        public static CommandShell Shell { get; private set; }
        public static CommandHistory History { get; private set; }
        public static CommandAutocomplete Autocomplete { get; private set; }

        public static bool IssuedError {
            get { return Shell.IssuedErrorMessage != null; }
        }

        public bool IsClosed {
            get { return state == TerminalState.Close; }
        }

        public bool IsShowInput
        {
            get
            {
                return state >= TerminalState.ShowInput;
            }
        }

        public static void Log(string format, params object[] message) {
            Log(TerminalLogType.ShellMessage, format, message);
        }

        public static void Log(TerminalLogType type, string format, params object[] message) {
            Buffer.HandleLog(string.Format(format, message), type);
        }

        public bool IsStateEnable(TerminalState new_state)
        {
            return (state & new_state) > TerminalState.Close;
        }

        public void SetState(TerminalState new_state, bool enable) {
            input_fix = true;
            cached_command_text = command_text;
            command_text = "";
            if(new_state != TerminalState.Close)
            {
                if (enable)
                {
                    new_state = state | new_state;
                }
                else
                {
                    new_state = state & (~new_state);
                }
            }

            switch (new_state) {
                case TerminalState.Close: {
                    open_target = 0;
                    break;
                }
                case TerminalState.ShowLog:
                {
                    real_window_size = Screen.height * MiniHeight;
                    open_target = real_window_size;
                    scroll_position.y = int.MaxValue;
                    break;
                }
                case TerminalState.ShowInput:
                case TerminalState.CompleteShow:
                default: {
                    real_window_size = Screen.height * MaxHeight;
                    open_target = real_window_size;
                    scroll_position.y = int.MaxValue;
                    break;
                }
            }
            state = new_state;
            RefreshBackgroundColor();
        }

        public void ToggleState(TerminalState new_state) {
            SetState(new_state, !IsStateEnable(new_state));
        }

        void OnEnable() {
            Buffer = new CommandLog(BufferSize);
            Shell = new CommandShell();
            History = new CommandHistory();
            Autocomplete = new CommandAutocomplete();

            // Hook Unity log events
            Application.logMessageReceived += HandleUnityLog;
        }

        void OnDisable() {
            Application.logMessageReceived -= HandleUnityLog;
        }

        void Start() {
            if (ConsoleFont == null) {
                ConsoleFont = Font.CreateDynamicFontFromOSFont("Courier New", 16);
                Debug.LogWarning("Command Console Warning: Please assign a font.");
            }

            command_text = "";
            cached_command_text = command_text;
            Assert.AreNotEqual(ShowLogHotkey.ToLower(), "return", "Return is not a valid ToggleHotkey");

            SetupWindow();
            SetupInput();
            SetupLabels();

            Shell.RegisterCommands();

            if (IssuedError) {
                Log(TerminalLogType.Error, "Error: {0}", Shell.IssuedErrorMessage);
            }

            foreach (var command in Shell.Commands) {
                Autocomplete.Register(command.Key);
            }
        }

        void OnGUI() {
            if (Event.current.Equals(Event.KeyboardEvent(ShowLogHotkey))) {
                ToggleState(TerminalState.ShowLog);
                initial_open = true;
            } else if (Event.current.Equals(Event.KeyboardEvent(ShowInputHotkey))) {
                if (IsClosed)
                {
                    ToggleState(TerminalState.ShowLog);
                }
                ToggleState(TerminalState.ShowInput);
                initial_open = true;
            }

            if (IsClosed) {
                return;
            }

            window = new Rect(0, open_target - real_window_size, Screen.width, real_window_size);
            window = GUILayout.Window(88, window, DrawConsole, "", window_style);
        }

        void RefreshBackgroundColor()
        {
            // Set background color
            Texture2D background_texture = new Texture2D(1, 1);
            Color realBgColor = BackgroundColor;
            realBgColor.a = IsStateEnable(TerminalState.ShowInput) ? 1f : 0f;
            background_texture.SetPixel(0, 0, realBgColor);
            background_texture.Apply();

            window_style = new GUIStyle();
            window_style.normal.background = background_texture;
            window_style.padding = new RectOffset(4, 4, 4, 4);
            window_style.normal.textColor = ForegroundColor;
            window_style.font = ConsoleFont;
        }

        void SetupWindow() {
            real_window_size = Screen.height * MaxHeight / 3;
            window = new Rect(0, open_target - real_window_size, Screen.width, real_window_size);

            RefreshBackgroundColor();
        }

        void SetupLabels() {
            label_style = new GUIStyle();
            label_style.font = ConsoleFont;
            label_style.normal.textColor = ForegroundColor;
            label_style.wordWrap = true;
        }

        void SetupInput() {
            input_style = new GUIStyle();
            input_style.padding = new RectOffset(4, 4, 4, 4);
            input_style.font = ConsoleFont;
            input_style.fixedHeight = ConsoleFont.fontSize * 1.6f;
            input_style.normal.textColor = InputColor;

            var dark_background = new Color();
            dark_background.r = BackgroundColor.r - InputContrast;
            dark_background.g = BackgroundColor.g - InputContrast;
            dark_background.b = BackgroundColor.b - InputContrast;
            dark_background.a = 0.5f;

            Texture2D input_background_texture = new Texture2D(1, 1);
            input_background_texture.SetPixel(0, 0, dark_background);
            input_background_texture.Apply();
            input_style.normal.background = input_background_texture;
        }

        void DrawConsole(int Window2D) {
            GUILayout.BeginVertical();

            scroll_position = GUILayout.BeginScrollView(scroll_position, false, false, GUIStyle.none, GUIStyle.none);
            GUILayout.FlexibleSpace();
            DrawLogs();
            GUILayout.EndScrollView();

            if (move_cursor) {
                CursorToEnd();
                move_cursor = false;
            }
            if (Event.current.Equals(Event.KeyboardEvent("escape"))) {
                SetState(TerminalState.ShowInput, false);
            } else if (Event.current.Equals(Event.KeyboardEvent("return"))) {
                EnterCommand();
            } else if (Event.current.Equals(Event.KeyboardEvent("up"))) {
                command_text = History.Previous();
                move_cursor = true;
            } else if (Event.current.Equals(Event.KeyboardEvent("down"))) {
                command_text = History.Next();
//             } else if (Event.current.Equals(Event.KeyboardEvent(ShowLogHotkey))) {
//                 ToggleState(TerminalState.ShowLog);
//             } else if (Event.current.Equals(Event.KeyboardEvent(ShowInputHotkey))) {
//                 if (IsClosed)
//                 {
//                     ToggleState(TerminalState.ShowLog);
//                 }
//                 ToggleState(TerminalState.ShowInput);
            } else if (Event.current.Equals(Event.KeyboardEvent("tab"))) {
                CompleteCommand();
                move_cursor = true; // Wait till next draw call
            }

            

            if (IsShowInput)
            {
                GUILayout.BeginHorizontal();
                if (InputCaret != "")
                {
                    GUILayout.Label(InputCaret, input_style, GUILayout.Width(ConsoleFont.fontSize));
                }

                GUI.SetNextControlName("command_text_field");
                command_text = GUILayout.TextField(command_text, input_style);

                if (input_fix && command_text.Length > 0)
                {
                    command_text = cached_command_text; // Otherwise the TextField picks up the ToggleHotkey character event
                    input_fix = false;                  // Prevents checking string Length every draw call
                }

                if (initial_open)
                {
                    GUI.FocusControl("command_text_field");
                    initial_open = false;
                }

                GUILayout.EndHorizontal();
            }

            
            GUILayout.EndVertical();
        }

        void DrawLogs() {
            foreach (var log in Buffer.Logs) {
                label_style.normal.textColor = GetLogColor(log.type);
                GUILayout.Label(log.message, label_style);
            }
        }

        void EnterCommand() {
            Log(TerminalLogType.Input, "{0}", command_text);
            Shell.RunCommand(command_text);
            History.Push(command_text);

            if (IssuedError) {
                Log(TerminalLogType.Error, "Error: {0}", Shell.IssuedErrorMessage);
            }

            command_text = "";
            scroll_position.y = int.MaxValue;
        }

        void CompleteCommand() {
            string head_text = command_text;
            string[] completion_buffer = Autocomplete.Complete(ref head_text);
            int completion_length = completion_buffer.Length;

            if (completion_length == 1) {
                command_text = head_text + completion_buffer[0];
            } else if (completion_length > 1) {
                // Print possible completions
                Log(string.Join("    ", completion_buffer));
                scroll_position.y = int.MaxValue;
            }
        }

        void CursorToEnd() {
            if (editor_state == null) {
                editor_state = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
            }

            editor_state.MoveCursorToPosition(new Vector2(999, 999));
        }

        void HandleUnityLog(string message, string stack_trace, LogType type) {
            Buffer.HandleLog(message, stack_trace, (TerminalLogType)type);
            scroll_position.y = int.MaxValue;
        }

        Color GetLogColor(TerminalLogType type) {
            switch (type) {
                case TerminalLogType.Message: return ForegroundColor;
                case TerminalLogType.Warning: return WarningColor;
                case TerminalLogType.Input: return InputColor;
                case TerminalLogType.ShellMessage: return ShellColor;
                default: return ErrorColor;
            }
        }
    }
}
