# utils.py
import ctypes
from ctypes import wintypes

user32 = ctypes.windll.user32

# 键盘虚拟键码
VK_CODES = {
    # 字母键
    'a': 0x41, 'b': 0x42, 'c': 0x43, 'd': 0x44, 'e': 0x45, 'f': 0x46,
    'g': 0x47, 'h': 0x48, 'i': 0x49, 'j': 0x4A, 'k': 0x4B, 'l': 0x4C,
    'm': 0x4D, 'n': 0x4E, 'o': 0x4F, 'p': 0x50, 'q': 0x51, 'r': 0x52,
    's': 0x53, 't': 0x54, 'u': 0x55, 'v': 0x56, 'w': 0x57, 'x': 0x58,
    'y': 0x59, 'z': 0x5A,
    # 数字键
    '0': 0x30, '1': 0x31, '2': 0x32, '3': 0x33, '4': 0x34,
    '5': 0x35, '6': 0x36, '7': 0x37, '8': 0x38, '9': 0x39,
    # 功能键
    'f1': 0x70, 'f2': 0x71, 'f3': 0x72, 'f4': 0x73,
    'f5': 0x74, 'f6': 0x75, 'f7': 0x76, 'f8': 0x77,
    'f9': 0x78, 'f10': 0x79, 'f11': 0x7A, 'f12': 0x7B,
    # 控制键
    'enter': 0x0D, 'space': 0x20, 'tab': 0x09, 'escape': 0x1B, 'esc': 0x1B,
    'backspace': 0x08, 'shift': 0x10, 'ctrl': 0x11, 'alt': 0x12,
    # 方向键
    'up': 0x26, 'down': 0x28, 'left': 0x25, 'right': 0x27,
}

KEY_ALIASES = {
    'esc': 'escape',
    'return': 'enter',
    'control': 'ctrl',
    'control_l': 'ctrl',
    'control_r': 'ctrl',
}

# 鼠标事件标志
MOUSE_EVENTS = {
    'mouse_left': (0x0002, 0x0004),
    'mouse_right': (0x0008, 0x0010),
    'mouse_middle': (0x0020, 0x0040),
    'mouse_x1': (0x0080, 0x0100),
    'mouse_x2': (0x0080, 0x0100),
}

# SendInput 相关常量
INPUT_KEYBOARD = 1
KEYEVENTF_KEYDOWN = 0x0000
KEYEVENTF_KEYUP = 0x0002
KEYEVENTF_SCANCODE = 0x0008
KEYEVENTF_EXTENDEDKEY = 0x0001

class KEYBDINPUT(ctypes.Structure):
    _fields_ = [
        ("wVk", wintypes.WORD),
        ("wScan", wintypes.WORD),
        ("dwFlags", wintypes.DWORD),
        ("time", wintypes.DWORD),
        ("dwExtraInfo", ctypes.POINTER(ctypes.c_ulong))
    ]

class MOUSEINPUT(ctypes.Structure):
    _fields_ = [
        ("dx", wintypes.LONG),
        ("dy", wintypes.LONG),
        ("mouseData", wintypes.DWORD),
        ("dwFlags", wintypes.DWORD),
        ("time", wintypes.DWORD),
        ("dwExtraInfo", ctypes.POINTER(ctypes.c_ulong))
    ]

class INPUT_UNION(ctypes.Union):
    _fields_ = [
        ("ki", KEYBDINPUT),
        ("mi", MOUSEINPUT),
    ]

class INPUT(ctypes.Structure):
    _fields_ = [
        ("type", wintypes.DWORD),
        ("union", INPUT_UNION),
    ]

def get_vk(key_name):
    if not isinstance(key_name, str):
        return None
    normalized = KEY_ALIASES.get(key_name.lower(), key_name.lower())
    return VK_CODES.get(normalized)

def is_mouse_key(key_name):
    return key_name.startswith('mouse_')

def send_mouse_event(key_name, is_down):
    if key_name not in MOUSE_EVENTS:
        return
    down_flag, up_flag = MOUSE_EVENTS[key_name]
    flag = down_flag if is_down else up_flag
    if key_name in ('mouse_x1', 'mouse_x2'):
        xbutton = 0x0001 if key_name == 'mouse_x1' else 0x0002
        ctypes.windll.user32.mouse_event(flag, 0, 0, xbutton, 0)
    else:
        ctypes.windll.user32.mouse_event(flag, 0, 0, 0, 0)

def get_scan_code(vk):
    return ctypes.windll.user32.MapVirtualKeyW(vk, 0)

def send_key_input(key_name, is_down):
    """使用 SendInput 发送键盘事件（扫描码方式）"""
    vk = get_vk(key_name)
    if vk is None:
        return

    # 扩展键列表（方向键、功能键等）
    extended_keys = {
        0x26, 0x28, 0x25, 0x27,  # 方向键
        0x2D, 0x2E,              # Ins, Del
        0x21, 0x22, 0x23, 0x24,  # PageUp/Down, End, Home
    }
    flags = KEYEVENTF_SCANCODE
    if vk in extended_keys:
        flags |= KEYEVENTF_EXTENDEDKEY
    if not is_down:
        flags |= KEYEVENTF_KEYUP

    scan = get_scan_code(vk)
    ki = KEYBDINPUT()
    ki.wVk = vk
    ki.wScan = scan
    ki.dwFlags = flags
    ki.time = 0
    ki.dwExtraInfo = None

    inp = INPUT()
    inp.type = INPUT_KEYBOARD
    inp.union.ki = ki

    ctypes.windll.user32.SendInput(1, ctypes.byref(inp), ctypes.sizeof(inp))

# ---------- 鼠标移动函数 ----------
MOUSEEVENTF_MOVE = 0x0001
MOUSEEVENTF_ABSOLUTE = 0x8000
SM_CXSCREEN = 0
SM_CYSCREEN = 1

def get_screen_size():
    return user32.GetSystemMetrics(SM_CXSCREEN), user32.GetSystemMetrics(SM_CYSCREEN)

def set_cursor_pos(x, y):
    user32.SetCursorPos(x, y)

def send_mouse_down(button='left'):
    if button == 'left':
        ctypes.windll.user32.mouse_event(0x0002, 0, 0, 0, 0)
    elif button == 'right':
        ctypes.windll.user32.mouse_event(0x0008, 0, 0, 0, 0)
    elif button == 'middle':
        ctypes.windll.user32.mouse_event(0x0020, 0, 0, 0, 0)

def send_mouse_up(button='left'):
    if button == 'left':
        ctypes.windll.user32.mouse_event(0x0004, 0, 0, 0, 0)
    elif button == 'right':
        ctypes.windll.user32.mouse_event(0x0010, 0, 0, 0, 0)
    elif button == 'middle':
        ctypes.windll.user32.mouse_event(0x0040, 0, 0, 0, 0)

def get_cursor_pos():
    point = wintypes.POINT()
    user32.GetCursorPos(ctypes.byref(point))
    return point.x, point.y
