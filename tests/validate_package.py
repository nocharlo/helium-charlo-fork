"""Static package validation; does not execute Playnite or WPF."""
import argparse
from pathlib import Path
import re
import struct
import xml.etree.ElementTree as ET
import zipfile

parser = argparse.ArgumentParser()
parser.add_argument('--build-dir', type=Path, required=True, help='Fresh Helium-TestHarness Build output; keeps approved packages untouched')
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
build = args.build_dir.resolve()
install = build / 'verify-extension'
packages = build / 'packages'
source_manifest = (root / 'source/HeliumHybrid/extension.yaml').read_text()
theme_manifest = (root / 'theme/theme.yaml').read_text()
version = re.search(r'(?m)^Version:\s*(\S+)', source_manifest).group(1)
assert re.search(r'(?m)^Version:\s*(\S+)', theme_manifest).group(1) == version
assert f'AssemblyVersion("{version}.0")' in (root / 'source/HeliumHybrid/Plugin.cs').read_text()
assert f'Name: Helium Navigator v{version}' in source_manifest
assert f'Name: Helium (charlo fork) v{version}' in theme_manifest

xaml = list((root / 'theme').rglob('*.xaml')) + [root / 'source/HeliumHybrid/Navigator.xaml']
for file in xaml:
    ET.parse(file)
xname = '{http://schemas.microsoft.com/winfx/2006/xaml}Name'
nav = ET.parse(root / 'source/HeliumHybrid/Navigator.xaml')
names = {n.attrib[xname] for n in nav.iter() if xname in n.attrib}
assert {'CollapseButton','Search','Grouping','Count','Status','Games','ResizeGrip','BackButton'} <= names
contract = {'RecentButton':'Button', 'ReadyButton':'Button', 'FiltersButton':'Button',
            'CloseFilters':'Button', 'ClearAllFilters':'Button', 'FilterIndicator':'TextBlock',
            'AdvancedPanel':'ScrollViewer', 'FilterSections':'StackPanel'}
for name, kind in contract.items():
    matches = [n for n in nav.iter() if n.attrib.get(xname) == name]
    assert len(matches) == 1 and matches[0].tag.endswith('}' + kind), (name, kind)
lib = ET.parse(root / 'theme/Views/Library.xaml')
assert [n for n in lib.iter() if n.attrib.get(xname) == 'HeliumCharlo_Navigator']
assert [n for n in lib.iter() if n.attrib.get(xname) == 'HeliumCharlo_SwitchDetails']
assert [n for n in lib.iter() if n.attrib.get(xname) == 'HeliumCharlo_SwitchGrid']
overlays = [n for n in lib.iter() if n.attrib.get(xname) == 'HeliumCharlo_Reopen']
assert len(overlays) == 1 and overlays[0].attrib['Visibility'] == 'Collapsed'
details = ET.parse(root / 'theme/Views/LibraryDetailsView.xaml')
assert [n for n in details.iter() if n.attrib.get(xname) == 'HeliumCharlo_NativeDetailsList']
source = (root / 'source/HeliumHybrid/Navigator.cs').read_text()
assert all(f'FindName("{name}")' in source for name in contract)
assert 'OpenNativeDetails(id)' in source and 'BackButton' in source
assert 'GetSettingsView' in (root / 'source/HeliumHybrid/Plugin.cs').read_text()
assert 'Settings.MigrateNavigation();' in (root / 'source/HeliumHybrid/Plugin.cs').read_text()
settings = (root / 'source/HeliumHybrid/Settings.cs').read_text()
assert 'SidebarClickAction' in settings and 'GridCoverClickAction' in settings
assert 'SteamMode ? 0 : 1' in settings and 'OpenFromGridCover ? 0 : 1' in settings
assert 'ShouldSerializeSteamMode() => false' in settings and 'ShouldSerializeOpenFromGridCover() => false' in settings
assert 'Open Full Details View' in settings and 'Open Grid Details Panel' in settings
assert 'ShowGameSideBarCommand' in (root / 'theme/Views/Library.xaml').read_text()
assert 'command.Execute(selected)' in source and 'panelRequested' in source
assert 'sidebarClickTimer.Stop()' in source and 'coverClickTimer.Stop()' in source
assert 'PreviewMouseLeftButtonDownEvent' in source and 'GetDoubleClickTime' in source
assert 'Width = collapsed ? 0 : prefs.Width' in source
assert 'b59e3c9f-9c52-4582-a332-0716b56cf023' in source_manifest
assert (install / 'extension.yaml').read_text() == source_manifest
assert 'e5d3a4ba-1cc7-42b1-a090-8d4d3a922b23' in theme_manifest
assert 'e5d3a4ba-1cc7-42b1-a090-8d4d3a922b23' in (root / 'theme/themeExtras.yaml').read_text()
binary = (install / 'HeliumCharlo.dll').read_bytes()
assert binary[:2] == b'MZ'
pe = struct.unpack_from('<I', binary, 0x3c)[0]
assert binary[pe:pe+4] == b'PE\x00\x00'
assert struct.unpack_from('<H', binary, pe+24)[0] == 0x10b
assert b'HeliumHybrid.Navigator.xaml' in binary
for name, manifest in [(f'Helium-Navigator-v{version}.pext','extension.yaml'), (f'Helium-charlo-fork-v{version}.pthm','theme.yaml')]:
    with zipfile.ZipFile(packages / name) as z:
        assert z.testzip() is None
        assert manifest in z.namelist()
        assert not any('..' in Path(f).parts for f in z.namelist())
        if name.endswith('.pext'):
            assert set(z.namelist()) == {'HeliumCharlo.dll','extension.yaml'}
            assert z.read('HeliumCharlo.dll') == binary
            assert z.read('extension.yaml') == (install / 'extension.yaml').read_bytes()
        else:
            expected = {str(p.relative_to(root / 'theme')).replace('\\', '/') for p in (root / 'theme').rglob('*') if p.is_file()}
            assert set(z.namelist()) == expected
            assert all(z.read(path) == (root / 'theme' / path).read_bytes() for path in expected)
print(f'PASS: {len(xaml)} XAML/XML; 2 paired v{version} packages; .NET DLL; stable IDs.')
