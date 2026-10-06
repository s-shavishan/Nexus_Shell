"""Static source checks only. This does not replace a Windows build/run."""
from pathlib import Path
import json
import re
import struct
import xml.etree.ElementTree as ET
import argparse

parser = argparse.ArgumentParser()
parser.add_argument("--syntax", action="store_true", help="Also parse C# syntax with optional tree-sitter packages (no type checking)")
options = parser.parse_args()

root = Path(__file__).resolve().parents[1]
project = root / "src/Nexus.Shell"
required = [
    "App.xaml", "App.xaml.cs", "MainWindow.xaml", "MainWindow.xaml.cs",
    "Nexus.Shell.csproj", "app.manifest", "Assets/Nexus.ico",
    "Interop/NativeMethods.cs", "Models/ShellState.cs", "Services/AppCatalog.cs",
    "Services/Log.cs", "Services/StartupRegistration.cs", "Services/StateStore.cs",
    "Services/UsageTracker.cs",
    "Services/ResourceSampler.cs", "UI/MotionController.cs", "UI/AppAccentConverter.cs",
]
for relative in required:
    assert (project / relative).is_file(), f"Missing file: {relative}"
for path in [*project.glob("*.xaml"), project / "Nexus.Shell.csproj", project / "app.manifest", root / "NuGet.Config"]:
    ET.parse(path)
    print("XML OK:", path.relative_to(root))

xns = "{http://schemas.microsoft.com/winfx/2006/xaml}"
window = ET.parse(project / "MainWindow.xaml").getroot()
code = (project / "MainWindow.xaml.cs").read_text(encoding="utf-8")
names = [node.attrib[xns + "Name"] for node in window.iter() if xns + "Name" in node.attrib]
assert len(names) == len(set(names)), "Duplicate XAML names"
handlers = set()
for node in window.iter():
    for key, value in node.attrib.items():
        if key in {"Click", "Toggled", "SizeChanged", "PointerPressed", "Loaded", "Opening", "Opened", "Closed", "ItemClick", "TextChanged", "ContainerContentChanging"}:
            assert re.search(r"\b" + re.escape(value) + r"\s*\(", code), f"Missing handler: {value}"
            handlers.add(value)
assert "AutomationProperties =" not in code, "Attached properties must use their setters"

application = ET.parse(project / "App.xaml").getroot()
app_resources = {node.attrib[xns + "Key"] for node in application.iter() if xns + "Key" in node.attrib}
local_resources = {node.attrib[xns + "Key"] for node in window.iter() if xns + "Key" in node.attrib}
for node in window.iter():
    for value in node.attrib.values():
        for match in re.finditer(r"\{StaticResource\s+([^}\s,]+)\}", value):
            assert match[1] in app_resources | local_resources, f"Missing resource: {match[1]}"

# Validate custom theme references, including styles, converters, and flyouts
# before they are attached to the themed root. XML well-formedness alone cannot
# detect a missing resource key that causes Application.LoadComponent to fail.
used_theme_keys = {
    match[1] for node in [*application.iter(), *window.iter()]
    for value in node.attrib.values()
    for match in re.finditer(r"\{ThemeResource\s+(Nexus[^}\s,]+)\}", value)
}
pns = "{http://schemas.microsoft.com/winfx/2006/xaml/presentation}"
theme_node = next(application.iter(pns + "ResourceDictionary.ThemeDictionaries"))
for theme in ["Light", "Dark", "HighContrast"]:
    dictionary = next(node for node in theme_node if node.attrib.get(xns + "Key") == theme)
    keys = {node.attrib[xns + "Key"] for node in dictionary if xns + "Key" in node.attrib}
    assert used_theme_keys <= keys, f"Missing {theme} resources: {used_theme_keys - keys}"
main_dictionary = application.find(pns + "Application.Resources/" + pns + "ResourceDictionary")
fallback_keys = {node.attrib[xns + "Key"] for node in main_dictionary if xns + "Key" in node.attrib}
assert used_theme_keys <= fallback_keys, f"Missing fallback resources: {used_theme_keys - fallback_keys}"

# Guard the library's finite viewport; an outer ScrollViewer/StackPanel would
# reintroduce full realization of all discovered application tiles.
parents = {child: parent for parent in window.iter() for child in parent}
apps_grid = next(node for node in window.iter() if node.attrib.get(xns + "Name") == "AppsGrid")
ancestor = parents[apps_grid]
while ancestor is not window:
    assert ancestor.tag.rsplit("}", 1)[-1] not in {"ScrollViewer", "StackPanel"}, "Unbounded app-library viewport"
    ancestor = parents[ancestor]
assert "AppsGrid.ItemsSource =" in code and "grid.Items.Add(AppButton" not in code
motion = (project / "UI/MotionController.cs").read_text(encoding="utf-8")
assert "IterationBehavior.Forever" not in motion and "CompositionTarget.Rendering" not in motion

json.loads((root / "global.json").read_text())
reserved, icon_type, count = struct.unpack("<HHH", (project / "Assets/Nexus.ico").read_bytes()[:6])
assert reserved == 0 and icon_type == 1 and count >= 5, "Invalid application icon"
for relative in ["build.ps1", "package.ps1", "diagnostics.ps1", "disable-startup.ps1", "measure-resources.ps1"]:
    assert (root / "scripts" / relative).is_file()
for relative in ["TEST-WINDOWS.md", "RUN-PORTABLE.md", "VALIDATION.md", "DESIGN-AND-PERFORMANCE.md", "CHANGELOG.md"]:
    assert (root / "docs" / relative).is_file()
workflow = (root / ".github/workflows/build-windows.yml").read_text()
assert "./scripts/build.ps1 -UseMSBuild" in workflow
assert "./scripts/package.ps1" in workflow
assert "contents: read" in workflow
assert "windows-2022" in workflow
assert "src\\Nexus.Shell\\Nexus.Shell.csproj" in (root / "Nexus.Shell.sln").read_text()
if options.syntax:
    from tree_sitter import Language, Parser
    import tree_sitter_c_sharp
    syntax_parser = Parser(Language(tree_sitter_c_sharp.language()))
    for source in sorted(project.rglob("*.cs")):
        tree = syntax_parser.parse(source.read_bytes())
        assert not tree.root_node.has_error, f"C# syntax error: {source}"
    print("C# syntax OK (tree-sitter; API/type resolution NOT checked)")
print(f"PASS: {len(required)} project files, {len(names)} named elements, {len(handlers)} handlers, resources, icon, JSON, and build references.")
print("C# / XAML compilation and Windows launch still required.")
