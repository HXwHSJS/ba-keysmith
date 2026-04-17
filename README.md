# BA KeySmith

**BA KeySmith（简称 BAKS）** 是由 **HXwHSJS** 开发的 Windows 桌面工具，用于蔚蓝档案（Blue Archive）PC 端的键位映射与自定义宏。

它可以把用户实际按下的键转换为游戏内按键输入，例如 `q -> 1`；也可以把键盘或鼠标按键绑定为宏触发键，执行连续点击、等待、循环、组合键、鼠标拖拽等操作。

## 功能特性

- 支持键盘按键映射，例如 `q` 映射为 `1`。
- 支持 `hold` 和 `tap` 两种简单映射模式。
- 支持键盘与鼠标触发键，包括 `mouse_left`、`mouse_right`、`mouse_middle`、`mouse_x1`、`mouse_x2`。
- 支持宏脚本指令：`press`、`release`、`tap`、`wait`、`loop`、`combo`、`drag`、`drag_rel`、`setpos`、`setpos_rel`。
- 宏编辑器内置语法检查、行号、语法高亮和上下文补全。
- 编辑宏时会自动暂停当前映射，避免在编辑窗口输入内容时误触发游戏输入。
- 提供现代化 Tkinter 桌面界面。

## 下载使用

前往 GitHub Releases 下载最新版本：

[BA KeySmith Releases](https://github.com/HXwHSJS/ba-keysmith/releases)

推荐下载压缩包版本，例如：

```text
BAKeySmith-v0.1.0-windows-x64.zip
```

解压后运行：

```text
BAKeySmith.exe
```

注意，BA KeySmith需要以管理员身份运行，否则输入可能无法送达游戏窗口。
# KeyMapper 指令与语法参考手册（当前稳定版）

本文档列出了 KeyMapper 当前版本支持的所有宏脚本指令、简单映射模式及键名规范。

---

## 一、简单映射模式

在 GUI 中添加简单映射时，可选择以下两种模式：

| 模式 | 说明 |
| :--- | :--- |
| **Hold（跟随）** | 按下触发键时按下目标键，松开触发键时松开目标键。 |
| **Tap（单次点击）** | 无论触发键按多久，目标键只模拟一次按下并立即松开（约 50ms 延迟以保证识别）。 |

---

## 二、宏脚本指令

### 1. 键盘/鼠标按键指令

| 指令 | 格式 | 说明 |
| :--- | :--- | :--- |
| `press` | `press <键名>` | 按下并按住一个键（直到遇到对应的 `release`）。 |
| `release` | `release <键名>` | 松开一个之前按住的键。 |
| `tap` | `tap <键名>` | 点击一个键（按下后立即松开，约 1~2ms 极速执行）。 |
| `combo` | `combo <键1> <键2> ...` | 同时按下多个键，短暂保持后按相反顺序松开，用于模拟组合键（如 `combo ctrl c`）。 |

### 2. 流程控制指令

| 指令 | 格式 | 说明 |
| :--- | :--- | :--- |
| `wait` | `wait <毫秒>` | 暂停指定的毫秒数。支持小数（如 `wait 0.5` 表示 0.5 毫秒）。 |
| `loop` | `loop <次数>` … `end` | 重复执行 `loop` 与 `end` 之间的指令块。次数为 `0` 或 `infinite` 时表示无限循环，松开触发键后自动停止。 |

### 3. 鼠标移动指令

| 指令 | 格式 | 说明 |
| :--- | :--- | :--- |
| `setpos` | `setpos <x> <y> [mouse]` | 将鼠标光标移动到指定坐标。若包含关键字 `mouse`，则坐标解释为屏幕比例（0.0~1.0），否则为绝对像素坐标。 |
| `setpos_rel` | `setpos_rel <dx> <dy>` | 将鼠标光标相对当前位置移动 (dx, dy) 像素。 |
| `drag` | `drag <x> <y> [mouse] [按键]` | 从当前位置按下指定鼠标按键（默认 `left`），移动到目标坐标后松开。 |
| `drag_rel` | `drag_rel <dx> <dy> [按键]` | 相对当前位置拖拽指定距离。 |

**拖拽按键参数**：`left`（默认）、`right`、`middle`。

---

## 三、键名规范

### 键盘键

| 类别 | 键名 |
| :--- | :--- |
| 字母键 | `a` ~ `z` |
| 数字键 | `0` ~ `9` |
| 功能键 | `f1` ~ `f12` |
| 方向键 | `up`, `down`, `left`, `right` |
| 控制键 | `enter`, `space`, `tab`, `escape`, `backspace`, `shift`, `ctrl`, `alt` |

### 鼠标键

| 键名 | 对应按键 |
| :--- | :--- |
| `mouse_left` | 鼠标左键 |
| `mouse_right` | 鼠标右键 |
| `mouse_middle` | 鼠标中键 |
| `mouse_x1` | 鼠标侧键 1（前进键） |
| `mouse_x2` | 鼠标侧键 2（后退键） |

---

## 四、语法规则

- 每条指令占一行。
- 以 `#` 开头的行为注释，不会执行。
- 指令和参数不区分大小写，但建议使用小写。
- 参数之间用空格分隔。
- `loop` 与 `end` 必须成对出现，且支持嵌套。

---

## 五、完整示例

```plaintext
# 示例：按下触发键后，快速点击数字1，等待50ms，
# 然后从屏幕中央拖拽左键到 (800, 600) 像素处

tap 1
wait 50
setpos mouse 0.5 0.5
drag 800 600 left

## 宏脚本示例

无限循环点击数字键 `1` 并点击鼠标左键：

```text
loop 0
tap 1
tap mouse_left
end
```

鼠标右键触发并循环点击 `Esc`：

```text
loop 0
tap esc
end
```

常用指令示例：

```text
tap esc
wait 100
combo ctrl c
drag_rel 120 0 left
```

## 常用键名

```text
esc
tab
enter
space
backspace
shift
ctrl
alt
up
down
left
right
mouse_left
mouse_right
mouse_middle
mouse_x1
mouse_x2
```

## 从源码运行

创建并激活虚拟环境，然后安装依赖：

```powershell
python -m venv venv
.\venv\Scripts\pip install -r requirements.txt
```

从源码启动：

```powershell
.\venv\Scripts\python gui.py
```

## 打包构建

使用 PyInstaller 打包单文件 exe：

```powershell
.\venv\Scripts\python -m PyInstaller BAKeySmith.spec --clean -y
```

生成文件位置：

```text
dist/BAKeySmith.exe
```

## 配置说明

运行时配置保存在本地 `config.json` 中。该文件会记录你的键位映射、宏脚本和热键设置。

`config.json` 已被 `.gitignore` 忽略，不会上传到仓库。仓库中提供了 `config.example.json` 作为参考模板。

## 注意事项

- 本工具检测的目标进程名为 `BlueArchive.exe`。
- 如果映射或宏没有生效，请确认游戏进程正在运行，并尝试以管理员身份运行 BA KeySmith。
- 宏速度过快时，可能受到 Windows 输入事件调度或游戏自身输入处理速度限制。
- 鼠标触发键可以触发宏，但全局鼠标监听通常无法阻止原始鼠标点击继续传递给游戏。

## 免责声明

BA KeySmith 是 HX 制作的非官方个人工具，与 Nexon、Yostar、蔚蓝档案开发或发行团队没有从属、授权、赞助或背书关系。

请在遵守游戏条款、社区规则和当地法律法规的前提下使用本工具。使用本工具产生的后果由使用者自行承担。

## 开源协议

本项目基于 MIT License 开源，详见 [LICENSE](LICENSE)。
