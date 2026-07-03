import time

from mapper_state import MacroRuntime, SyntheticKeyEventFilter, TriggerStateStore


def test_trigger_state_replace_keeps_existing_dict_reference():
    store = TriggerStateStore({'a': True})
    states = store.states

    store.replace({'space': False})

    assert states is store.states
    assert states == {'space': False}


def test_synthetic_key_event_cancel_prevents_later_consume():
    event_filter = SyntheticKeyEventFilter()
    token = event_filter.mark('a', 'down')

    event_filter.cancel(token)

    assert event_filter.consume('a', 'down') is False


def test_macro_runtime_rejects_restart_until_old_thread_stops():
    runtime = MacroRuntime()

    def slow_macro():
        time.sleep(0.05)
        runtime.finish_current('a')

    assert runtime.start('a', slow_macro) is True
    assert runtime.start('a', slow_macro, join_timeout=0) is False
    assert runtime.should_stop('a') is True
    runtime.stop_all(join_timeout=0.2)
    runtime.clear_finished()
