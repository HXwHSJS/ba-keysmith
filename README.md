# BA KeySmith

**BA KeySmith（简称 BAKS）** 是用于蔚蓝档案（Blue Archive）PC 端的键位映射与自定义宏的 Windows 工具。

它可以把用户实际按下的键转换为游戏内按键输入，例如 `q -> 1`；也可以把键盘或鼠标按键绑定为宏触发键，执行连续点击、等待、循环、组合键、鼠标拖拽等操作。

## 功能特性

- 支持键盘按键映射，例如 `q` 映射为 `1`。
- 支持 `hold` 和 `tap` 两种简单映射模式。
- 支持键盘与鼠标触发键，包括如 `shift`、`crtl`、`alt`、`mouse_left`、`mouse_right`、`mouse_middle`、`mouse_x1`、`mouse_x2`。
- 支持宏脚本指令：`press`、`release`、`tap`、`wait`、`loop`、`combo`、`drag`、`drag_rel`、`setpos`、`setpos_rel`。
- 宏编辑器内置语法检查、行号、语法高亮和上下文补全，包括触发的键值。
- 支持运行中编辑映射后自动热更新，无需手动停止再启动。
- 支持单个 exe 独立运行，配置会自动保存到用户数据目录。

## 下载使用

前往 GitHub Releases 下载最新版本：

[BA KeySmith Releases](https://github.com/HXwHSJS/ba-keysmith/releases)

推荐下载测试版压缩包（或者exe程序）：

```text
BAKeySmith-v0.2.0-beta.1-windows-x64.zip
```

解压后运行：

```text
BAKeySmith.exe
```

程序可作为单个 exe 独立使用，不需要把 `config.json` 放在 exe 同目录。

注意，BA KeySmith 需要以管理员身份运行，否则输入可能无法送达游戏窗口。

## KeyMapper 指令与语法参考手册

本文档列出了 KeyMapper 当前版本支持的所有宏脚本指令、简单映射模式及键名规范。

## 一、简单映射模式

在 GUI 中添加简单映射时，可选择以下两种模式：

| 模式 | 说明 |
| :--- | :--- |
| **Hold（跟随）** | 按下触发键时按下目标键，松开触发键时松开目标键。 |
| **Tap（单次点击）** | 无论触发键按多久，目标键只模拟一次按下并立即松开。 |

## 二、宏脚本指令

### 1. 键盘/鼠标按键指令

| 指令 | 格式 | 说明 |
| :--- | :--- | :--- |
| `press` | `press <键名>` | 按下并按住一个键，直到遇到对应的 `release`。 |
| `release` | `release <键名>` | 松开一个之前按住的键。 |
| `tap` | `tap <键名>` | 点击一个键。 |
| `combo` | `combo <键1> <键2> ...` | 同时按下多个键，短暂保持后按相反顺序松开，例如 `combo ctrl c`。 |

### 2. 流程控制指令

| 指令 | 格式 | 说明 |
| :--- | :--- | :--- |
| `wait` | `wait <毫秒>` | 暂停指定的毫秒数，支持小数。 |
| `loop` | `loop <次数>` ... `end` | 重复执行 `loop` 与 `end` 之间的指令块。次数为 `0` 或 `infinite` 时表示无限循环，松开触发键后自动停止。 |

### 3. 鼠标移动指令

| 指令 | 格式 | 说明 |
| :--- | :--- | :--- |
| `setpos` | `setpos <x> <y> [mouse]` | 将鼠标光标移动到指定坐标。若包含关键字 `mouse`，坐标解释为屏幕比例（0.0 到 1.0），否则为绝对像素坐标。 |
| `setpos_rel` | `setpos_rel <dx> <dy>` | 将鼠标光标相对当前位置移动 `(dx, dy)` 像素。 |
| `drag` | `drag <x> <y> [mouse] [按键]` | 从当前位置按下指定鼠标按键，移动到目标坐标后松开。 |
| `drag_rel` | `drag_rel <dx> <dy> [按键]` | 相对当前位置拖拽指定距离。 |

拖拽按键参数支持 `left`、`right`、`middle`、`x1`、`x2`，也支持 `mouse_left`、`mouse_right` 等完整鼠标键名。

## 三、键名规范

### 键盘键

| 类别 | 键名 |
| :--- | :--- |
| 字母键 | `a` 到 `z` |
| 数字键 | `0` 到 `9` |
| 功能键 | `f1` 到 `f12` |
| 方向键 | `up`, `down`, `left`, `right` |
| 控制键 | `enter`, `space`, `tab`, `escape`, `backspace`, `caps lock`, `shift`, `ctrl`, `alt` |
| 导航键 | `insert`, `delete`, `home`, `end`, `page up`, `page down` |
| 状态键 | `num lock`, `scroll lock` |

### 鼠标键

| 键名 | 对应按键 |
| :--- | :--- |
| `mouse_left` | 鼠标左键 |
| `mouse_right` | 鼠标右键 |
| `mouse_middle` | 鼠标中键 |
| `mouse_x1` | 鼠标侧键 1（前进键） |
| `mouse_x2` | 鼠标侧键 2（后退键） |

## 四、语法规则

- 每条指令占一行。
- 以 `#` 开头的行为注释，不会执行。
- 指令和参数不区分大小写，但建议使用小写。
- 参数之间用空格分隔。
- `loop` 与 `end` 必须成对出现，且支持嵌套。

## 五、完整示例

```text
# 示例：按下触发键后，快速点击数字1，等待50ms，
# 然后从屏幕中央拖拽左键到 (800, 600) 像素处

tap 1
wait 50
setpos 0.5 0.5 mouse
drag 800 600 left
```

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

运行时配置会自动保存到用户数据目录：

```text
%APPDATA%\BAKeySmith\config.json
```

该文件会记录你的键位映射、宏脚本和热键设置。首次运行新版程序时，如果检测到旧版同目录 `config.json`，会自动复制迁移到用户数据目录。

仓库中提供了 `config.example.json` 作为参考模板。

## 注意事项

- 本工具检测的目标进程名为 `BlueArchive.exe`。
- 如果映射或宏没有生效，请确认游戏进程正在运行，并尝试以管理员身份运行 BA KeySmith。
- 宏速度过快时，可能受到 Windows 输入事件调度或游戏自身输入处理速度限制。
- 鼠标触发键可以触发宏，但全局鼠标监听通常无法阻止原始鼠标点击继续传递给游戏。

## 免责声明

BA KeySmith 是 HXwHSJS/HX 制作的非官方个人工具，与 Nexon、Yostar、蔚蓝档案开发或发行团队没有从属、授权、赞助或背书关系。

请在遵守游戏条款、社区规则和当地法律法规的前提下使用本工具。使用本工具产生的后果由使用者自行承担。

## 开源协议

本项目基于 MIT License 开源，详见 [LICENSE](LICENSE)。
