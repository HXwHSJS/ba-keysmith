# gui.py
import tkinter as tk
from tkinter import ttk, messagebox, scrolledtext
import json
import threading
import keyboard
import ctypes
import time
from pynput import mouse, keyboard as pynput_keyboard
from mapper import KeyMapper
from utils import KEY_ALIASES, MOUSE_EVENTS, VK_CODES

# ---------- Windows API 用于输入法禁用 ----------
user32 = ctypes.windll.user32
imm32 = ctypes.windll.imm32

def disable_ime():
    imm32.ImmDisableIME(0)

def enable_ime():
    imm32.ImmDisableIME(-1)

def center_window(window, width=None, height=None):
    window.update_idletasks()
    w = width or window.winfo_width()
    h = height or window.winfo_height()
    parent = window.master
    if parent:
        x = parent.winfo_x() + (parent.winfo_width() - w) // 2
        y = parent.winfo_y() + (parent.winfo_height() - h) // 2
    else:
        x = (window.winfo_screenwidth() - w) // 2
        y = (window.winfo_screenheight() - h) // 2
    window.geometry(f"{w}x{h}+{x}+{y}")

def normalize_captured_key(key):
    try:
        key_name = key.char
    except AttributeError:
        key_name = str(key).replace('Key.', '').lower()

    special_chars = {
        ' ': 'space',
        '\t': 'tab',
        '\r': 'enter',
        '\n': 'enter',
    }
    if key_name in special_chars:
        return special_chars[key_name]
    if isinstance(key_name, str):
        return KEY_ALIASES.get(key_name.lower(), key_name.lower())
    return str(key_name).lower()

class ToolTip:
    def __init__(self, widget, text, delay=450):
        self.widget = widget
        self.text = text
        self.delay = delay
        self._after_id = None
        self._tip = None
        widget.bind('<Enter>', self._schedule, add='+')
        widget.bind('<Leave>', self._hide, add='+')
        widget.bind('<ButtonPress>', self._hide, add='+')

    def _schedule(self, event=None):
        self._cancel()
        self._after_id = self.widget.after(self.delay, self._show)

    def _cancel(self):
        if self._after_id:
            self.widget.after_cancel(self._after_id)
            self._after_id = None

    def _show(self):
        if self._tip or not self.text:
            return
        x = self.widget.winfo_rootx() + 18
        y = self.widget.winfo_rooty() + self.widget.winfo_height() + 8
        self._tip = tk.Toplevel(self.widget)
        self._tip.wm_overrideredirect(True)
        self._tip.wm_geometry(f"+{x}+{y}")
        label = tk.Label(
            self._tip,
            text=self.text,
            bg='#111827',
            fg='#f8fafc',
            padx=10,
            pady=6,
            font=('Microsoft YaHei UI', 9),
            relief='flat'
        )
        label.pack()

    def _hide(self, event=None):
        self._cancel()
        if self._tip:
            self._tip.destroy()
            self._tip = None

# ---------- 按键捕获弹窗 ----------
class KeyCaptureDialog:
    def __init__(self, parent, callback, capture_mouse=True, on_close=None):
        self.parent = parent
        self.top = tk.Toplevel(parent)
        self.top.title("按下任意键或鼠标按键")
        self.top.geometry("350x180")
        self.top.resizable(False, False)
        self.top.transient(parent)
        self.top.grab_set()
        self.callback = callback
        self.on_close = on_close
        self.captured_key = None
        self.capture_mouse = capture_mouse
        self._closed = False

        label = ttk.Label(self.top, text="请按下要绑定的按键...\n（支持键盘按键、鼠标左/右/中/侧键）",
                          font=("微软雅黑", 11), justify='center')
        label.pack(expand=True, pady=20)

        self.listener_keyboard = None
        self.listener_mouse = None

        disable_ime()
        self.top.protocol("WM_DELETE_WINDOW", self._on_cancel)
        self.start_listeners()
        center_window(self.top)

    def start_listeners(self):
        def on_press(key):
            self.captured_key = normalize_captured_key(key)
            self.top.after(10, self._close)

        def on_click(x, y, button, pressed):
            if pressed and self.capture_mouse:
                btn_name = str(button).replace('Button.', '').lower()
                if btn_name == 'left':
                    self.captured_key = 'mouse_left'
                elif btn_name == 'right':
                    self.captured_key = 'mouse_right'
                elif btn_name == 'middle':
                    self.captured_key = 'mouse_middle'
                elif btn_name in ('x1', 'x2'):
                    self.captured_key = f'mouse_{btn_name}'
                else:
                    self.captured_key = btn_name
                self.top.after(10, self._close)
                return False

        self.listener_keyboard = pynput_keyboard.Listener(on_press=on_press)
        self.listener_keyboard.start()
        if self.capture_mouse:
            self.listener_mouse = mouse.Listener(on_click=on_click)
            self.listener_mouse.start()

    def _close(self):
        if self._closed:
            return
        self._closed = True
        if self.captured_key:
            self.callback(self.captured_key)
        self.top.grab_release()
        self.top.destroy()
        self.stop_listeners()
        enable_ime()
        self.parent.after(20, self.parent.focus_force)
        if self.on_close:
            self.on_close()

    def _on_cancel(self):
        if self._closed:
            return
        self._closed = True
        self.top.grab_release()
        self.top.destroy()
        self.stop_listeners()
        enable_ime()
        self.parent.after(20, self.parent.focus_force)
        if self.on_close:
            self.on_close()

    def stop_listeners(self):
        if self.listener_keyboard:
            self.listener_keyboard.stop()
        if self.listener_mouse:
            self.listener_mouse.stop()


class HotkeyCaptureDialog(KeyCaptureDialog):
    def __init__(self, parent, callback, on_close=None):
        super().__init__(parent, callback, capture_mouse=False, on_close=on_close)
        self.top.title("按下热键（可单键或组合键）")
        self.pressed_keys = set()
        self.modifier_map = {
            'ctrl': 'ctrl', 'alt': 'alt', 'shift': 'shift',
            'cmd': 'cmd', 'win': 'win', 'ctrl_l': 'ctrl',
            'ctrl_r': 'ctrl', 'alt_l': 'alt', 'alt_r': 'alt',
            'shift_l': 'shift', 'shift_r': 'shift'
        }
        self.stop_listeners()
        self.listener_keyboard = pynput_keyboard.Listener(
            on_press=self.on_key_press, on_release=self.on_key_release)
        self.listener_keyboard.start()
        for child in self.top.winfo_children():
            if isinstance(child, ttk.Label):
                child.config(text="请按下热键... (支持单键如 F5，或组合键如 Ctrl+Shift+F12)")

    def on_key_press(self, key):
        k = normalize_captured_key(key)
        k = self.modifier_map.get(k, k)
        self.pressed_keys.add(k)
        combo = '+'.join(sorted(self.pressed_keys))
        for child in self.top.winfo_children():
            if isinstance(child, ttk.Label):
                child.config(text=f"当前按下: {combo}")

    def on_key_release(self, key):
        if self.pressed_keys:
            combo = '+'.join(sorted(self.pressed_keys))
            self.captured_key = combo
            self.top.after(10, self._close)


class EditMappingDialog:
    def __init__(self, parent, current_mode, callback):
        self.top = tk.Toplevel(parent)
        self.top.title("修改模式")
        self.top.geometry("250x140")
        self.top.resizable(False, False)
        self.top.transient(parent)
        self.top.grab_set()
        self.callback = callback

        ttk.Label(self.top, text="选择新模式:", font=("微软雅黑", 10)).pack(pady=10)
        self.mode_var = tk.StringVar(value=current_mode)
        combo = ttk.Combobox(self.top, textvariable=self.mode_var, values=['hold', 'tap'], state='readonly', width=10)
        combo.pack(pady=5)

        btn_frame = ttk.Frame(self.top)
        btn_frame.pack(pady=15)
        ttk.Button(btn_frame, text="确定", command=self._on_ok, width=8).pack(side='left', padx=5)
        ttk.Button(btn_frame, text="取消", command=self.top.destroy, width=8).pack(side='left', padx=5)

        center_window(self.top)

    def _on_ok(self):
        self.callback(self.mode_var.get())
        self.top.destroy()

class LineNumberCanvas(tk.Canvas):
    """行号显示画布"""
    def __init__(self, master, text_widget, **kwargs):
        super().__init__(master, width=40, **kwargs)
        self.text_widget = text_widget
        self.text_widget.bind('<KeyRelease>', self.redraw)
        self.text_widget.bind('<MouseWheel>', self.redraw)
        self.text_widget.bind('<Button-4>', self.redraw)
        self.text_widget.bind('<Button-5>', self.redraw)
        self.redraw()

    def redraw(self, event=None):
        self.delete('all')
        i = self.text_widget.index("@0,0")
        while True:
            dline = self.text_widget.dlineinfo(i)
            if dline is None:
                break
            y = dline[1]
            linenum = str(i).split('.')[0]
            self.create_text(35, y, anchor='ne', text=linenum, font=('Consolas', 10), fill='#666')
            i = self.text_widget.index(f"{i}+1line")


class AutocompleteListbox(tk.Toplevel):
    """自动补全下拉列表"""
    def __init__(self, parent, text_widget, commands, on_select_callback=None):
        super().__init__(parent)
        self.text_widget = text_widget
        self.commands = commands
        self.prefix = ''
        self.on_select_callback = on_select_callback
        self.overrideredirect(True)  # 无边框
        self.withdraw()

        self.listbox = tk.Listbox(self, font=('Consolas', 10), height=6, exportselection=False)
        self.listbox.pack(fill='both', expand=True)
        self.listbox.bind('<ButtonRelease-1>', self.on_select)
        self.listbox.bind('<Return>', self.on_select)
        self.listbox.bind('<Tab>', self.on_select)
        self.listbox.bind('<Escape>', lambda e: self.withdraw())

    def show(self, x, y, prefix, candidates=None):
        self.prefix = prefix
        source = self.commands if candidates is None else candidates
        filtered = [cmd for cmd in source if cmd.startswith(prefix)]
        if not filtered:
            self.withdraw()
            return
        self.listbox.delete(0, tk.END)
        for cmd in filtered[:12]:
            self.listbox.insert(tk.END, cmd)
        self.listbox.selection_set(0)
        self.geometry(f"+{x}+{y}")
        self.deiconify()
        self.lift()

    def on_select(self, event=None):
        if self.listbox.curselection():
            selected = self.listbox.get(self.listbox.curselection())
            if self.prefix:
                self.text_widget.delete(f'insert-{len(self.prefix)}c', 'insert')
            self.text_widget.insert('insert', selected)
            self.withdraw()
            self.text_widget.focus()
            if self.on_select_callback:
                self.on_select_callback()
        return 'break'

    def move_selection(self, delta):
        if not self.winfo_viewable() or self.listbox.size() == 0:
            return False
        current = self.listbox.curselection()
        index = current[0] if current else 0
        index = max(0, min(self.listbox.size() - 1, index + delta))
        self.listbox.selection_clear(0, tk.END)
        self.listbox.selection_set(index)
        self.listbox.activate(index)
        return True

class MacroEditorDialog:
    """增强型宏脚本编辑器（语法高亮、行号、实时错误检查、自动补全）"""
    HELP_TEMPLATE = (
        "# ========== 指令参考 ==========\n"
        "# press <键名>        - 按下并按住键\n"
        "# release <键名>      - 松开键\n"
        "# tap <键名>          - 点击键（按下后立即松开）\n"
        "# wait <毫秒>         - 等待指定时间\n"
        "# loop <次数>         - 开始循环（0 或 infinite 无限循环）\n"
        "# end                 - 结束循环\n"
        "# drag <x> <y> [mouse] [按键]   - 从当前位置拖拽到目标坐标\n"
        "# drag_rel <dx> <dy> [按键]     - 相对拖拽\n"
        "# setpos <x> <y> [mouse]        - 移动光标\n"
        "# setpos_rel <dx> <dy>          - 相对移动光标\n"
        "# combo <键1> <键2> ...         - 同时按下多个键\n"
        "# 常用键名: esc, tab, enter, space, q, w, e, 1, 2, 3, mouse_left, mouse_right\n"
        "# 注释用 # 开头\n"
        "# ===============================\n\n"
    )

    def __init__(self, parent, initial_script="", callback=None, on_close=None):
        self.top = tk.Toplevel(parent)
        self.top.title("编辑宏脚本")
        self.top.geometry("700x500")
        self.top.minsize(600, 400)
        self.top.transient(parent)
        self.callback = callback
        self.on_close = on_close
        self._closed = False

        # 指令列表（用于自动补全和高亮）
        self.keywords = [
            'press', 'release', 'tap', 'wait', 'loop', 'end',
            'drag', 'drag_rel', 'setpos', 'setpos_rel', 'combo'
        ]
        self.modifiers = ['mouse', 'left', 'right', 'middle', 'infinite']
        self.key_names = sorted(set(VK_CODES) | set(KEY_ALIASES) | set(MOUSE_EVENTS))

        # 主框架
        main_frame = ttk.Frame(self.top)
        main_frame.pack(fill='both', expand=True, padx=10, pady=10)

        # 文本编辑区与行号
        text_frame = ttk.Frame(main_frame)
        text_frame.pack(fill='both', expand=True)

        self.text = tk.Text(text_frame, wrap=tk.WORD, font=('Consolas', 10), undo=True)
        self.text.pack(side='right', fill='both', expand=True)

        self.line_numbers = LineNumberCanvas(text_frame, self.text, bg='#f0f0f0', highlightthickness=0)
        self.line_numbers.pack(side='left', fill='y')

        # 垂直滚动条
        scrollbar = ttk.Scrollbar(main_frame, orient='vertical', command=self.text.yview)
        self.text.configure(yscrollcommand=lambda *args: (scrollbar.set(*args), self.line_numbers.redraw()))
        scrollbar.pack(side='right', fill='y')

        # 状态栏（显示错误信息）
        self.status_var = tk.StringVar(value="就绪")
        status_bar = ttk.Label(main_frame, textvariable=self.status_var, relief='sunken', anchor='w', font=('微软雅黑', 9))
        status_bar.pack(side='bottom', fill='x', pady=(5, 0))

        # 按钮栏
        btn_frame = ttk.Frame(main_frame)
        btn_frame.pack(side='bottom', fill='x', pady=(5, 0))
        ttk.Button(btn_frame, text="保存", command=self._on_save).pack(side='right', padx=5)
        ttk.Button(btn_frame, text="取消", command=self._on_cancel).pack(side='right', padx=5)
        ttk.Button(btn_frame, text="检查语法", command=self.check_syntax).pack(side='left', padx=5)

        # 设置语法高亮标签
        self.text.tag_configure('keyword', foreground='#0000cc', font=('Consolas', 10, 'bold'))
        self.text.tag_configure('comment', foreground='#008000')
        self.text.tag_configure('error', underline=True, underlinefg='red')
        self.text.tag_configure('modifier', foreground='#aa5500')

        # 绑定事件
        self.text.bind('<KeyRelease>', self.on_key_release)
        self.text.bind('<Return>', self.handle_return)
        self.text.bind('<space>', self.on_key_release)
        self.text.bind('<Control-space>', self.show_autocomplete)
        self.text.bind('<Tab>', self.handle_tab)
        self.text.bind('<Down>', self.handle_down)
        self.text.bind('<Up>', self.handle_up)
        self.top.protocol("WM_DELETE_WINDOW", self._on_cancel)

        # 自动补全实例
        self.autocomplete = AutocompleteListbox(
            self.top,
            self.text,
            self.keywords + self.modifiers + self.key_names,
            on_select_callback=self.after_programmatic_edit
        )

        # 插入帮助模板
        self.insert_help_template(initial_script)

        # 初始语法检查
        self.top.after(100, self.check_syntax)

        center_window(self.top, 700, 500)

    @classmethod
    def strip_help_template(cls, script):
        cleaned = script or ''
        marker_start = "# ========== 指令参考 =========="
        marker_end = "# ==============================="
        while cleaned.lstrip().startswith(marker_start):
            leading_spaces = len(cleaned) - len(cleaned.lstrip())
            end_idx = cleaned.find(marker_end)
            if end_idx == -1:
                break
            end_idx += len(marker_end)
            if end_idx < len(cleaned) and cleaned[end_idx:end_idx + 2] == '\r\n':
                end_idx += 2
            elif end_idx < len(cleaned) and cleaned[end_idx] == '\n':
                end_idx += 1
            cleaned = cleaned[:leading_spaces] + cleaned[end_idx:].lstrip('\r\n')
        return cleaned

    def insert_help_template(self, initial_script):
        clean_script = self.strip_help_template(initial_script).strip()
        if clean_script:
            self.text.insert('1.0', clean_script)
        else:
            self.text.insert('1.0', self.HELP_TEMPLATE)
            self.text.insert(tk.END, "# 示例:\n# tap e\n# wait 100\n# loop 3\n#     combo ctrl c\n#     wait 50\n# end\n")
        self.highlight_syntax()

    def on_key_release(self, event=None):
        self.highlight_syntax()
        self.line_numbers.redraw()
        # 延迟检查语法，避免卡顿
        self.top.after(300, self.check_syntax)
        ignored = {'Escape', 'Return', 'Tab', 'Up', 'Down', 'Left', 'Right'}
        if event is None or event.keysym not in ignored:
            self.top.after(1, self.show_autocomplete)

    def after_programmatic_edit(self):
        self.highlight_syntax()
        self.line_numbers.redraw()
        self.check_syntax()

    def highlight_syntax(self):
        # 清除所有标签
        for tag in ('keyword', 'comment', 'modifier'):
            self.text.tag_remove(tag, '1.0', tk.END)

        content = self.text.get('1.0', tk.END)
        lines = content.splitlines()
        for i, line in enumerate(lines):
            line_num = i + 1
            # 注释高亮
            if '#' in line:
                idx = line.index('#')
                start = f"{line_num}.{idx}"
                end = f"{line_num}.end"
                self.text.tag_add('comment', start, end)

            # 关键字高亮
            words = line.split()
            col = 0
            for word in words:
                # 忽略注释部分
                if '#' in word:
                    break
                clean_word = word.strip('(),:')
                if clean_word in self.keywords:
                    # 找到单词在行中的准确位置
                    start_col = line.find(clean_word, col)
                    if start_col != -1:
                        start = f"{line_num}.{start_col}"
                        end = f"{line_num}.{start_col + len(clean_word)}"
                        self.text.tag_add('keyword', start, end)
                elif clean_word in self.modifiers:
                    start_col = line.find(clean_word, col)
                    if start_col != -1:
                        start = f"{line_num}.{start_col}"
                        end = f"{line_num}.{start_col + len(clean_word)}"
                        self.text.tag_add('modifier', start, end)
                col += len(word) + 1

    def check_syntax(self):
        """实时编译检查，标记错误行"""
        self.text.tag_remove('error', '1.0', tk.END)
        script = self.text.get('1.0', tk.END).strip()
        if not script:
            self.status_var.set("就绪")
            return
        from script_compiler import ScriptCompiler
        compiler = ScriptCompiler()
        _, errors = compiler.compile(script)
        if errors:
            # 解析错误行号
            for err in errors:
                # 错误格式: "第 X 行: ..."
                import re
                match = re.search(r'第 (\d+) 行', err)
                if match:
                    line_num = int(match.group(1))
                    start = f"{line_num}.0"
                    end = f"{line_num}.end"
                    self.text.tag_add('error', start, end)
            self.status_var.set(errors[0] if errors else "语法错误")
        else:
            self.status_var.set("语法正确")

    def get_autocomplete_context(self):
        cursor_pos = self.text.index('insert')
        line_start = cursor_pos.split('.')[0] + '.0'
        line_text = self.text.get(line_start, cursor_pos)
        trailing_space = bool(line_text) and line_text[-1].isspace()
        tokens = line_text.split()
        prefix = '' if trailing_space or not tokens else tokens[-1].lower()
        command = tokens[0].lower() if tokens else ''

        if not tokens or (len(tokens) == 1 and not trailing_space):
            candidates = self.keywords
        elif command in ('press', 'release', 'tap', 'combo'):
            candidates = self.key_names
        elif command == 'wait':
            candidates = ['10', '20', '30', '50', '100', '200', '500', '1000']
        elif command == 'loop':
            candidates = ['0', 'infinite', '1', '2', '3', '5', '10']
        elif command in ('drag', 'drag_rel'):
            candidates = ['left', 'right', 'middle', 'mouse']
        elif command == 'setpos':
            candidates = ['mouse']
        elif command in ('end',):
            candidates = []
        else:
            candidates = self.keywords + self.modifiers + self.key_names
        return cursor_pos, prefix, candidates

    def show_autocomplete(self, event=None):
        cursor_pos, prefix, candidates = self.get_autocomplete_context()
        # 获取光标屏幕坐标
        bbox = self.text.bbox(cursor_pos)
        if bbox:
            x, y, width, height = bbox
            x_root = self.text.winfo_rootx() + x
            y_root = self.text.winfo_rooty() + y + height
            self.autocomplete.show(x_root, y_root, prefix, candidates)
        return 'break'

    def handle_tab(self, event):
        # 如果有自动补全显示，则选择第一项
        if self.autocomplete.winfo_viewable():
            self.autocomplete.on_select()
            return 'break'
        # 否则插入制表符
        self.text.insert('insert', '    ')
        return 'break'

    def handle_return(self, event):
        self.autocomplete.withdraw()

    def handle_down(self, event):
        if self.autocomplete.move_selection(1):
            return 'break'

    def handle_up(self, event):
        if self.autocomplete.move_selection(-1):
            return 'break'

    def _on_save(self):
        # 保存前检查语法
        self.check_syntax()
        if '错误' in self.status_var.get() or '语法错误' in self.status_var.get():
            if not messagebox.askyesno("语法错误", "脚本存在语法错误，确定保存吗？\n保存后可能无法正常执行。"):
                return
        script = self.strip_help_template(self.text.get('1.0', tk.END)).strip()
        if self.callback:
            self.callback(script)
        self._close()

    def _on_cancel(self):
        self._close()

    def _close(self):
        if self._closed:
            return
        self._closed = True
        self.autocomplete.withdraw()
        self.top.destroy()
        if self.on_close:
            self.on_close()


class LogWindow:
    def __init__(self, parent):
        self.top = tk.Toplevel(parent)
        self.top.title("运行日志")
        self.top.geometry("500x300")
        self.top.transient(parent)
        self.log_text = scrolledtext.ScrolledText(self.top, state='disabled', wrap=tk.WORD)
        self.log_text.pack(fill='both', expand=True)
        center_window(self.top, 500, 300)

    def log(self, message):
        self.log_text.config(state='normal')
        self.log_text.insert(tk.END, f"[{time.strftime('%H:%M:%S')}] {message}\n")
        self.log_text.see(tk.END)
        self.log_text.config(state='disabled')


class MapperGUI:
    def __init__(self):
        self.mapper = KeyMapper()
        self.mapper_thread = None
        self.hotkey = 'ctrl+shift+f12'
        self.log_window = None
        self._capture_dialog_active = False
        self._capture_dialog_cooldown_until = 0
        self.load_config()

        self.root = tk.Tk()
        self.root.title("BA KeySmith")
        self.root.geometry("960x680")
        self.root.minsize(880, 620)
        self.root.protocol("WM_DELETE_WINDOW", self.on_close)

        self.setup_styles()
        self.create_widgets()
        self.refresh_table()
        center_window(self.root, 960, 680)

        self.update_game_status()

    def setup_styles(self):
        style = ttk.Style()
        style.theme_use('clam')
        self.colors = {
            'bg': '#eef3f8',
            'surface': '#ffffff',
            'surface_soft': '#f8fafc',
            'ink': '#172033',
            'muted': '#64748b',
            'border': '#dbe5ef',
            'accent': '#2563eb',
            'accent_hover': '#1d4ed8',
            'success': '#16a34a',
            'warning': '#d97706',
            'danger': '#dc2626',
        }
        base_font = ('Microsoft YaHei UI', 9)
        title_font = ('Microsoft YaHei UI', 18, 'bold')
        section_font = ('Microsoft YaHei UI', 10, 'bold')

        self.root.configure(bg=self.colors['bg'])
        style.configure('.', font=base_font)
        style.configure('TFrame', background=self.colors['bg'])
        style.configure('App.TFrame', background=self.colors['bg'])
        style.configure('Surface.TFrame', background=self.colors['surface'])
        style.configure('TLabel', background=self.colors['bg'], foreground=self.colors['ink'], font=base_font)
        style.configure('Card.TLabel', background=self.colors['surface'], foreground=self.colors['ink'], font=base_font)
        style.configure('Title.TLabel', background=self.colors['bg'], foreground=self.colors['ink'], font=title_font)
        style.configure('Subtitle.TLabel', background=self.colors['bg'], foreground=self.colors['muted'], font=('Microsoft YaHei UI', 9))
        style.configure('Muted.TLabel', background=self.colors['surface'], foreground=self.colors['muted'], font=('Microsoft YaHei UI', 8))
        style.configure('TLabelframe', background=self.colors['surface'], foreground=self.colors['ink'], borderwidth=1, relief='solid')
        style.configure('TLabelframe.Label', background=self.colors['surface'], foreground=self.colors['ink'], font=section_font)
        style.configure('Card.TLabelframe', background=self.colors['surface'], foreground=self.colors['ink'], borderwidth=1, relief='solid')
        style.configure('Card.TLabelframe.Label', background=self.colors['surface'], foreground=self.colors['ink'], font=section_font)

        style.configure('TButton', font=base_font, padding=(12, 7), borderwidth=0)
        style.configure('Accent.TButton', background=self.colors['accent'], foreground='white')
        style.map('Accent.TButton', background=[('active', self.colors['accent_hover']), ('disabled', '#a7b8d8')], foreground=[('disabled', '#eef2ff')])
        style.configure('Ghost.TButton', background=self.colors['surface_soft'], foreground=self.colors['ink'])
        style.map('Ghost.TButton', background=[('active', '#e2e8f0'), ('disabled', '#f1f5f9')])
        style.configure('Danger.TButton', background='#fee2e2', foreground=self.colors['danger'])
        style.map('Danger.TButton', background=[('active', '#fecaca'), ('disabled', '#f8fafc')])

        style.configure('Status.TLabel', background=self.colors['surface'], foreground=self.colors['muted'], padding=(10, 5), font=('Microsoft YaHei UI', 9, 'bold'))
        style.configure('Idle.Status.TLabel', background='#e2e8f0', foreground='#334155', padding=(10, 5), font=('Microsoft YaHei UI', 9, 'bold'))
        style.configure('Running.Status.TLabel', background='#dcfce7', foreground='#166534', padding=(10, 5), font=('Microsoft YaHei UI', 9, 'bold'))
        style.configure('Paused.Status.TLabel', background='#fee2e2', foreground='#991b1b', padding=(10, 5), font=('Microsoft YaHei UI', 9, 'bold'))
        style.configure('Editing.Status.TLabel', background='#fef3c7', foreground='#92400e', padding=(10, 5), font=('Microsoft YaHei UI', 9, 'bold'))

        style.configure('Treeview', background=self.colors['surface'], fieldbackground=self.colors['surface'], foreground=self.colors['ink'], rowheight=32, borderwidth=0, font=('Microsoft YaHei UI', 9))
        style.configure('Treeview.Heading', background='#e8f0fb', foreground=self.colors['ink'], font=('Microsoft YaHei UI', 9, 'bold'), padding=(8, 8))
        style.map('Treeview', background=[('selected', '#dbeafe')], foreground=[('selected', '#1e3a8a')])
        style.configure('TEntry', fieldbackground='white', padding=(8, 5))
        style.configure('TCombobox', fieldbackground='white', padding=(8, 5))

    def load_config(self):
        try:
            with open('config.json', 'r', encoding='utf-8') as f:
                config = json.load(f)
            self.mappings = config.get('mappings', [])
            self.hotkey = config.get('hotkey', 'ctrl+shift+f12')
        except FileNotFoundError:
            self.mappings = []
            self.hotkey = 'ctrl+shift+f12'

    def save_config(self):
        config = {
            'mappings': self.mappings,
            'hotkey': self.hotkey
        }
        with open('config.json', 'w', encoding='utf-8') as f:
            json.dump(config, f, indent=4)

    def log(self, message):
        if self.log_window and self.log_window.top.winfo_exists():
            self.log_window.log(message)

    def _pause_mapper_for_dialog(self):
        if not self.mapper.running:
            return None
        previous_enabled = self.mapper.enabled
        self.mapper.enabled = False
        self.mapper._release_all()
        self.status_var.set("编辑中暂停")
        self.status_label.configure(style='Editing.Status.TLabel')
        return previous_enabled

    def _restore_mapper_after_dialog(self, previous_enabled):
        if previous_enabled is None or not self.mapper.running:
            return
        self.mapper.enabled = previous_enabled
        self._update_status_after_toggle(previous_enabled)

    def _open_macro_editor(self, initial_script, save_callback):
        previous_enabled = self._pause_mapper_for_dialog()

        def restore():
            self._restore_mapper_after_dialog(previous_enabled)

        MacroEditorDialog(
            self.root,
            initial_script,
            save_callback,
            on_close=restore
        )

    def update_game_status(self):
        running = KeyMapper.check_game_running()
        if running:
            self.game_status_var.set("游戏运行中")
            self.game_status_label.configure(style='Running.Status.TLabel')
        else:
            self.game_status_var.set("游戏未运行")
            self.game_status_label.configure(style='Idle.Status.TLabel')
        self.root.after(2000, self.update_game_status)

    def create_widgets(self):
        self.root.grid_rowconfigure(0, weight=0)
        self.root.grid_rowconfigure(1, weight=1)
        self.root.grid_rowconfigure(2, weight=0)
        self.root.grid_columnconfigure(0, weight=1)

        header_frame = ttk.Frame(self.root, style='App.TFrame')
        header_frame.grid(row=0, column=0, sticky='ew', padx=22, pady=(18, 10))
        header_frame.columnconfigure(0, weight=1)

        title_group = ttk.Frame(header_frame, style='App.TFrame')
        title_group.grid(row=0, column=0, sticky='w')
        ttk.Label(title_group, text="BA KeySmith", style='Title.TLabel').pack(anchor='w')
        ttk.Label(
            title_group,
            text="BAKS by HX · 面向蔚蓝档案 PC 端的键位映射与宏工具",
            style='Subtitle.TLabel'
        ).pack(anchor='w', pady=(2, 0))

        status_group = ttk.Frame(header_frame, style='App.TFrame')
        status_group.grid(row=0, column=1, sticky='e')
        self.status_var = tk.StringVar(value="未启动")
        self.status_label = ttk.Label(status_group, textvariable=self.status_var, style='Idle.Status.TLabel')
        self.status_label.pack(side='left', padx=(0, 8))
        self.game_status_var = tk.StringVar(value="检测中")
        self.game_status_label = ttk.Label(status_group, textvariable=self.game_status_var, style='Idle.Status.TLabel')
        self.game_status_label.pack(side='left')

        middle_frame = ttk.Frame(self.root, style='App.TFrame')
        middle_frame.grid(row=1, column=0, sticky='nsew', padx=22, pady=(0, 10))
        middle_frame.grid_rowconfigure(0, weight=1)
        middle_frame.grid_columnconfigure(0, weight=1)

        # 表格区域
        table_frame = ttk.LabelFrame(middle_frame, text="映射列表", padding=14, style='Card.TLabelframe')
        table_frame.grid(row=0, column=0, sticky='nsew')
        table_frame.grid_rowconfigure(0, weight=1)
        table_frame.grid_columnconfigure(0, weight=1)

        columns = ('触发键', '类型', '目标/脚本', '模式')
        self.tree = ttk.Treeview(table_frame, columns=columns, show='headings', height=8)
        self.tree.heading('触发键', text='触发键')
        self.tree.heading('类型', text='类型')
        self.tree.heading('目标/脚本', text='目标/脚本')
        self.tree.heading('模式', text='模式')
        self.tree.column('触发键', width=120, anchor='center')
        self.tree.column('类型', width=90, anchor='center')
        self.tree.column('目标/脚本', width=420, anchor='w')
        self.tree.column('模式', width=90, anchor='center')
        self.tree.grid(row=0, column=0, sticky='nsew')
        self.tree.bind('<Double-1>', lambda event: self.edit_selected())
        self.tree.bind('<Return>', lambda event: self.edit_selected())
        self.tree.bind('<Delete>', lambda event: self.delete_mapping())
        self.tree.tag_configure('odd', background='#fbfdff')
        self.tree.tag_configure('even', background='#f4f8fd')

        tree_scrollbar = ttk.Scrollbar(table_frame, orient='vertical', command=self.tree.yview)
        self.tree.configure(yscrollcommand=tree_scrollbar.set)
        tree_scrollbar.grid(row=0, column=1, sticky='ns')

        table_footer = ttk.Frame(table_frame, style='Surface.TFrame')
        table_footer.grid(row=1, column=0, columnspan=2, pady=(12, 0), sticky='ew')
        table_footer.columnconfigure(1, weight=1)
        btn_frame = ttk.Frame(table_footer, style='Surface.TFrame')
        btn_frame.grid(row=0, column=0, sticky='w')
        edit_btn = ttk.Button(btn_frame, text="编辑选中", command=self.edit_selected, style='Ghost.TButton')
        edit_btn.pack(side='left', padx=(0, 8))
        delete_btn = ttk.Button(btn_frame, text="删除选中", command=self.delete_mapping, style='Danger.TButton')
        delete_btn.pack(side='left')
        ttk.Label(
            table_footer,
            text="提示：双击映射可编辑，Delete 可删除",
            style='Muted.TLabel'
        ).grid(row=0, column=1, sticky='e')
        ToolTip(edit_btn, "双击列表项也可以编辑")
        ToolTip(delete_btn, "选中一行后按 Delete 也可以删除")

        # 添加映射区域
        add_frame = ttk.LabelFrame(middle_frame, text="添加新映射", padding=14, style='Card.TLabelframe')
        add_frame.grid(row=1, column=0, sticky='ew', pady=(12, 0))
        for i in range(8):
            add_frame.columnconfigure(i, weight=1)

        ttk.Label(add_frame, text="触发键", style='Card.TLabel').grid(row=0, column=0, padx=(0, 6), pady=6, sticky='e')
        self.trigger_var = tk.StringVar()
        ttk.Entry(add_frame, textvariable=self.trigger_var, width=14, state='readonly').grid(
            row=0, column=1, padx=6, pady=6, sticky='ew')
        capture_trigger_btn = ttk.Button(
            add_frame,
            text="捕获",
            command=lambda: self.capture_key('trigger'),
            takefocus=False,
            style='Ghost.TButton'
        )
        capture_trigger_btn.grid(row=0, column=2, padx=6, pady=6)
        ToolTip(capture_trigger_btn, "捕获键盘或鼠标按键作为触发键")

        ttk.Label(add_frame, text="类型", style='Card.TLabel').grid(row=0, column=3, padx=(14, 6), pady=6, sticky='e')
        self.type_var = tk.StringVar(value='simple')
        type_combo = ttk.Combobox(add_frame, textvariable=self.type_var, values=['simple', 'macro'], state='readonly', width=8)
        type_combo.grid(row=0, column=4, padx=6, pady=6, sticky='ew')
        type_combo.bind('<<ComboboxSelected>>', self.on_type_change)

        # 简单映射专用控件
        self.simple_frame = ttk.Frame(add_frame, style='Surface.TFrame')
        self.simple_frame.grid(row=1, column=0, columnspan=8, sticky='ew', pady=5)

        ttk.Label(self.simple_frame, text="目标键", style='Card.TLabel').grid(row=0, column=0, padx=(0, 6), pady=6, sticky='e')
        self.target_var = tk.StringVar()
        ttk.Entry(self.simple_frame, textvariable=self.target_var, width=14, state='readonly').grid(
            row=0, column=1, padx=6, pady=6, sticky='ew')
        capture_target_btn = ttk.Button(
            self.simple_frame,
            text="捕获",
            command=lambda: self.capture_key('target'),
            takefocus=False,
            style='Ghost.TButton'
        )
        capture_target_btn.grid(row=0, column=2, padx=6, pady=6)
        ToolTip(capture_target_btn, "捕获要发送给游戏的目标键")

        ttk.Label(self.simple_frame, text="模式", style='Card.TLabel').grid(row=0, column=3, padx=(14, 6), pady=6, sticky='e')
        self.mode_var = tk.StringVar(value='hold')
        mode_combo = ttk.Combobox(self.simple_frame, textvariable=self.mode_var, values=['hold', 'tap'], state='readonly', width=8)
        mode_combo.grid(row=0, column=4, padx=6, pady=6, sticky='w')
        ttk.Label(
            self.simple_frame,
            text="hold=按住映射，tap=点击一次",
            style='Muted.TLabel'
        ).grid(row=0, column=5, padx=(10, 0), sticky='w')

        # 宏专用控件
        self.macro_frame = ttk.Frame(add_frame, style='Surface.TFrame')
        self.macro_frame.grid(row=1, column=0, columnspan=8, sticky='ew', pady=5)
        self.macro_frame.grid_remove()

        ttk.Label(self.macro_frame, text="脚本", style='Card.TLabel').grid(row=0, column=0, padx=(0, 6), pady=6, sticky='e')
        self.script_preview_var = tk.StringVar()
        ttk.Entry(self.macro_frame, textvariable=self.script_preview_var, width=40, state='readonly').grid(
            row=0, column=1, padx=6, pady=6, sticky='ew')
        macro_btn = ttk.Button(self.macro_frame, text="编辑脚本", command=self.edit_macro, style='Ghost.TButton')
        macro_btn.grid(row=0, column=2, padx=6, pady=6)
        ToolTip(macro_btn, "打开宏编辑器，运行中会自动暂停映射输入")

        add_btn = ttk.Button(add_frame, text="添加映射", command=self.add_mapping, style='Accent.TButton')
        add_btn.grid(row=2, column=0, columnspan=8, pady=(8, 0), sticky='ew')

        # 底部控制栏
        bottom_frame = ttk.LabelFrame(self.root, text="运行控制", padding=14, style='Card.TLabelframe')
        bottom_frame.grid(row=2, column=0, sticky='ew', padx=22, pady=(0, 18))
        bottom_frame.columnconfigure(1, weight=1)

        ctrl_frame = ttk.Frame(bottom_frame, style='Surface.TFrame')
        ctrl_frame.grid(row=0, column=0, sticky='w')
        ctrl_frame.columnconfigure(1, weight=1)

        self.btn_start = ttk.Button(ctrl_frame, text="启动映射", command=self.start_mapper, style='Accent.TButton')
        self.btn_start.grid(row=0, column=0, padx=(0, 8))

        self.btn_stop = ttk.Button(ctrl_frame, text="停止", command=self.stop_mapper, state='disabled', style='Danger.TButton')
        self.btn_stop.grid(row=0, column=1, padx=(0, 8), sticky='w')

        hotkey_frame = ttk.Frame(bottom_frame, style='Surface.TFrame')
        hotkey_frame.grid(row=0, column=1, sticky='e')
        self.hotkey_label = ttk.Label(hotkey_frame, text=f"开关热键: {self.hotkey}", style='Card.TLabel')
        self.hotkey_label.pack(side='left', padx=(0, 10))
        hotkey_btn = ttk.Button(hotkey_frame, text="捕获热键", command=self.capture_hotkey, takefocus=False, style='Ghost.TButton')
        hotkey_btn.pack(side='left', padx=(0, 8))
        reset_btn = ttk.Button(hotkey_frame, text="重置", command=self.reset_hotkey, style='Ghost.TButton')
        reset_btn.pack(side='left', padx=(0, 8))
        log_btn = ttk.Button(hotkey_frame, text="运行日志", command=self.show_log_window, style='Ghost.TButton')
        log_btn.pack(side='left')
        ToolTip(self.btn_start, "启动后仅在检测到 BlueArchive.exe 时发送映射输入")
        ToolTip(self.btn_stop, "停止映射并释放所有按住状态")
        ToolTip(hotkey_btn, "设置运行中暂停/恢复的全局热键")
        ToolTip(log_btn, "查看捕获、启动、暂停等运行记录")

    def on_type_change(self, event=None):
        if self.type_var.get() == 'simple':
            self.macro_frame.grid_remove()
            self.simple_frame.grid()
        else:
            self.simple_frame.grid_remove()
            self.macro_frame.grid()

    def edit_macro(self):
        current = getattr(self, 'temp_macro_script', '')
        def save_callback(script):
            self.script_preview_var.set(script[:50] + ('...' if len(script) > 50 else ''))
            self.temp_macro_script = script
        self._open_macro_editor(current, save_callback)

    def capture_key(self, target):
        if self._capture_dialog_active or time.perf_counter() < self._capture_dialog_cooldown_until:
            return
        self._capture_dialog_active = True
        self.root.focus_force()

        def on_close():
            self._capture_dialog_active = False
            self._capture_dialog_cooldown_until = time.perf_counter() + 0.25

        def set_key(key_name):
            if target == 'trigger':
                self.trigger_var.set(key_name)
            else:
                self.target_var.set(key_name)
            self.log(f"捕获按键: {key_name}")
        KeyCaptureDialog(self.root, set_key, capture_mouse=True, on_close=on_close)

    def capture_hotkey(self):
        if self._capture_dialog_active or time.perf_counter() < self._capture_dialog_cooldown_until:
            return
        self._capture_dialog_active = True
        self.root.focus_force()

        def on_close():
            self._capture_dialog_active = False
            self._capture_dialog_cooldown_until = time.perf_counter() + 0.25

        def set_hotkey(combo):
            self.hotkey = combo
            self.hotkey_label.config(text=f"开关热键: {self.hotkey}")
            self.save_config()
            self.log(f"热键已设置为: {combo}")
            messagebox.showinfo("提示", f"热键已设置为 {self.hotkey}，下次启动映射时生效。")
        HotkeyCaptureDialog(self.root, set_hotkey, on_close=on_close)

    def reset_hotkey(self):
        self.hotkey = 'ctrl+shift+f12'
        self.hotkey_label.config(text=f"开关热键: {self.hotkey}")
        self.save_config()
        self.log("热键已重置为 ctrl+shift+f12")
        messagebox.showinfo("提示", "热键已重置为 ctrl+shift+f12")

    def edit_selected(self):
        selected = self.tree.selection()
        if not selected:
            messagebox.showinfo("提示", "请先在表格中选中要编辑的映射")
            return
        item = self.tree.item(selected[0])
        values = item['values']
        trigger = values[0]
        mtype = values[1]

        # 找到对应映射
        for m in self.mappings:
            if m['trigger'] == trigger:
                if mtype == '简单' or m.get('type') == 'simple':
                    # 编辑简单映射的模式
                    current_mode = m.get('mode', 'hold')
                    def update_mode(new_mode):
                        m['mode'] = new_mode
                        self.save_config()
                        self.refresh_table()
                        self.log(f"修改映射 {trigger} 模式为 {new_mode}")
                    EditMappingDialog(self.root, current_mode, update_mode)
                else:
                    # 编辑宏脚本
                    current_script = m.get('script', '')
                    def update_script(new_script):
                        m['script'] = new_script
                        self.save_config()
                        self.refresh_table()
                        self.log(f"修改宏 {trigger} 脚本")
                    self._open_macro_editor(current_script, update_script)
                break

    def refresh_table(self):
        for row in self.tree.get_children():
            self.tree.delete(row)
        for index, item in enumerate(self.mappings):
            trigger = item['trigger']
            mtype = item.get('type', 'simple')
            if mtype == 'simple':
                target = item.get('target', '')
                mode = item.get('mode', 'hold')
                display_type = '简单'
                display_info = target
                display_mode = mode
            else:
                script = item.get('script', '')
                display_type = '宏'
                display_info = script[:30] + ('...' if len(script) > 30 else '')
                display_mode = '---'
            tag = 'even' if index % 2 == 0 else 'odd'
            self.tree.insert('', 'end', values=(trigger, display_type, display_info, display_mode), tags=(tag,))

    def add_mapping(self):
        trigger = self.trigger_var.get().strip().lower()
        if not trigger:
            messagebox.showerror("错误", "请先捕获触发键")
            return

        # 检查重复
        for m in self.mappings:
            if m['trigger'] == trigger:
                messagebox.showerror("错误", f"触发键 {trigger} 已存在映射")
                return

        mtype = self.type_var.get()
        if mtype == 'simple':
            target = self.target_var.get().strip().lower()
            mode = self.mode_var.get()
            if not target:
                messagebox.showerror("错误", "请先捕获目标键")
                return
            new_mapping = {
                'trigger': trigger,
                'type': 'simple',
                'target': target,
                'mode': mode
            }
        else:
            script = getattr(self, 'temp_macro_script', '')
            if not script:
                messagebox.showerror("错误", "请先编辑宏脚本")
                return
            new_mapping = {
                'trigger': trigger,
                'type': 'macro',
                'script': script
            }
            self.temp_macro_script = ''

        self.mappings.append(new_mapping)
        self.save_config()
        self.refresh_table()
        self.trigger_var.set('')
        self.target_var.set('')
        self.script_preview_var.set('')
        self.log(f"添加映射: {trigger} ({mtype})")

    def delete_mapping(self):
        selected = self.tree.selection()
        if not selected:
            messagebox.showinfo("提示", "请先在表格中选中要删除的映射")
            return
        item = self.tree.item(selected[0])
        trigger = item['values'][0]
        self.mappings = [m for m in self.mappings if m['trigger'] != trigger]
        self.save_config()
        self.refresh_table()
        self.log(f"删除映射: {trigger}")

    def start_mapper(self):
        if self.mapper_thread and self.mapper_thread.is_alive():
            return

        if not KeyMapper.check_game_running():
            if not messagebox.askyesno("游戏未运行", "未检测到 BlueArchive.exe 进程，映射可能不会生效。\n是否继续启动？"):
                return

        self.mapper.clear_mappings()
        for m in self.mappings:
            try:
                if m.get('type') == 'macro':
                    self.mapper.add_macro(m['trigger'], m['script'])
                else:
                    self.mapper.add_simple_mapping(m['trigger'], m['target'], m.get('mode', 'hold'))
            except ValueError as e:
                messagebox.showerror("宏编译错误", f"触发键 {m['trigger']} 的宏脚本有误:\n{e}")
                return

        self.mapper_thread = threading.Thread(target=self._run_mapper, daemon=True)
        self.mapper_thread.start()

        try:
            keyboard.remove_hotkey(self.hotkey)
        except:
            pass
        try:
            keyboard.add_hotkey(self.hotkey, self.toggle_from_hotkey)
        except Exception as e:
            messagebox.showerror("错误", f"注册热键失败: {e}")

        self.btn_start.config(state='disabled')
        self.btn_stop.config(state='normal')
        self.status_var.set("运行中")
        self.status_label.configure(style='Running.Status.TLabel')
        self.log(f"映射已启动，开关热键: {self.hotkey}")

    def _run_mapper(self):
        self.mapper.start()
        while self.mapper.running:
            time.sleep(0.5)

    def toggle_from_hotkey(self):
        state = self.mapper.toggle()
        self.root.after(0, lambda: self._update_status_after_toggle(state))
        self.log(f"热键切换: {'运行' if state else '暂停'}")

    def _update_status_after_toggle(self, state):
        if state:
            self.status_var.set("运行中")
            self.status_label.configure(style='Running.Status.TLabel')
        else:
            self.status_var.set("已暂停")
            self.status_label.configure(style='Paused.Status.TLabel')

    def stop_mapper(self):
        self.mapper.stop()
        try:
            keyboard.remove_hotkey(self.hotkey)
        except:
            pass
        self.btn_start.config(state='normal')
        self.btn_stop.config(state='disabled')
        self.status_var.set("已停止")
        self.status_label.configure(style='Idle.Status.TLabel')
        self.log("映射已停止")

    def show_log_window(self):
        if self.log_window is None or not self.log_window.top.winfo_exists():
            self.log_window = LogWindow(self.root)
        else:
            self.log_window.top.lift()

    def on_close(self):
        self.stop_mapper()
        if self.log_window and self.log_window.top.winfo_exists():
            self.log_window.top.destroy()
        self.root.destroy()

    def run(self):
        self.root.mainloop()


if __name__ == '__main__':
    app = MapperGUI()
    app.run()
