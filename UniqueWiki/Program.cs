using ArchConsole;
using SadConsole;
using SadConsole.Input;
using SadConsole.UI.Controls;
using SadRogue.Primitives;
using System.Text;
using System.Text.RegularExpressions;
using static SadConsole.ColoredString;

Settings.WindowTitle = "UniqueWiki";
Game.Create(100, 60, "IBMCGA+.font");
Game.Instance.OnStart = () => {
    Game.Instance.Screen = new EditorScreen(100, 60) { IsFocused = true };
};
Game.Instance.Run();

class EditorScreen : SadConsole.Console {
    public string currentFile;
    public Dictionary<string, TextEditor> editors=new();
    public EditorScreen(int w, int h) : base(w, h) {
        ResetUI();
    }
    public void SetFile(string file) {

        if (!File.Exists(file)) {
            return;
        }
        currentFile = file;
        ResetUI();
    }
    public void ResetUI() {
        Children.Clear();
        Children.Add(new TextField(Width - 2) {
            Position = new(1, 1),
            text = currentFile ?? Environment.CurrentDirectory,
            EnterPressed = t => {
                SetFile(t.text);
            }
        });
        Children.Add(new NavMenu(16, Height-3, this) {
            Position = new(1, 3)
        });
        if(currentFile == null) {

        } else if (editors.TryGetValue(currentFile, out var te)) {
            Children.Add(te);
        } else {
            Children.Add(editors[currentFile] = new(Width - 20, Height - 3, currentFile, this) {
                Position = new(20, 3)
            });
        }
    }
}

class NavMenu : SadConsole.Console {

    EditorScreen main;
    public NavMenu(int w, int h, EditorScreen editor) : base(w, h) {
        this.main = editor;
        FocusOnMouseClick = true;
    }
    int yMouse;
    bool prevMouseDown;
    public override bool ProcessMouse(MouseScreenObjectState state) {

        if (state.IsOnScreenObject) {
            yMouse = state.SurfaceCellPosition.Y;

            var mouseDown = state.Mouse.LeftButtonDown;
            if(prevMouseDown && !mouseDown && yMouse < main.editors.Count) {
                main.SetFile(main.editors.Keys.ElementAt(yMouse));
            }

            prevMouseDown = mouseDown;
        } else {
            yMouse = -1;
        }
        return base.ProcessMouse(state);
    }
    public override void Render(TimeSpan delta) {
        this.Clear();
        var y = 0;
        foreach(var f in main.editors.Keys) {
            var str = Path.GetFileName(f);
            
            str = str.Length > Width ? str.Substring(0, Width) : str;
            var cur = f == main.currentFile;

            if(y == yMouse) {
                if(prevMouseDown) {
                    this.Print(0, y++, str, Color.Black, Color.Yellow);
                } else {
                    this.Print(0, y++, str, cur ? Color.Yellow : Color.White, new Color(102, 102, 102));
                }
            } else {
                this.Print(0, y++, str, cur ? Color.Yellow : Color.White, Color.Black);
            }


            
        }
        base.Render(delta);
    }
}
class TextEditor : SadConsole.Console {

    public string file;
    public int cursor, columnMemory;
    public StringBuilder s = new();

    EditorScreen editor;
    public TextEditor(int w, int h, string file, EditorScreen editor) : base(w, h) {
        UseKeyboard = true;
        FocusOnMouseClick = true;
        DefaultBackground = Color.Black;
        this.file = file;
        this.editor = editor;

        s = new(File.ReadAllText(file));
        textChanged = true;
    }

    public bool textChanged = false;
    public Dictionary<string, HashSet<Point>> buttons = new();
    public Dictionary<Point, string> labelField = new();



    public bool prevRightDown = false;
    public string hoveredLabel;
    public override void Update(TimeSpan delta) {
        if(!textChanged) {
            return;
        }
        textChanged = false;

        buttons.Clear();
        labelField.Clear();

        int row = 0;
        int col = 0;

        StringBuilder label = null;
        HashSet<Point> labelPoints = new();
        foreach(var c in s.ToString()) {
            if(c == '\n') {
                col = 0;
                row++;
                continue;
            }
            
            if(c == '[') {
                label = new();
                labelPoints = new();
                labelPoints.Add(new(col, row));
            } else if(c == ']') {
                if(label != null) {
                    labelPoints.Add(new(col, row));

                    var l = label.ToString();
                    buttons[l] = labelPoints;
                    foreach(var p in labelPoints) {
                        labelField[p] = l;
                    }
                }
                label = null;
            } else if(label != null) {
                label.Append(c);
                labelPoints.Add(new(col, row));
            }
            col++;
        }
        base.Update(delta);
    }
    public override bool ProcessMouse(MouseScreenObjectState state) {
        if(labelField.TryGetValue(state.SurfaceCellPosition, out var l)) {
            hoveredLabel = l;

            var rightDown = state.Mouse.RightButtonDown;
            if (prevRightDown && !rightDown) {

                var m = Regex.Match(hoveredLabel, "(?<file>[^#]+)(#(?<section>.+))?");
                var file = m.Groups["file"].Value;
                var section = m.Groups["section"].Value;

                file = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(this.file), file));
                if (File.Exists(file)) {
                    editor.SetFile(file);
                    hoveredLabel = null;
                }
            }
            prevRightDown = rightDown;
        } else {
            hoveredLabel = null;
        }
        return base.ProcessMouse(state);
    }
    public override bool ProcessKeyboard(Keyboard keyboard) {
        bool ctrl = keyboard.IsKeyDown(Keys.LeftControl);

        foreach(var k in keyboard.KeysPressed) {
            switch (k.Key) {
                case Keys.Escape: {
                        break;
                    }
                case Keys.Home: {
                    Home:
                        if (FindPrevLine(out int index)) {
                            cursor = index;
                        } else {
                            cursor = 0;
                        }
                        columnMemory = CountColumn();
                        break;
                    }
                case Keys.End: {
                        if (FindNextLine(out int index)) {
                            cursor = index;
                        } else {
                            cursor = 0;
                        }
                        columnMemory = CountColumn();
                        break;
                    }
                case Keys.Left: {
                    LeftArrow:
                        if (cursor > 0) {
                            cursor--;
                            if (ctrl && s[cursor] != ' ') {
                                goto LeftArrow;
                            }
                        }
                        columnMemory = CountColumn();
                        break;
                    }
                case Keys.Right: {
                    RightArrow:
                        if (cursor < s.Length - 1 && ctrl && s[cursor + 1] != ' ') {
                            cursor++;
                            goto RightArrow;
                        } else if (cursor < s.Length) {
                            cursor++;
                        }
                        columnMemory = CountColumn();
                        break;
                    }
                case Keys.Up: {
                        if (FindPrevLine(out int index)) {
                            int column = Math.Min(columnMemory, CountLineLength(index));
                            cursor = index + column;
                        } else {
                            cursor = 0;
                        }
                        break;
                    }
                case Keys.Down: {
                        if (FindNextLine(out int index)) {
                            int column = Math.Min(columnMemory, CountLineLength(index));
                            cursor = index + column;
                        } else {
                            cursor = s.Length;
                        }
                        break;
                    }
                case Keys.Back: {

                        //Global.Break();
                        if (ctrl) {
                            //Make sure we have characters to delete
                            if (cursor == 0) {
                                break;
                            }
                            //If we are at a space, just delete it
                            if (s[cursor - 1] == ' ') {
                                cursor--;
                                s.Remove(cursor, 1);
                            } else {
                                //Otherwise, delete characters until we reach a space
                                int length = 0;
                                while (cursor > 0 && s[cursor - 1] != ' ') {
                                    cursor--;
                                    length++;
                                }
                                s.Remove(cursor, length);
                            }
                        } else {
                            if (s.Length > 0 && cursor > 0) {
                                cursor--;
                                s.Remove(cursor, 1);
                            }
                        }
                        columnMemory = CountColumn();
                        break;
                    }
                case Keys.Enter: {

                        if (cursor == s.Length) {
                            int indent = CountIndent();
                            s.Append("\n" + new string(' ', indent));
                            cursor++;
                            cursor += indent;
                        } else {
                            int indent = CountIndent();
                            s.Insert(cursor, "\n" + new string(' ', indent));
                            cursor++;
                            cursor += indent;
                        }
                        columnMemory = CountColumn();
                        break;
                    }
                case Keys.Tab: {

                        if (ctrl) {
                            break;
                        }
                        if (cursor == s.Length) {
                            s.Append("    ");
                        } else {
                            s.Insert(cursor, "    ");
                        }
                        cursor += 4;
                        columnMemory = CountColumn();
                        break;
                    }
                default: {
                        //Don't type if we are doing a keyboard shortcut
                        if (ctrl) {
                            break;
                        }

                        if (k.Character != 0) {
                            if (cursor == s.Length) {
                                s.Append(k.Character);
                            } else {
                                s.Insert(cursor, k.Character);
                            }
                            cursor++;
                            columnMemory = CountColumn();
                            textChanged = true;
                        }
                        break;
                    }
            }
        }

        return base.ProcessKeyboard(keyboard);
    }
    int CountColumn() {
        int count = 0;
        int index = cursor - 1;
        while (index > -1 && s[index] != '\n') {
            index--;
            count++;
        }
        return count;
    }
    int CountIndent() {
        int index = cursor - 1;
        int indent = 0;
        while (index > -1) {
            switch (s[index]) {
                case ' ':
                    indent++;
                    break;
                case '\n':
                    return indent;
                default:
                    indent = 0;
                    break;
            }
            index--;
        }
        return indent;
    }
    bool FindPrevLine(out int index) {
        index = Math.Min(cursor, s.Length - 1);
        if (index > -1) {
            index--;
        }
        while (index > -1 && s[index] != '\n') {
            index--;
        }
        if (index == -1) {
            return false;
        }
        index--;

        while (index > -1 && s[index] != '\n') {
            index--;
        }
        index++;
        return true;
    }
    bool FindNextLine(out int index) {
        if (s.Length == 0) {
            index = 0;
            return false;
        }
        index = Math.Min(cursor, s.Length - 1);
        while (index < s.Length && s[index] != '\n') {
            index++;
        }
        if (index != s.Length) {
            //Return index of first char of line
            index++;
            return true;
        } else {
            return false;
        }
    }
    int CountLineLength(int index) {
        int count = 0;
        while (index < s.Length && s[index] != '\n') {
            index++;
            count++;
        }
        return count;
    }
    public override void Render(TimeSpan delta) {

        var back = new Color(0, 0, 0);
        var highlight = Color.Yellow;
        var fore = Color.White;
        var cursorSpace = new ColoredGlyph(back, highlight, ' ');
        var buffer = new List<ColoredString>();

        int width = Width;
        var line = new ColoredString(width);
        int index = 0;
        int length = 0;
        foreach (var ch in s.ToString()) {
            if (ch == '\n') {
                buffer.Add(line.SubString(0, length));
                line = new(width);
                if (cursor == index) {
                    buffer[buffer.Count - 1] += new ColoredString(cursorSpace);
                }
                index++;
                length = 0;
                continue;
            }

            line[length] = 
                index == cursor ?
                    new() { Foreground = back, Background = highlight, Glyph = ch } :
                    new() { Foreground = fore, Background = back, Glyph = ch };
            index++;
            length++;
            if (length == width) {
                buffer.Add(line);
                line = new(width);
                length = 0;
            }
        }
        if (length > 0) {
            buffer.Add(line.SubString(0, length));
        }
        if (index == cursor) {
            if (length == 0) {
                buffer.Add(new(cursorSpace));
            } else {
                buffer[buffer.Count - 1] += new ColoredString(cursorSpace);
            }
        }

        var y = 0;

        this.Clear();
        foreach(var l in buffer) {
            this.Print(0, y++, l);
        }

        if(hoveredLabel != null) {
            if (prevRightDown) {
                foreach (var p in buttons[hoveredLabel]) {
                    this.SetForeground(p.X, p.Y, Color.Black);
                    this.SetBackground(p.X, p.Y, Color.White);

                }
            } else {
                foreach (var p in buttons[hoveredLabel]) {
                    this.SetBackground(p.X, p.Y, new Color(102, 102, 102));
                }
            }
        }

        base.Render(delta);
    }
}