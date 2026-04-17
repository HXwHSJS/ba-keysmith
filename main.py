# main.py
import json
import time
from mapper import KeyMapper

def load_config(mapper, config_file='config.json'):
    try:
        with open(config_file, 'r', encoding='utf-8') as f:
            config = json.load(f)
        # 加载目标进程
        mapper.target_process = config.get('target_process', mapper.target_process)
        # 加载映射
        for item in config.get('mappings', []):
            if item.get('type') == 'macro':
                mapper.add_macro(item['trigger'], item['script'])
            else:
                mapper.add_simple_mapping(
                    trigger=item['trigger'],
                    target=item['target'],
                    mode=item.get('mode', 'hold')
                )
        print(f"已加载配置：{len(mapper.mappings)} 条映射")
    except FileNotFoundError:
        print("配置文件不存在，使用空映射")

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
