import re,json
from pathlib import Path
def blocks(path):
    text=Path(path).read_text(encoding='utf-8-sig')
    return {m.group(1):m.group(2) for m in re.finditer(r'^--- !u!\d+ &(\d+)[^\n]*\n(.*?)(?=^--- !u!|\Z)',text,re.M|re.S)}
a=blocks('Captures/Stage2_Z3/before.unity')
b=blocks('Assets/01_Scenes/MapDesign/MapDesign_Stage2.unity')
def tree(data,name):
    go=next(k for k,v in data.items() if v.startswith('GameObject:') and ('  m_Name: '+name+'\n') in v)
    ids={go}
    def visit(g):
        v=data[g]
        comp=re.search(r'  m_Component:(.*?)  m_Layer:',v,re.S)
        for c in re.findall(r'fileID: (\d+)',comp.group(1)):
            ids.add(c)
            if data[c].startswith('Transform:'):
                children=re.search(r'  m_Children:(.*?)  m_Father:',data[c],re.S)
                for child in re.findall(r'fileID: (\d+)',children.group(1)):
                    cg=re.search(r'm_GameObject: \{fileID: (\d+)',data[child]).group(1)
                    ids.add(cg);visit(cg)
    visit(go)
    return ids
report={}
for name in ['Z0_Entrance','Z1_GlassMemory_5x6','Z2_CooperativeCrossing','Z4_LowPriority_Interchange','Z5_16Rooms_InverseDoors','Z6_Lava_IronSwings','Z7_Narrowing_Roller_Ascent','Z8_Reunion_CrystalHall']:
    ids=tree(a,name)
    changed=[k for k in ids if a[k]!=b.get(k)]
    report[name]={'objects_and_components':len(ids),'changed':len(changed)}
Path('Captures/Stage2_Z3/unchanged-zones.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print(json.dumps(report))

