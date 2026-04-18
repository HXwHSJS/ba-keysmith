# main.py
import json
import time
from mapper import KeyMapper
from utils import ensure_user_config_path

def load_config(mapper, config_file=None):
    config_path = config_file or ensure_user_config_path()[0]
    try:
        with open(config_path, 'r', encoding='utf-8') as f:
            config = json.load(f)
        # 加载目标进程
        mapper.target_process = config.get('target_process', mapper.target_process)
        # 加载映射
        loaded = 0
        for item in config.get('mappings', []):
            if not isinstance(item, dict) or not item.get('trigger'):
                continue
            try:
                if item.get('type') == 'macro':
                    mapper.add_macro(item['trigger'], item.get('script', ''))
                else:
                    mapper.add_simple_mapping(
                        trigger=item['trigger'],
                        target=item['target'],
                        mode=item.get('mode', 'hold')
                    )
                loaded += 1
            except (KeyError, ValueError) as e:
                print(f"跳过无效映射 {item.get('trigger', '<未知>')}: {e}")
        print(f"已加载配置：{loaded} 条映射")
    except FileNotFoundError:
        print(f"配置文件不存在，使用空映射：{config_path}")
    except json.JSONDecodeError as e:
        print(f"配置文件格式错误：第 {e.lineno} 行，第 {e.colno} 列。使用空映射。")

def main():
    mapper = KeyMapper()
    load_config(mapper)
    mapper.start()
    print("BA KeySmith 已启动。按 Ctrl+C 停止。")
    try:
        while True:
            time.sleep(1)
    except KeyboardInterrupt:
        mapper.stop()
        print("已停止")

if __name__ == '__main__':
    main()
