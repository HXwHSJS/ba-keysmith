import threading
import time

from utils import normalize_key_name


class TriggerStateStore:
    def __init__(self, states=None):
        self._states = {}
        if states:
            self.replace(states)

    @property
    def states(self):
        return self._states

    def register(self, trigger):
        self._states[normalize_key_name(trigger)] = False

    def remove(self, trigger):
        self._states.pop(normalize_key_name(trigger), None)

    def clear(self):
        self._states.clear()

    def replace(self, states):
        self._states.clear()
        for trigger, pressed in states.items():
            self._states[normalize_key_name(trigger)] = bool(pressed)

    def is_pressed(self, trigger):
        return self._states.get(normalize_key_name(trigger), False)

    def press(self, trigger):
        self._states[normalize_key_name(trigger)] = True

    def release(self, trigger):
        self._states[normalize_key_name(trigger)] = False

    def reset_all(self):
        for trigger in list(self._states.keys()):
            self._states[trigger] = False


class SyntheticKeyEventFilter:
    def __init__(self, event_window=0.25):
        self._event_window = event_window
        self._events = {}
        self._lock = threading.Lock()

    def mark(self, key_name, event_type):
        key_name = normalize_key_name(key_name)
        token = (key_name, event_type, time.perf_counter())
        with self._lock:
            self._expire_locked()
            self._events.setdefault((key_name, event_type), []).append(token)
        return token

    def cancel(self, token):
        if token is None:
            return
        key_name, event_type, _ = token
        with self._lock:
            events = self._events.get((key_name, event_type))
            if not events:
                return
            try:
                events.remove(token)
            except ValueError:
                return
            if not events:
                self._events.pop((key_name, event_type), None)

    def consume(self, key_name, event_type):
        key_name = normalize_key_name(key_name)
        with self._lock:
            self._expire_locked()
            events = self._events.get((key_name, event_type))
            if not events:
                return False
            events.pop(0)
            if not events:
                self._events.pop((key_name, event_type), None)
            return True

    def _expire_locked(self):
        now = time.perf_counter()
        expired_keys = []
        for key, events in self._events.items():
            events[:] = [
                event for event in events
                if now - event[2] <= self._event_window
            ]
            if not events:
                expired_keys.append(key)
        for key in expired_keys:
            self._events.pop(key, None)


class MacroRuntime:
    def __init__(self):
        self.lock = threading.Lock()
        self.threads = {}
        self.stop_flags = {}

    def should_stop(self, trigger):
        return self.stop_flags.get(trigger, False)

    def request_stop(self, trigger):
        with self.lock:
            self.stop_flags[trigger] = True

    def start(self, trigger, target, args=(), join_timeout=0.1):
        with self.lock:
            old_thread = self.threads.get(trigger)
            if old_thread and old_thread.is_alive():
                self.stop_flags[trigger] = True
            else:
                old_thread = None

        if old_thread:
            old_thread.join(timeout=join_timeout)
            with self.lock:
                current = self.threads.get(trigger)
                if current and current.is_alive():
                    return False

        with self.lock:
            self.stop_flags[trigger] = False
            thread = threading.Thread(target=target, args=args, daemon=True)
            self.threads[trigger] = thread
        thread.start()
        return True

    def finish_current(self, trigger):
        with self.lock:
            if self.threads.get(trigger) is threading.current_thread():
                self.threads.pop(trigger, None)
                self.stop_flags.pop(trigger, None)

    def clear_finished(self):
        with self.lock:
            for trigger in list(self.threads.keys()):
                if not self.threads[trigger].is_alive():
                    self.threads.pop(trigger, None)
                    self.stop_flags.pop(trigger, None)

    def stop_all(self, join_timeout=0.5):
        with self.lock:
            for trigger in self.stop_flags:
                self.stop_flags[trigger] = True
            threads_to_join = [
                thread for thread in self.threads.values()
                if thread is not threading.current_thread() and thread.is_alive()
            ]
        for thread in threads_to_join:
            thread.join(timeout=join_timeout)
