# script_compiler.py
from dataclasses import dataclass
from typing import List, Union

from utils import MOUSE_EVENTS, VK_CODES, normalize_key_name, normalize_mouse_button

@dataclass
class Instruction:
    opcode: str          # 'press', 'release', 'tap', 'wait', 'loop_start', 'loop_end', 'drag', 'drag_rel', 'setpos', 'setpos_rel', 'combo'
    args: List[Union[str, int, float, bool]]
    line: int = 0

class ScriptCompiler:
    def __init__(self):
        self.errors: List[str] = []

    @staticmethod
    def _normalize_key_arg(key_name: str) -> str:
        key_name = normalize_key_name(key_name)
        if key_name in VK_CODES or key_name in MOUSE_EVENTS:
            return key_name
        raise ValueError(f"未知键名: {key_name}")

    @staticmethod
    def _normalize_drag_button(button: str) -> str | None:
        key_name = normalize_mouse_button(button)
        if not key_name:
            return None
        return key_name.replace('mouse_', '', 1)

    def compile(self, script_text: str) -> tuple[List[Instruction], List[str]]:
        """返回 (指令列表, 错误列表)"""
        self.errors.clear()
        instructions = []
        lines = script_text.splitlines()
        loop_stack = []

        for line_num, raw_line in enumerate(lines, start=1):
            line = raw_line.split('#', 1)[0].strip()
            if not line or line.startswith('#'):
                continue

            parts = line.split()
            cmd = parts[0].lower()
            args = [arg.lower() for arg in parts[1:]]

            try:
                # ---------- 原有指令 ----------
                if cmd == 'press':
                    if len(args) != 1:
                        raise ValueError(f"press 需要一个参数（键名）")
                    instructions.append(Instruction('press', [self._normalize_key_arg(args[0])], line_num))

                elif cmd == 'release':
                    if len(args) != 1:
                        raise ValueError(f"release 需要一个参数（键名）")
                    instructions.append(Instruction('release', [self._normalize_key_arg(args[0])], line_num))

                elif cmd == 'tap':
                    if len(args) != 1:
                        raise ValueError(f"tap 需要一个参数（键名）")
                    instructions.append(Instruction('tap', [self._normalize_key_arg(args[0])], line_num))

                elif cmd == 'wait':
                    if len(args) != 1:
                        raise ValueError(f"wait 需要一个参数（毫秒数）")
                    ms = int(args[0])
                    if ms < 0:
                        raise ValueError(f"wait 时间不能为负数")
                    instructions.append(Instruction('wait', [ms], line_num))

                elif cmd == 'loop':
                    if len(args) != 1:
                        raise ValueError(f"loop 需要一个参数（次数，0 或 infinite 表示无限）")
                    count_str = args[0].lower()
                    if count_str in ('0', 'infinite'):
                        count = 0
                    else:
                        count = int(count_str)
                        if count <= 0:
                            raise ValueError(f"loop 次数必须大于0")
                    instructions.append(Instruction('loop_start', [count], line_num))
                    loop_stack.append(len(instructions) - 1)

                elif cmd == 'end':
                    if not loop_stack:
                        raise ValueError(f"多余的 end，没有对应的 loop")
                    start_idx = loop_stack.pop()
                    instructions[start_idx].args.append(len(instructions))
                    instructions.append(Instruction('loop_end', [], line_num))

                # ---------- 鼠标指令 ----------
                elif cmd == 'drag':
                    button = 'left'
                    use_mouse = False
                    if 'mouse' in args:
                        use_mouse = True
                        args.remove('mouse')
                    if len(args) >= 3:
                        maybe_button = self._normalize_drag_button(args[-1])
                        if maybe_button:
                            button = maybe_button
                            args.pop()
                    if len(args) == 2:
                        x, y = float(args[0]), float(args[1])
                        instructions.append(Instruction('drag', [x, y, use_mouse, button], line_num))
                    else:
                        raise ValueError("drag 需要 x 和 y 参数")

                elif cmd == 'drag_rel':
                    button = 'left'
                    if len(args) >= 3:
                        maybe_button = self._normalize_drag_button(args[-1])
                        if maybe_button:
                            button = maybe_button
                            args.pop()
                    if len(args) == 2:
                        dx, dy = int(args[0]), int(args[1])
                        instructions.append(Instruction('drag_rel', [dx, dy, button], line_num))
                    else:
                        raise ValueError("drag_rel 需要 dx 和 dy 参数")

                elif cmd == 'setpos':
                    use_mouse = False
                    if 'mouse' in args:
                        use_mouse = True
                        args.remove('mouse')
                    if len(args) == 2:
                        x, y = float(args[0]), float(args[1])
                        instructions.append(Instruction('setpos', [x, y, use_mouse], line_num))
                    else:
                        raise ValueError("setpos 需要 x 和 y 参数")

                elif cmd == 'setpos_rel':
                    if len(args) == 2:
                        dx, dy = int(args[0]), int(args[1])
                        instructions.append(Instruction('setpos_rel', [dx, dy], line_num))
                    else:
                        raise ValueError("setpos_rel 需要 dx 和 dy 参数")

                # ---------- 新增 combo 指令 ----------
                elif cmd == 'combo':
                    if len(args) < 1:
                        raise ValueError("combo 至少需要一个键名")
                    instructions.append(Instruction('combo', [self._normalize_key_arg(arg) for arg in args], line_num))

                else:
                    raise ValueError(f"未知指令: {cmd}")

            except ValueError as e:
                self.errors.append(f"第 {line_num} 行: {e}")

        if loop_stack:
            self.errors.append(f"存在未闭合的 loop（缺少 end）")

        return instructions, list(self.errors)
