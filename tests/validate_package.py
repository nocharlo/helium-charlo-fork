"""Static package validation: does not execute Playnite/WPF."""
from pathlib import Path
import struct
import xml.etree.ElementTree as ET
import zipfile
root = Path(__file__).resolve().parents[1]
xaml = list((root / 'theme').rglob('*.xaml')) + [root / 'source/HeliumHybrid/Navigator.xaml']
for file in xaml: ET.parse(file)
xname = '{http://schemas.microsoft.com/winfx/2006/xaml}Name'
nav = ET.parse(root / 'source/HeliumHybrid/Navigator.xaml')
names = {n.attrib[xname] for n in nav.iter() if xname in n.attrib}
assert {'CollapseButton','Search','Grouping','Count','Status','Games','ResizeGrip','BackButton'} <= names
lib = ET.parse(root / 'theme/Views/Library.xaml')
assert [n for n in lib.iter() if n.attrib.get(xname) == 'HeliumCharlo_Navigator']
assert [n for n in lib.iter() if n.attrib.get(xname) == 'HeliumCharlo_SwitchDetails']
assert [n for n in lib.iter() if n.attrib.get(xname) == 'HeliumCharlo_SwitchGrid']
overlays = [n for n in lib.iter() if n.attrib.get(xname) == 'HeliumCharlo_Reopen']
assert len(overlays) == 1 and overlays[0].attrib['Visibility'] == 'Collapsed'
details = ET.parse(root / 'theme/Views/LibraryDetailsView.xaml')
assert [n for n in details.iter() if n.attrib.get(xname) == 'HeliumCharlo_NativeDetailsList']
source = (root / 'source/HeliumHybrid/Navigator.cs').read_text()
assert 'OpenNativeDetails(id)' in source and 'BackButton' in source
assert 'GetSettingsView' in (root / 'source/HeliumHybrid/Plugin.cs').read_text()
assert 'Settings.MigrateNavigation();' in (root / 'source/HeliumHybrid/Plugin.cs').read_text()
assert 'AssemblyVersion("0.2.3.0")' in (root / 'source/HeliumHybrid/Plugin.cs').read_text()
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
assert 'b59e3c9f-9c52-4582-a332-0716b56cf023' in (root / 'install/extension.yaml').read_text()
assert 'Name: Helium Navigator v0.2.3' in (root / 'install/extension.yaml').read_text()
assert 'Version: 0.2.3' in (root / 'install/extension.yaml').read_text()
assert 'e5d3a4ba-1cc7-42b1-a090-8d4d3a922b23' in (root / 'theme/theme.yaml').read_text()
assert 'Name: Helium (charlo fork) v0.2.3' in (root / 'theme/theme.yaml').read_text()
assert 'Version: 0.2.3' in (root / 'theme/theme.yaml').read_text()
assert 'e5d3a4ba-1cc7-42b1-a090-8d4d3a922b23' in (root / 'theme/themeExtras.yaml').read_text()
binary = (root / 'install/HeliumCharlo.dll').read_bytes()
assert binary[:2] == b'MZ'
pe = struct.unpack_from('<I', binary, 0x3c)[0]
assert binary[pe:pe+4] == b'PE\x00\x00'
assert struct.unpack_from('<H', binary, pe+24)[0] == 0x10b
assert b'HeliumHybrid.Navigator.xaml' in binary
for name, manifest in [('Helium-Navigator-v0.2.3.pext','extension.yaml'), ('Helium-charlo-fork-v0.2.3.pthm','theme.yaml')]:
    with zipfile.ZipFile(root / 'packages' / name) as z:
        assert z.testzip() is None
        assert manifest in z.namelist()
        assert not any('..' in Path(f).parts for f in z.namelist())
        if name.endswith('.pext'):
            assert set(z.namelist()) == {'HeliumCharlo.dll','extension.yaml'}
            assert z.read('HeliumCharlo.dll') == binary
print(f'PASS: {len(xaml)} XAML/XML; 2 packages; .NET DLL; IDs independent from 0.1.')
