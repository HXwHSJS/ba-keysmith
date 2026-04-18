# mapper.py
import keyboard
import time
import threading
import psutil
from pynput import mouse as pynput_mouse
from utils import (
    is_mouse_key, send_mouse_event, send_key_input,
    set_cursor_pos, get_cursor_pos, get_screen_size,
    send_mouse_down, send_mouse_up, normalize_key_name
)
from script_compiler import ScriptCompiler

class KeyMapper:
    PAIRED_HOOK_TRIGGERS = {'tab', 'ctrl', 'alt', 'shift'}
    SCAN_CODE_HOOK_TRIGGERS = {'alt'}
    TRIGGER_ALIASES = {
        'alt': ('alt', 'left alt', 'right alt', 'left menu', 'right menu', 'alt gr'),
    }

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
        self._game_check_interval = 0.5
        self._last_game_check = 0
        self._last_game_running = False

    def add_simple_mapping(self, trigger, target, mode='hold'):
        t = normalize_key_name(trigger)
        self.mappings[t] = {
            'type': 'simple',
            'target': normalize_key_name(target),
            'mode': mode
        }
        self.trigger_states[t] = False

    def add_macro(self, trigger, script_text):
        t = normalize_key_name(trigger)
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
        t = normalize_key_name(trigger)
        if t in self.mappings:
            del self.mappings[t]
        if t in self.trigger_states:
            del self.trigger_states[t]

    def clear_mappings(self):
        self.mappings.clear()
        self.trigger_states.clear()

    @staticmethod
    def compile_mapping_entries(mapping_entries):
        compiled_mappings = {}
        trigger_states = {}
        compiler = ScriptCompiler()

        for mapping in mapping_entries:
            if not isinstance(mapping, dict) or not mapping.get('trigger'):
                continue

            trigger = normalize_key_name(mapping['trigger'])
            if trigger in compiled_mappings:
                raise ValueError(f"触发键 {trigger} 重复")

            if mapping.get('type') == 'macro':
                script_text = mapping.get('script', '')
                instructions, errors = compiler.compile(script_text)
                if errors:
                    raise ValueError(f"触发键 {trigger} 的宏脚本有误:\n" + "\n".join(errors))
                compiled_mappings[trigger] = {
                    'type': 'macro',
                    'script': script_text,
                    'compiled': instructions
                }
            else:
                target = normalize_key_name(mapping.get('target', ''))
                if not target:
                    raise ValueError(f"触发键 {trigger} 缺少目标键")
                mode = mapping.get('mode', 'hold')
                if mode not in ('hold', 'tap'):
                    mode = 'hold'
                compiled_mappings[trigger] = {
                    'type': 'simple',
                    'target': target,
                    'mode': mode
                }
            trigger_states[trigger] = False

        return compiled_mappings, trigger_states

    def replace_compiled_mappings(self, compiled_mappings, trigger_states):
        self.mappings = dict(compiled_mappings)
        self.trigger_states = dict(trigger_states)

    def replace_mappings(self, mapping_entries):
        compiled_mappings, trigger_states = self.compile_mapping_entries(mapping_entries)
        self.replace_compiled_mappings(compiled_mappings, trigger_states)

    def _is_game_active(self):
        now = time.perf_counter()
        if now - self._last_game_check < self._game_check_interval:
            return self._last_game_running

        self._last_game_check = now
        self._last_game_running = self.check_game_running(self.target_process)
        return self._last_game_running

    def _send_key(self, key_name, is_down):
        key_name = normalize_key_name(key_name)
        if is_mouse_key(key_name):
            if not send_mouse_event(key_name, is_down):
                return False
            if is_down:
                self.pressed_keys.add(key_name)
            else:
                self.pressed_keys.discard(key_name)
            return True

        if not send_key_input(key_name, is_down):
            return False
        if is_down:
            self.pressed_keys.add(key_name)
        else:
            self.pressed_keys.discard(key_name)
        return True

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
        return normalize_key_name(key_name)

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
        local_pressed = set()

        def send_macro_key(key_name, is_down):
            key_name = normalize_key_name(key_name)
            if not self._send_key(key_name, is_down):
                return
            if is_down:
                local_pressed.add(key_name)
            else:
                local_pressed.discard(key_name)

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
                    send_macro_key(args[0], True)
                    self._macro_pause(self.macro_safe_delay)
                elif op == 'release':
                    send_macro_key(args[0], False)
                    self._macro_pause(self.macro_safe_delay)
                elif op == 'tap':
                    send_macro_key(args[0], True)
                    self._macro_pause(self.macro_tap_hold)
                    send_macro_key(args[0], False)
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
                        send_macro_key(key, True)
                        self._macro_pause(self.macro_combo_key_gap)
                    self._macro_pause(self.macro_combo_hold)
                    for key in reversed(keys):
                        send_macro_key(key, False)
                        self._macro_pause(self.macro_combo_key_gap)
                    self._macro_pause(self.macro_safe_delay)

                pc += 1

        finally:
            for key in list(local_pressed):
                self._send_key(key, False)
                self._macro_pause(self.macro_pointer_delay)
            if self._macro_threads.get(trigger) is threading.current_thread():
                self._macro_threads.pop(trigger, None)
                self._macro_stop_flags.pop(trigger, None)

    def _make_handler(self, trigger, mapping):
        t = normalize_key_name(trigger)
        mapping_type = mapping['type']

        def handler(event):
            if not self.enabled or not self._is_game_active():
                if event.event_type == 'up' and self.trigger_states.get(t, False):
                    self.trigger_states[t] = False
                    if mapping_type == 'simple' and mapping['mode'] == 'hold':
                        self._send_key(mapping['target'], False)
                    elif mapping_type == 'macro':
                        self._macro_stop_flags[t] = True
                return True

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
                        self._macro_threads[t].join(timeout=0.1)
                        if self._macro_threads.get(t) and self._macro_threads[t].is_alive():
                            return False
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

    def _scan_code_hooks_for_trigger(self, trigger, mapping):
        handler = self._make_handler(trigger, mapping)
        scan_codes = []
        for key_name in self.TRIGGER_ALIASES.get(trigger, (trigger,)):
            try:
                scan_codes.extend(keyboard.key_to_scan_codes(key_name, error_if_missing=False))
            except Exception:
                continue

        hooks = []
        for scan_code in dict.fromkeys(scan_codes):
            hooks.append(keyboard.hook_key(
                scan_code,
                lambda event, callback=handler: callback(event),
                suppress=True
            ))
        return tuple(hooks)

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
        try:
            for trigger, mapping in self.mappings.items():
                trigger = normalize_key_name(trigger)
                if is_mouse_key(trigger):
                    mouse_mappings[trigger] = mapping
                    continue
                if trigger in self.SCAN_CODE_HOOK_TRIGGERS:
                    self.hook_handlers[trigger] = self._scan_code_hooks_for_trigger(trigger, mapping)
                    continue
                if trigger in self.PAIRED_HOOK_TRIGGERS:
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
        except Exception:
            self.stop()
            raise

    def stop(self):
        if self._stop_lock:
            return
        self._stop_lock = True
        try:
            self.running = False
            self.stop_active_macros()
            self._macro_threads.clear()
            self._macro_stop_flags.clear()
            if self.mouse_listener:
                try:
                    self.mouse_listener.stop()
                except:
                    pass
                self.mouse_listener = None

            for trigger, handler in self.hook_handlers.items():
                handlers = handler if isinstance(handler, tuple) else (handler,)
                for hook in handlers:
                    try:
                        keyboard.unhook(hook)
                    except:
                        pass
            self.hook_handlers.clear()
            self._release_all()
        finally:
            self._stop_lock = False

    def stop_active_macros(self, join_timeout=0.5):
        for t in list(self._macro_stop_flags.keys()):
            self._macro_stop_flags[t] = True
        current_thread = threading.current_thread()
        for thread in list(self._macro_threads.values()):
            if thread is not current_thread and thread.is_alive():
                thread.join(timeout=join_timeout)

    def set_enabled(self, enabled):
        self.enabled = enabled
        if not enabled:
            self.stop_active_macros(join_timeout=0.5)
            self._release_all()
            for trigger in list(self.trigger_states.keys()):
                self.trigger_states[trigger] = False
        return self.enabled

    def toggle(self):
        return self.set_enabled(not self.enabled)

    @staticmethod
    def check_game_running(process_name="BlueArchive.exe"):
        process_name = (process_name or "").lower()
        for proc in psutil.process_iter(['name']):
            try:
                if (proc.info.get('name') or '').lower() == process_name:
                    return True
            except (psutil.NoSuchProcess, psutil.AccessDenied, psutil.ZombieProcess):
                continue
        return False
