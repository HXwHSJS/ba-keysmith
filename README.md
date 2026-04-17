# BA KeySmith

BA KeySmith (BAKS) is a Windows key mapping and macro tool for the PC version of Blue Archive.

Author: HX

## Features

- Map one keyboard key to another key, for example `q -> 1`.
- Support hold and tap mapping modes.
- Create macro bindings with simple script commands such as `tap`, `wait`, `loop`, `combo`, `drag`, and mouse actions.
- Support keyboard and mouse trigger keys, including `mouse_left`, `mouse_right`, `mouse_middle`, `mouse_x1`, and `mouse_x2`.
- Pause mappings automatically while editing macro scripts to avoid accidental input.
- Modern Tkinter-based desktop UI with contextual macro autocomplete.

## Macro Example

```text
loop 0
tap 1
tap mouse_left
end
```

Useful command examples:

```text
tap esc
wait 100
combo ctrl c
drag_rel 120 0 left
```

## Development

Create and activate a virtual environment, then install dependencies:

```powershell
python -m venv venv
.\venv\Scripts\pip install -r requirements.txt
```

Run from source:

```powershell
.\venv\Scripts\python gui.py
```

Build a single-file executable:

```powershell
.\venv\Scripts\python -m PyInstaller BAKeySmith.spec --clean -y
```

The executable will be generated at:

```text
dist/BAKeySmith.exe
```

## Configuration

Runtime configuration is stored in `config.json`, which is intentionally ignored by git because it contains local user mappings.

Use `config.example.json` as a starter template if needed.

## Notes

- This tool targets the Windows PC client process `BlueArchive.exe`.
- If the game runs with elevated privileges, BA KeySmith may also need to be run as administrator.
- Very fast macros may be limited by game input handling and Windows input event scheduling.

## Disclaimer

BA KeySmith is an unofficial fan-made utility by HX. It is not affiliated with, endorsed by, or sponsored by Nexon, Yostar, or the Blue Archive development/publishing teams. Use responsibly and follow the game's terms and community rules.

## License

This project is released under the MIT License. See [LICENSE](LICENSE).
