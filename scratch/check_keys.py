import os, re, json
keys = set()
for root, dirs, files in os.walk('Stokendra'):
    for file in files:
        if file.endswith('.cs') or file.endswith('.axaml'):
            with open(os.path.join(root, file), 'r', encoding='utf-8') as f:
                content = f.read()
                for match in re.findall(r'\{infra:L\s+([a-zA-Z0-9_]+)\}', content):
                    keys.add(match)
                for match in re.findall(r'LocalizationManager\.L\(\"([a-zA-Z0-9_]+)\"', content):
                    keys.add(match)

with open('Stokendra/Resources/lang_tr.json', encoding='utf-8') as f: tr = set(json.load(f).keys())
missing = keys - tr
print('Keys used in code but missing in JSON:')
for m in sorted(missing):
    print('-', m)
