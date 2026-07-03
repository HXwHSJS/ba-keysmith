import time

from mapper import KeyMapper


class Event:
    def __init__(self, event_type):
        self.event_type = event_type


def active_mapper():
    mapper = KeyMapper()
    mapper.running = True
    mapper._is_game_active = lambda: True
    mapper._macro_pause = lambda seconds: None
    return mapper


def test_unrelated_synthetic_event_does_not_block_real_trigger():
    mapper = active_mapper()
    sent = []
    mapper.add_simple_mapping('space', 'esc', 'tap')
    mapper.add_simple_mapping('1', '2', 'tap')
    mapper._send_key = lambda key, is_down: sent.append((key, is_down)) or True
    mapper.synthetic_key_events.mark('1', 'down')

    handler = mapper._make_handler('space', mapper.mappings['space'])

    assert handler(Event('down')) is False
    assert sent == [('escape', True), ('escape', False)]
    assert mapper.trigger_states['space'] is True


def test_synthetic_same_key_event_is_passed_without_triggering_mapping():
    mapper = active_mapper()
    sent = []
    mapper.add_simple_mapping('a', '1', 'tap')
    mapper._send_key = lambda key, is_down: sent.append((key, is_down)) or True
    mapper.synthetic_key_events.mark('a', 'down')

    handler = mapper._make_handler('a', mapper.mappings['a'])

    assert handler(Event('down')) is True
    assert sent == []
    assert mapper.trigger_states['a'] is False


def test_mouse_macro_trigger_recovers_when_release_was_missed():
    mapper = active_mapper()
    sent = []
    mapper.add_macro('mouse_x1', 'tap 1')
    mapper._send_key = lambda key, is_down: sent.append((key, is_down)) or True

    handler = mapper._make_handler('mouse_x1', mapper.mappings['mouse_x1'])

    assert handler(Event('down')) is False
    deadline = time.perf_counter() + 1
    while mapper._macro_threads and time.perf_counter() < deadline:
        time.sleep(0.001)
    assert mapper.trigger_states['mouse_x1'] is True

    assert handler(Event('down')) is False
    deadline = time.perf_counter() + 1
    while mapper._macro_threads and time.perf_counter() < deadline:
        time.sleep(0.001)

    assert sent == [('1', True), ('1', False), ('1', True), ('1', False)]
