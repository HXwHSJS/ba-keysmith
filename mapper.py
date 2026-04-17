# mapper.py
import keyboard
import time
import threading
import psutil
from pynput import mouse as pynput_mouse
from utils import (
    is_mouse_key, send_mouse_event, send_key_input,
    set_cursor_pos, get_cursor_pos, get_screen_size,
    send_mouse_down, send_mouse_up
)
from script_compiler import ScriptCompiler

class KeyMapper:
    def __init__(self):
        self.mappings = {}
        self.pressed_keys = set()
        self.trigger_states = {}
        self.running = False
        self.enabled = True
        self.hook_handlers = {}
        self.target_process = "BlueArchive.exe"
        self._stop_lock = False
        self._macro_threads = {}
        self._macro_stop_flags = {}
        self.mouse_listener = None
        self.speed_factor = 1.0
        self.macro_safe_delay = 0.0
        self.macro_tap_hold = 0.0005
        self.macro_pointer_delay = 0.0005
        self.macro_combo_key_gap = 0.0005
        self.macro_combo_hold = 0.001

    def add_simple_mapping(self, trigger, target, mode='hold'):
        t = trigger.lower()
        self.mappings[t] = {
            'type': 'simple',
            'target': target.lower(),
            'mode': mode
        }
        self.trigger_states[t] = False

    def add_macro(self, trigger, script_text):
        t = trigger.lower()
        compiler = ScriptCompiler()
        instructions, errors = compiler.compile(script_text)
        if errors:
            raise ValueError("\n".join(errors))
        self.mappings[t] = {
            'type': 'macro',
            'script': script_text,
            'compiled': instructions
        }
        self.trigger_states[t] = False

    def remove_mapping(self, trigger):
        t = trigger.lower()
        if t in self.mappings:
            del self.mappings[t]
        if t in self.trigger_states:
            del self.trigger_states[t]

    def clear_mappings(self):
        self.mappings.clear()
        self.trigger_states.clear()

    def _is_game_active(self):
        for proc in psutil.process_iter(['name']):
            if proc.info['name'] == self.target_process:
                return True
        return False

    def _send_key(self, key_name, is_down):
        if is_mouse_key(key_name):
            send_mouse_event(key_name, is_down)
            if is_down:
                self.pressed_keys.add(key_name)
            else:
                self.pressed_keys.discard(key_name)
            return

        send_key_input(key_name, is_down)
        if is_down:
            self.pressed_keys.add(key_name)
        else:
            self.pressed_keys.discard(key_name)

    def _release_all(self):
        for key in list(self.pressed_keys):
            self._send_key(key, False)
            self._macro_pause(self.macro_pointer_delay)

    @staticmethod
    def _macro_pause(seconds):
        if seconds > 0:
            time.sleep(seconds)

    def _precise_wait(self, ms, trigger):
        if ms <= 0:
            return
        ms = ms * self.speed_factor
        end = time.perf_counter() + (ms / 1000.0)
        while time.perf_counter() < end:
            if self._macro_stop_flags.get(trigger, False):
                break
            time.sleep(0.0001)

    @staticmethod
    def _normalize_key_name(key_name):
        if isinstance(key_name, str):
            return key_name.strip().lower()
        return key_name

    def _resolve_args(self, opcode, args):
        if opcode in ('press', 'release', 'tap'):
            return [self._normalize_key_name(args[0])]
        if opcode == 'combo':
            return [self._normalize_key_name(key) for key in args]
        if opcode == 'drag':
            x, y, use_mouse, button = args
            return [x, y, use_mouse, self._normalize_key_name(button)]
        if opcode == 'drag_rel':
            dx, dy, button = args
            return [dx, dy, self._normalize_key_name(button)]
        return list(args)

    def _execute_macro(self, trigger, instructions):
        try:
            pc = 0
            loop_stack = []

            while pc < len(instructions):
                if self._macro_stop_flags.get(trigger, False):
                    break

                instr = instructions[pc]
                op = instr.opcode
                args = self._resolve_args(op, instr.args)

                if op == 'press':
                    self._send_key(args[0], True)
                    self._macro_pause(self.macro_safe_delay)
                elif op == 'release':
                    self._send_key(args[0], False)
                    self._macro_pause(self.macro_safe_delay)
                elif op == 'tap':
                    self._send_key(args[0], True)
                    self._macro_pause(self.macro_tap_hold)
                    self._send_key(args[0], False)
                    self._macro_pause(self.macro_safe_delay)
                elif op == 'wait':
                    self._precise_wait(args[0], trigger)
                elif op == 'loop_start':
                    count = args[0]
                    end_idx = args[1]
                    if count == 0:
                        loop_stack.append((pc, end_idx, 0))
                    else:
                        loop_stack.append((pc, end_idx, count))
                elif op == 'loop_end':
                    if loop_stack:
                        start_pc, end_pc, remaining = loop_stack.pop()
                        if remaining == 0:
                            pc = start_pc
                            loop_stack.append((start_pc, end_pc, 0))
                        elif remaining > 1:
                            pc = start_pc
                            loop_stack.append((start_pc, end_pc, remaining - 1))
                elif op == 'drag':
                    x, y, use_mouse, button = args
                    if use_mouse:
                        screen_w, screen_h = get_screen_size()
                        x = int(x * screen_w)
                        y = int(y * screen_h)
                    else:
                        x, y = int(x), int(y)
                    send_mouse_down(button)
                    self._macro_pause(self.macro_pointer_delay)
                    set_cursor_pos(x, y)
                    self._macro_pause(self.macro_pointer_delay)
                    send_mouse_up(button)
                    self._macro_pause(self.macro_safe_delay)
                elif op == 'drag_rel':
                    dx, dy, button = args
                    cur_x, cur_y = get_cursor_pos()
                    target_x, target_y = cur_x + dx, cur_y + dy
                    send_mouse_down(button)
                    self._macro_pause(self.macro_pointer_delay)
                    set_cursor_pos(target_x, target_y)
                    self._macro_pause(self.macro_pointer_delay)
                    send_mouse_up(button)
                    self._macro_pause(self.macro_safe_delay)
                elif op == 'setpos':
                    x, y, use_mouse = args
                    if use_mouse:
                        screen_w, screen_h = get_screen_size()
                        x = int(x * screen_w)
                        y = int(y * screen_h)
                    else:
                        x, y = int(x), int(y)
                    set_cursor_pos(x, y)
                    self._macro_pause(self.macro_safe_delay)
                elif op == 'setpos_rel':
                    dx, dy = args
                    cur_x, cur_y = get_cursor_pos()
                    set_cursor_pos(cur_x + dx, cur_y + dy)
                    self._macro_pause(self.macro_safe_delay)
                elif op == 'combo':
                    keys = args
                    for key in keys:
                        self._send_key(key, True)
                        self._macro_pause(self.macro_combo_key_gap)
                    self._macro_pause(self.macro_combo_hold)
                    for key in reversed(keys):
                        self._send_key(key, False)
                        self._macro_pause(self.macro_combo_key_gap)
                    self._macro_pause(self.macro_safe_delay)

                pc += 1

        finally:
            self._release_all()
            self._macro_threads.pop(trigger, None)
            self._macro_stop_flags.pop(trigger, None)

    def _make_handler(self, trigger, mapping):
        t = trigger.lower()
        mapping_type = mapping['type']

        def handler(event):
            if not self.enabled or not self._is_game_active():
                return

            if event.event_type == 'down':
                if self.trigger_states.get(t, False):
                    return False
                self.trigger_states[t] = True

                if mapping_type == 'simple':
                    target = mapping['target']
                    mode = mapping['mode']
                    # 简单映射 tap 模式：Esc 等键需要稍长按下时间（50ms），保证兼容
                    if mode == 'hold':
                        self._send_key(target, True)
                    elif mode == 'tap':
                        self._send_key(target, True)
                        time.sleep(0.05)      # 50ms，确保特殊键识别
                        self._send_key(target, False)

                elif mapping_type == 'macro':
                    if t in self._macro_threads and self._macro_threads[t].is_alive():
                        self._macro_stop_flags[t] = True
                        self._macro_threads[t].join(timeout=0.5)
                    self._macro_stop_flags[t] = False
                    thread = threading.Thread(
                        target=self._execute_macro,
                        args=(t, mapping['compiled']),
                        daemon=True
                    )
                    self._macro_threads[t] = thread
                    thread.start()

                return False

            elif event.event_type == 'up':
                if not self.trigger_states.get(t, False):
                    return False
                self.trigger_states[t] = False

                if mapping_type == 'simple' and mapping['mode'] == 'hold':
                    self._send_key(mapping['target'], False)

                elif mapping_type == 'macro':
                    self._macro_stop_flags[t] = True

                return False
            return True
        return handler

    @staticmethod
    def _mouse_button_name(button):
        btn_name = str(button).replace('Button.', '').lower()
        if btn_name in ('left', 'right', 'middle', 'x1', 'x2'):
            return f'mouse_{btn_name}'
        return None

    def _make_mouse_handler(self, mouse_mappings):
        handlers = {
            trigger: self._make_handler(trigger, mapping)
            for trigger, mapping in mouse_mappings.items()
        }

        class MouseEvent:
            def __init__(self, event_type):
                self.event_type = event_type

        def on_click(x, y, button, pressed):
            trigger = self._mouse_button_name(button)
            if trigger not in handlers:
                return
            event_type = 'down' if pressed else 'up'
            handlers[trigger](MouseEvent(event_type))

        return on_click

    def start(self):
        if self.running:
            return
        self.running = True
        mouse_mappings = {}
        for trigger, mapping in self.mappings.items():
            if is_mouse_key(trigger):
                mouse_mappings[trigger] = mapping
                continue
            if trigger == 'tab':
                pid = keyboard.on_press_key(trigger, self._make_handler(trigger, mapping), suppress=True)
                rid = keyboard.on_release_key(trigger, self._make_handler(trigger, mapping), suppress=True)
                self.hook_handlers[trigger] = (pid, rid)
            else:
                hid = keyboard.hook_key(trigger, self._make_handler(trigger, mapping), suppress=True)
                self.hook_handlers[trigger] = hid
        if mouse_mappings:
            self.mouse_listener = pynput_mouse.Listener(
                on_click=self._make_mouse_handler(mouse_mappings)
            )
            self.mouse_listener.start()

    def stop(self):
        if self._stop_lock:
            return
        self._stop_lock = True
        self.running = False
        for t in list(self._macro_stop_flags.keys()):
            self._macro_stop_flags[t] = True
        for t, thread in list(self._macro_threads.items()):
            if thread.is_alive():
                thread.join(timeout=0.5)
        self._macro_threads.clear()
        self._macro_stop_flags.clear()
        if self.mouse_listener:
            try:
                self.mouse_listener.stop()
            except:
                pass
            self.mouse_listener = None

        for trigger, handler in self.hook_handlers.items():
            try:
                if trigger == 'tab' and isinstance(handler, tuple):
                    keyboard.unhook(handler[0])
                    keyboard.unhook(handler[1])
                else:
                    keyboard.unhook(handler)
            except:
                pass
        self.hook_handlers.clear()
        self._release_all()
        self._stop_lock = False

    def toggle(self):
        self.enabled = not self.enabled
        if not self.enabled:
            self._release_all()
            for t in self._macro_stop_flags:
                self._macro_stop_flags[t] = True
        return self.enabled

    @staticmethod
    def check_game_running():
        for proc in psutil.process_iter(['name']):
            if proc.info['name'] == "BlueArchive.exe":
                return True
        return False
