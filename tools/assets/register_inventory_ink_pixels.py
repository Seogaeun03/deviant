"""Select the approved inventory ink PNGs and their Unity import settings."""

import hashlib
import json
from pathlib import Path
import uuid

from register_stage1_village_colour import META


ROOT = Path(__file__).resolve().parents[2]
ASSET = 'ui.inventory-ink-pixel-20261117'
SOURCE = 'ui/inventory-ink-pixel-20261117/runtime'
TARGET = 'unity/Assets/Deviant/UI/Theme/Resources/ApprovedTheme/sources/inventory-ink-pixel'


def create_meta(path, content):
    meta = ROOT/(path+'.meta')
    if not meta.exists():
        meta.write_text(content, encoding='utf-8', newline='\n')


def guid(path):
    return uuid.uuid5(uuid.NAMESPACE_URL, 'deviant:'+path).hex


def register():
    contract_path = ROOT/'docs/assets/manifests/unity-deployment.json'
    contract = json.loads(contract_path.read_text(encoding='utf-8'))
    sources = sorted((ROOT/'resources'/SOURCE).glob('*.png'))
    ids = {row['id'] for row in json.loads((ROOT/'unity/Assets/Deviant/Combat/Resources/RunInks.json').read_text(encoding='utf-8'))['inks']}
    if {path.stem for path in sources} != ids:
        raise ValueError('The deployment requires exactly one PNG per game ink')
    (ROOT/TARGET).mkdir(parents=True, exist_ok=True)
    create_meta(TARGET, f'fileFormatVersion: 2\nguid: {guid(TARGET)}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')
    entries = {row['target']:row for row in contract['files']}
    for source in sources:
        target = f'{TARGET}/{source.name}'
        raw = source.read_bytes()
        digest = hashlib.sha256(raw).hexdigest()
        row = {'target':target,'bytes':len(raw),'sha256':digest,'source':{'asset':ASSET,'path':f'{SOURCE}/{source.name}'}}
        previous = entries.get(target)
        if previous:
            replaced = {previous['sha256'], *previous.get('replaceHashes',[])}-{digest}
            if replaced:
                row['replaceHashes'] = sorted(replaced)
        entries[target] = row
        meta = META.format(guid=guid(target),wrap=1,px='.5',py='.5',readable=1)
        create_meta(target, meta.replace('textureType: 8','textureType: 0').replace('spriteMode: 1','spriteMode: 0'))
    contract['files'] = list(entries.values())
    contract_path.write_text(json.dumps(contract, ensure_ascii=False, indent=2)+'\n', encoding='utf-8', newline='\n')
    print(f'Registered {len(sources)} inventory ink textures')


if __name__ == '__main__':
    register()
