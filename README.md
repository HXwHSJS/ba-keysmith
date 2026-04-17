# BA KeySmith

**BA KeySmith（简称 BAKS）** 是由 **HX** 开发的 Windows 桌面工具，用于蔚蓝档案（Blue Archive）PC 端的键位映射与自定义宏。

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

如果游戏以管理员权限运行，BA KeySmith 也可能需要以管理员身份运行，否则输入可能无法送达游戏窗口。

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
