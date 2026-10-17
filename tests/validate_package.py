"""Verificação estática: não inicia Playnite/WPF. Python 3.9+."""
from pathlib import Path
import struct
import xml.etree.ElementTree as ET
import zipfile

root = Path(__file__).resolve().parents[1]
xaml = list((root / 'theme').rglob('*.xaml')) + [root / 'source/HeliumHybrid/Navigator.xaml']
for path in xaml:
    ET.parse(path)
qn = '{http://schemas.microsoft.com/winfx/2006/xaml}Name'
nav = ET.parse(root / 'source/HeliumHybrid/Navigator.xaml')
names = {node.attrib[qn] for node in nav.iter() if qn in node.attrib}
assert {'CollapseButton','Search','OptionsButton','Grouping','Count','Status','Games','ResizeGrip',
        'OptionsPopup','LayoutMode','SteamMode','LocateCover','PreserveScroll'} <= names
lib = ET.parse(root / 'theme/Views/Library.xaml')
libnames = {node.attrib[qn] for node in lib.iter() if qn in node.attrib}
assert {'HeliumCharlo_Navigator','HeliumCharlo_Reveal','PART_ViewGrid','PART_ViewDetails'} <= libnames
view = ET.parse(root / 'theme/Views/LibraryGridView.xaml')
viewnames = {node.attrib[qn] for node in view.iter() if qn in node.attrib}
assert {'PART_ControlGameView','PART_ListGames','PART_SliderZoom','HybridGridRoot'} <= viewnames
manifest = (root / 'install/extension.yaml').read_text()
assert 'HeliumCharlo_84fd550a-26c3-43d6-8c09-77c48e5a27a6' in manifest
assert 'Module: HeliumCharlo.dll' in manifest
assert 'a6792e69-2874-4243-9041-2656a163c40f' in (root / 'theme/theme.yaml').read_text()
assert 'a6792e69-2874-4243-9041-2656a163c40f' in (root / 'theme/themeExtras.yaml').read_text()
binary = (root / 'install/HeliumCharlo.dll').read_bytes()
assert binary[:2] == b'MZ'
pe = struct.unpack_from('<I', binary, 0x3c)[0]
assert binary[pe:pe+4] == b'PE\x00\x00'
assert struct.unpack_from('<H', binary, pe+24)[0] == 0x10b
assert struct.unpack_from('<II', binary, pe+24+96+14*8)[0] != 0
assert b'HeliumHybrid.Navigator.xaml' in binary
assert b'HeliumCharlo' in binary
for name, manifestname in [('HeliumCharlo-Navigator-0.2.0.pext','extension.yaml'), ('HeliumCharlo-0.2.0.pthm','theme.yaml')]:
    with zipfile.ZipFile(root / 'packages' / name) as z:
        assert z.testzip() is None
        assert manifestname in z.namelist()
        if name.endswith('.pext'):
            assert set(z.namelist()) == {'HeliumCharlo.dll','extension.yaml'}
            assert z.read('HeliumCharlo.dll') == binary
assert zipfile.ZipFile(root / 'reference/HeliumHybrid-KKCD-0.1.0.zip').testzip() is None
print(f'PASS: {len(xaml)} XAML/XML; 2 pacotes; DLL .NET; versão 0.1 preservada.')
