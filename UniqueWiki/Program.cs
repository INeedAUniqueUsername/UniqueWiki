using ArchConsole;
using SadConsole;
using SadConsole.Input;
using SadConsole.UI.Controls;
using SadRogue.Primitives;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using static SadConsole.ColoredString;

Settings.WindowTitle = "UniqueWiki";
Game.Create(100, 60, "IBMCGA+.font");
Game.Instance.OnStart = () => {
    Game.Instance.Screen = new EditorScreen(100, 60) { IsFocused = true };
};
Game.Instance.Run();

public static class Common {
    public static string GetSHA256(this string text) {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        using (var sha = SHA256.Create()) {
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "");
        }
    }
}
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
        foreach((var f, var editor) in main.editors) {
            var str = Path.GetFileName(f);

            str = $"{(str.Length > Width - 1 ? str.Substring(0, Width - 1) : str)}{(editor.unsaved ? "*" : "")}";
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
    public int cursorRawIndex, cursorVisibleIndex, columnMemory;
    public StringBuilder raw = new(), printed = new();

    EditorScreen editor;
    public TextEditor(int w, int h, string file, EditorScreen editor) : base(w, h) {
        UseKeyboard = true;
        FocusOnMouseClick = true;
        DefaultBackground = Color.Black;
        this.file = file;
        this.editor = editor;

        var content = File.ReadAllText(file);
        raw = new(content);
        textChanged = true;
    }

    public bool textChanged = false, needRefresh = false;
    public Dictionary<string, HashSet<Point>> links = new();
    public Dictionary<Point, string> linkMap = new();

    public DateTime lastChecked = DateTime.Now;
    public DateTime lastSaved = DateTime.Now;
    public DateTime lastChanged = DateTime.Now;
    public bool prevRightDown = false;
    public string hoveredLink;
    public bool unsaved;

    public void CheckUnsaved() {
        unsaved = File.GetLastWriteTime(file) > lastSaved || lastChanged > lastSaved;
        lastChecked = DateTime.Now;
    }
    public override void Update(TimeSpan delta) {
        if (!textChanged) {
            if ((DateTime.Now - lastChecked).TotalSeconds > 1) {
                CheckUnsaved();

                if(unsaved && File.ReadAllText(file).GetSHA256() == raw.ToString().GetSHA256()) {
                    unsaved = false;
                    lastSaved = DateTime.Now;
                }
            }
            return;
        }
        /*
        if ((DateTime.Now - lastChecked).TotalSeconds > 0.25) {
            
        }
        */
        textChanged = false;
        lastChanged = DateTime.Now;

        CheckUnsaved();

        hoveredLink = null;
        links.Clear();
        linkMap.Clear();

        cursorVisibleIndex = cursorRawIndex;

        int row = 0;
        int col = 0;

        printed.Clear();

        int i = 0;
        while(i < raw.Length) {
            var c = raw[i];
            if (c == '\n') {
                printed.Append(c);
                col = 0;
                row++;
                i++;
                continue;
            }

            if (c == '[' && Regex.Match(raw.ToString().Substring(i), "\\[\\[(?<link>[^\\|\\[\\]]+)(\\|(?<label>[^\\]]+))?\\]\\]") is Match {Success:true } m) {
                var link = m.Groups["link"].Value;
                var label = m.Groups["label"].Value;
                var buttonPoints = new HashSet<Point>();
                string visible;
                if(label.Length > 0) {
                    var l = link.Length;
                    if (cursorRawIndex >= i + 2) {
                        if (cursorRawIndex < i + 2 + l + 1 + 1) {
                            visible = m.Value;
                        } else {
                            cursorVisibleIndex -= l + 1;
                            visible = $"[[{label}]]";
                        }
                    } else {
                        visible = $"[[{label}]]";
                    }
                } else {
                    visible = $"[[{link}]]";
                }
                foreach (var ch in visible) {
                    //to do: handle newlines
                    printed.Append(ch);
                    buttonPoints.Add(new(col, row));
                    col++;
                }
                links[link] = buttonPoints;
                foreach (var p in buttonPoints) {
                    linkMap[p] = link;
                }
                i += m.Length;
                continue;
            }

            printed.Append(c);
            col++;
            i++;
        }
        base.Update(delta);
    }
    public override bool ProcessMouse(MouseScreenObjectState state) {
        if(linkMap.TryGetValue(state.SurfaceCellPosition, out var l)) {
            hoveredLink = l;

            var rightDown = state.Mouse.LeftButtonDown;
            if (prevRightDown && !rightDown) {

                var m = Regex.Match(hoveredLink, "(?<file>[^#]+)(#(?<section>.+))?");
                var file = m.Groups["file"].Value;
                var section = m.Groups["section"].Value;

                file = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(this.file), file));
                if (File.Exists(file)) {
                    editor.SetFile(file);
                    hoveredLink = null;
                } else {
                    File.Create(file);
                    editor.SetFile(file);
                    hoveredLink = null;
                }
            }
            prevRightDown = rightDown;
        } else {
            hoveredLink = null;
        }
        return base.ProcessMouse(state);
    }
    public void Save() {
        unsaved = false;
        File.WriteAllText(file, raw.ToString());
        lastSaved = DateTime.Now;
    }
    public override bool ProcessKeyboard(Keyboard keyboard) {
        bool ctrl = keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl);

        foreach(var k in keyboard.KeysPressed) {
            switch (k.Key) {
                case Keys.S when ctrl:
                    Save();
                    break;
                case Keys.Escape: {
                        break;
                    }
                case Keys.Home: {
                    Home:
                        if (FindPrevLine(out int index)) {
                            cursorRawIndex = index;
                        } else {
                            cursorRawIndex = 0;
                        }
                        columnMemory = CountColumn();
                        break;
                    }
                case Keys.End: {
                        if (FindNextLine(out int index)) {
                            cursorRawIndex = index;
                        } else {
                            cursorRawIndex = 0;
                        }
                        columnMemory = CountColumn();
                        break;
                    }
                case Keys.Left: {
                    LeftArrow:
                        if (cursorRawIndex > 0) {
                            cursorRawIndex--;
                            if (ctrl && raw[cursorRawIndex] != ' ') {
                                goto LeftArrow;
                            }
                        }
                        columnMemory = CountColumn();
                        textChanged = true;
                        break;
                    }
                case Keys.Right: {
                    RightArrow:
                        if (cursorRawIndex < raw.Length - 1 && ctrl && raw[cursorRawIndex + 1] != ' ') {
                            cursorRawIndex++;
                            goto RightArrow;
                        } else if (cursorRawIndex < raw.Length) {
                            cursorRawIndex++;
                        }
                        columnMemory = CountColumn();
                        textChanged = true;
                        break;
                    }
                case Keys.Up: {
                        if (FindPrevLine(out int index)) {
                            int column = Math.Min(columnMemory, CountLineLength(index));
                            cursorRawIndex = index + column;
                        } else {
                            cursorRawIndex = 0;
                        }
                        textChanged = true;
                        break;
                    }
                case Keys.Down: {
                        if (FindNextLine(out int index)) {
                            int column = Math.Min(columnMemory, CountLineLength(index));
                            cursorRawIndex = index + column;
                        } else {
                            cursorRawIndex = raw.Length;
                        }
                        textChanged = true;
                        break;
                    }
                case Keys.Back: {
                        hoveredLink = null;
                        //Global.Break();
                        if (ctrl) {
                            //Make sure we have characters to delete
                            if (cursorRawIndex == 0) {
                                break;
                            }
                            //If we are at a space, just delete it
                            if (raw[cursorRawIndex - 1] == ' ') {
                                cursorRawIndex--;
                                raw.Remove(cursorRawIndex, 1);
                            } else {
                                //Otherwise, delete characters until we reach a space
                                int length = 0;
                                while (cursorRawIndex > 0 && raw[cursorRawIndex - 1] != ' ') {
                                    cursorRawIndex--;
                                    length++;
                                }
                                raw.Remove(cursorRawIndex, length);
                            }
                        } else {
                            if (raw.Length > 0 && cursorRawIndex > 0) {
                                cursorRawIndex--;
                                raw.Remove(cursorRawIndex, 1);
                            }
                        }
                        columnMemory = CountColumn();
                        textChanged = true;
                        break;
                    }
                case Keys.Enter: {

                        if (cursorRawIndex == raw.Length) {
                            int indent = CountIndent();
                            raw.Append("\n" + new string(' ', indent));
                            cursorRawIndex++;
                            cursorRawIndex += indent;
                        } else {
                            int indent = CountIndent();
                            raw.Insert(cursorRawIndex, "\n" + new string(' ', indent));
                            cursorRawIndex++;
                            cursorRawIndex += indent;
                        }
                        columnMemory = CountColumn();
                        textChanged = true;
                        break;
                    }
                case Keys.Tab: {

                        if (ctrl) {
                            break;
                        }
                        if (cursorRawIndex == raw.Length) {
                            raw.Append("    ");
                        } else {
                            raw.Insert(cursorRawIndex, "    ");
                        }
                        cursorRawIndex += 4;
                        columnMemory = CountColumn();
                        textChanged = true;
                        break;
                    }
                default: {
                        //Don't type if we are doing a keyboard shortcut
                        if (ctrl) {
                            break;
                        }

                        if (k.Character != 0) {
                            if (cursorRawIndex == raw.Length) {
                                raw.Append(k.Character);
                            } else {
                                raw.Insert(cursorRawIndex, k.Character);
                            }
                            cursorRawIndex++;
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
        int index = cursorRawIndex - 1;
        while (index > -1 && raw[index] != '\n') {
            index--;
            count++;
        }
        return count;
    }
    int CountIndent() {
        int index = cursorRawIndex - 1;
        int indent = 0;
        while (index > -1) {
            switch (raw[index]) {
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
        index = Math.Min(cursorRawIndex, raw.Length - 1);
        if (index > -1) {
            index--;
        }
        while (index > -1 && raw[index] != '\n') {
            index--;
        }
        if (index == -1) {
            return false;
        }
        index--;

        while (index > -1 && raw[index] != '\n') {
            index--;
        }
        index++;
        return true;
    }
    bool FindNextLine(out int index) {
        if (raw.Length == 0) {
            index = 0;
            return false;
        }
        index = Math.Min(cursorRawIndex, raw.Length - 1);
        while (index < raw.Length && raw[index] != '\n') {
            index++;
        }
        if (index != raw.Length) {
            //Return index of first char of line
            index++;
            return true;
        } else {
            return false;
        }
    }
    int CountLineLength(int index) {
        int count = 0;
        while (index < raw.Length && raw[index] != '\n') {
            index++;
            count++;
        }
        return count;
    }

    List<ColoredString> buffer = new();
    Dictionary<Point, int> selectField = new();
    public void UpdateBuffer() {
        var back = new Color(0, 0, 0);
        var highlight = Color.Yellow;
        var fore = Color.White;
        var cursorSpace = new ColoredGlyph(back, highlight, ' ');
        buffer.Clear();
        selectField.Clear();

        int width = Width;
        var line = new ColoredString(width);
        int index = 0;
        int col = 0;
        foreach (var ch in printed.ToString()) {
            if (ch == '\n') {
                buffer.Add(line.SubString(0, col));
                line = new(width);
                if (cursorVisibleIndex == index) {
                    buffer[buffer.Count - 1] += new ColoredString(cursorSpace);
                }
                index++;
                col = 0;
                continue;
            }

            line[col] =
                index == cursorVisibleIndex ?
                    new() { Foreground = back, Background = highlight, Glyph = ch } :
                    new() { Foreground = fore, Background = back, Glyph = ch };
            index++;
            col++;
            if (col == width) {
                buffer.Add(line);
                line = new(width);
                col = 0;
            }
        }
        if (col > 0) {
            buffer.Add(line.SubString(0, col));
        }
        if (index == cursorVisibleIndex) {
            if (col == 0) {
                buffer.Add(new(cursorSpace));
            } else {
                buffer[buffer.Count - 1] += new ColoredString(cursorSpace);
            }
        }
    }
    public override void Render(TimeSpan delta) {
        UpdateBuffer();
        var y = 0;

        this.Clear();
        foreach(var l in buffer) {
            this.Print(0, y++, l);
        }

        if(hoveredLink != null) {
            if (prevRightDown) {
                foreach (var p in links[hoveredLink]) {
                    this.SetForeground(p.X, p.Y, Color.Black);
                    this.SetBackground(p.X, p.Y, Color.White);

                }
            } else {
                foreach (var p in links[hoveredLink]) {
                    this.SetBackground(p.X, p.Y, new Color(102, 102, 102));
                }
            }
        }

        base.Render(delta);
    }
}