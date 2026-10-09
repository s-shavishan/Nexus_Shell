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
    "Desktop/DesktopEnvironment.cs", "Desktop/DesktopWindow.cs", "Desktop/TaskbarWindow.cs", "Desktop/MenuWindow.cs",
    "UI/Desktop/DesktopSurface.cs", "UI/Taskbar/TaskbarView.cs", "UI/Menus/DesktopMenus.cs", "UI/Menus/StartMenuView.cs", "UI/ShellTheme.cs",
    "Services/ShellSession.cs", "Services/DesktopLayout.cs", "Services/DesktopCatalog.cs", "Interop/ShellLayerInterop.cs", "Interop/TaskbarRegistration.cs",
    "MainWindow.Polish.cs", "MainWindow.PcControls.cs", "Services/AudioController.cs", "Services/PcMetrics.cs", "Services/WindowLayouts.cs",
    "App.xaml", "App.xaml.cs", "MainWindow.xaml", "MainWindow.xaml.cs", "MainWindow.Workspaces.cs", "MainWindow.Orbit.cs", "MainWindow.Desktop.cs", "MainWindow.Appearance.cs", "MainWindow.Experience.cs", "MainWindow.Explore.cs", "Services/ExploreWorkspace.cs", "UI/SavedKindConverter.cs", "UI/NexusIcons.cs", "Services/NavigationTrail.cs", "Services/ShellExperience.cs", "Services/AuraPalette.cs", "Services/DesktopWorkspace.cs", "Interop/DesktopIntegration.cs",
    "Services/FocusSession.cs", "Services/CommandSearch.cs", "Services/WorkspaceState.cs",
    "Nexus.Shell.csproj", "app.manifest", "Assets/Nexus.ico",
    "Interop/NativeMethods.cs", "Models/ShellState.cs", "Services/AppCatalog.cs",
    "Services/Log.cs", "Services/StartupRegistration.cs", "Services/StateStore.cs",
    "Services/UsageTracker.cs",
    "Services/ResourceSampler.cs", "UI/MotionController.cs", "UI/AppAccentConverter.cs",
    "Desktop/FilesWindow.cs", "Desktop/SwitcherWindow.cs", "UI/Files/FilesView.cs", "MainWindow.DesktopMode.cs",
    "Services/FileCatalog.cs", "Services/DesktopShellPolicy.cs", "Services/DesktopShellRegistration.cs", "Services/DesktopSessionRecovery.cs", "Services/DesktopSessionRecord.cs",
    "Interop/ExclusiveTaskbarRegistration.cs", "Interop/ShellKeyboardHook.cs", "Interop/NexusDesktopToggle.cs", "Interop/ShortcutResolver.cs", "Interop/RecycleBinService.cs",
    "Interop/DesktopWorkArea.cs", "Services/DesktopWorkAreaReservation.cs", "Services/DesktopVisuals.cs",
    "Services/BrightnessController.cs", "Services/BrightnessScale.cs", "Desktop/QuickSettingsWindow.cs", "UI/Controls/QuickSettingsView.cs",
    "Interop/WindowChrome.cs", "UI/SurfaceMotion.cs",
]
for relative in required:
    assert (project / relative).is_file(), f"Missing file: {relative}"
for path in [*project.glob("*.xaml"), project / "Nexus.Shell.csproj", project / "app.manifest", root / "NuGet.Config"]:
    ET.parse(path)
    print("XML OK:", path.relative_to(root))

# XAML's implicit content collection must form one contiguous block. XML alone
# accepts content -> property element -> more content, but WinUI reports this
# as WMC0035 (ResourceDictionary's implicit collection is named _Items).
for path in project.glob("*.xaml"):
    for node in ET.parse(path).getroot().iter():
        seen_content = False
        content_ended = False
        for child in node:
            local_name = child.tag.rsplit("}", 1)[-1]
            if "." in local_name:
                if seen_content:
                    content_ended = True
            else:
                assert not content_ended, (
                    f"Non-contiguous XAML content (WMC0035): {path.relative_to(root)}; "
                    f"{node.tag.rsplit('}', 1)[-1]} resumes at {local_name}. "
                    "Keep collection items together before or after property elements."
                )
                seen_content = True
print("XAML content ordering OK")

for path in project.glob("*.xaml"):
    for node in ET.parse(path).getroot().iter():
        tag = node.tag.rsplit("}", 1)[-1]
        if tag in {"Border", "Window", "ScrollViewer", "Viewbox", "Flyout", "Button", "ContentControl"}:
            children = [c for c in node if "." not in c.tag.rsplit("}", 1)[-1]]
            assert len(children) <= 1, f"Multiple children in {tag}: {path}"

xns = "{http://schemas.microsoft.com/winfx/2006/xaml}"
window = ET.parse(project / "MainWindow.xaml").getroot()
code = "\n".join(p.read_text(encoding="utf-8") for p in project.glob("MainWindow*.cs"))
names = [node.attrib[xns + "Name"] for node in window.iter() if xns + "Name" in node.attrib]
assert len(names) == len(set(names)), "Duplicate XAML names"
handlers = set()
for node in window.iter():
    for key, value in node.attrib.items():
        if key in {"Click", "DragOver", "Drop", "Toggled", "Checked", "Unchecked", "KeyDown", "SizeChanged", "PointerPressed", "Loaded", "Opening", "Opened", "Closed", "ItemClick", "TextChanged", "ContainerContentChanging", "SelectionChanged", "PreviewKeyDown", "ValueChanged"}:
            assert re.search(r"\b" + re.escape(value) + r"\s*\(", code), f"Missing handler: {value}"
            handlers.add(value)
assert "AutomationProperties =" not in code, "Attached properties must use their setters"

# These UWP-only events can compile yet terminate desktop startup. Check the
# entire app source so the regression cannot move to another partial/helper.
for source in project.rglob("*.cs"):
    source_text = source.read_text(encoding="utf-8")
    assert not re.search(r"\b(?:HighContrastChanged|ColorValuesChanged)\s*[+-]=", source_text), (
        f"Unsupported desktop WinRT event subscription: {source.relative_to(root)}"
    )
print("Desktop WinRT event compatibility OK")

# WinUI Border is sealed in its C# projection (CS0509). A custom surface must
# compose a Border inside a supported layout/control base. This catches that
# specific API mistake; native C# compilation is still required.
for source in project.rglob("*.cs"):
    assert not re.search(
        r"\bclass\s+\w+\s*:\s*(?:global::)?(?:Microsoft\.UI\.Xaml\.Controls\.)?Border\b",
        source.read_text(encoding="utf-8"),
    ), f"Sealed WinUI Border inheritance (CS0509): {source.relative_to(root)}. Compose the Border instead."
print("WinUI sealed Border inheritance OK")

# WinUI Thickness has uniform and four-side constructors. The two-value WPF
# overload is not available and caused CS7036 in the 0.8.0 Windows build.
for source in project.rglob("*.cs"):
    for match in re.finditer(r"\bnew\s+Thickness\s*\(([^()\n]*)\)", source.read_text(encoding="utf-8")):
        assert len(match[1].split(",")) in {1, 4}, f"Unsupported WinUI Thickness overload: {source.relative_to(root)}: {match[0]}"
print("WinUI Thickness constructors OK")

application = ET.parse(project / "App.xaml").getroot()
app_resources = {node.attrib[xns + "Key"] for node in application.iter() if xns + "Key" in node.attrib}
local_resources = {node.attrib[xns + "Key"] for node in window.iter() if xns + "Key" in node.attrib}
# C#-composed windows need the same resource-key validation as XAML.
for source in project.rglob("*.cs"):
    for match in re.finditer(r'\.Brush\("(Nexus[^"\s]+)"\)', source.read_text()):
        assert match[1] in app_resources, f"Missing C# UI brush: {source.relative_to(root)}: {match[1]}"
print("C# UI brush resources OK")

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
for grid_name in ["AppsGrid", "WindowsGrid", "ExploreGrid"]:
    grid = next(node for node in window.iter() if node.attrib.get(xns + "Name") == grid_name)
    ancestor = parents[grid]
    while ancestor is not window:
        assert ancestor.tag.rsplit("}", 1)[-1] not in {"ScrollViewer", "StackPanel"}, f"Unbounded {grid_name} viewport"
        ancestor = parents[ancestor]
page_host = next(node for node in window.iter() if node.attrib.get(xns + "Name") == "PageHost")
overview = next(node for node in window.iter() if node.attrib.get(xns + "Name") == "WindowOverviewView")
assert parents[overview] is page_host, "Window overview must be inside the page host"
assert "AppsGrid.ItemsSource =" in code and "grid.Items.Add(AppButton" not in code
motion = (project / "UI/MotionController.cs").read_text(encoding="utf-8")
assert "IterationBehavior.Forever" not in motion and "CompositionTarget.Rendering" not in motion

json.loads((root / "global.json").read_text())
reserved, icon_type, count = struct.unpack("<HHH", (project / "Assets/Nexus.ico").read_bytes()[:6])
assert reserved == 0 and icon_type == 1 and count >= 5, "Invalid application icon"
for relative in ["build.ps1", "package.ps1", "verify-resources.ps1", "package-update.ps1", "apply-update.ps1", "test-update.ps1", "diagnostics.ps1", "disable-startup.ps1", "measure-resources.ps1"]:
    assert (root / "scripts" / relative).is_file()
for relative in ["TEST-WINDOWS.md", "RUN-PORTABLE.md", "VALIDATION.md", "DESIGN-AND-PERFORMANCE.md", "CHANGELOG.md"]:
    assert (root / "docs" / relative).is_file()
assert (root / "docs/UPDATING.md").is_file()
assert (root / "tests/Nexus.Core.Checks/Nexus.Core.Checks.csproj").is_file()
assert (root / "tests/Nexus.Core.Checks/Program.cs").is_file()
assert (root / "tests/Nexus.Core.Checks/DesktopNativeChecks.cs").is_file()
workflow = (root / ".github/workflows/build-windows.yml").read_text()
assert "./scripts/build.ps1 -UseMSBuild" in workflow
assert "./scripts/package.ps1" in workflow
assert "contents: read" in workflow
assert "windows-2022" in workflow
assert "src\\Nexus.Shell\\Nexus.Shell.csproj" in (root / "Nexus.Shell.sln").read_text()
# The project's own resource index must be carried into the unpackaged publish;
# the native framework DLL/PRIs alone cannot resolve ms-appx:///MainWindow.xaml.
project_xml = ET.parse(project / "Nexus.Shell.csproj").getroot()
assert project_xml.findtext("PropertyGroup/EnableMsixTooling") == "true"
assert project_xml.findtext("PropertyGroup/WindowsPackageType") == "None"
icon_content = project_xml.find("ItemGroup/Content[@Include='Assets\\Icons\\*.svg']")
assert icon_content is not None and icon_content.attrib.get("CopyToPublishDirectory") == "PreserveNewest", "Native vector icons must be published"
icon_names = {p.stem for p in (project / "Assets/Icons").glob("*.svg")}
assert {"Nexus", "Explore", "Study", "Files", "Apps", "Browser", "Search", "Settings", "Windows", "Note", "Document", "Terminal", "Game"} <= icon_names
for path in (project / "Assets/Icons").glob("*.svg"):
    for node in ET.parse(path).getroot().iter():
        assert node.tag.rsplit("}", 1)[-1] not in {"script", "image", "filter", "animate", "text"}, f"Unsupported/external vector content: {path}"
for source in project.glob("*.xaml"):
    for node in ET.parse(source).getroot().iter():
        uri = node.attrib.get("UriSource", "")
        if uri.startswith("ms-appx:///Assets/Icons/"):
            assert uri.rsplit("/", 1)[-1].removesuffix(".svg") in icon_names, f"Missing icon: {uri}"
print("Native vector assets and publish wiring OK")
wallpaper_content = project_xml.find("ItemGroup/Content[@Include='Assets\\Wallpapers\\*.png']")
assert wallpaper_content is not None and wallpaper_content.attrib.get("CopyToPublishDirectory") == "PreserveNewest"
for mood in ["Solstice", "Ember", "Opal", "Lagoon", "Graphite", "Pearl"]:
    png = (project / "Assets/Wallpapers" / (mood + ".png")).read_bytes()
    assert png[:8] == b"\x89PNG\r\n\x1a\n" and struct.unpack(">II", png[16:24]) == (2560, 1600), f"Invalid wallpaper cache: {mood}"
desktop = (project / "UI/Desktop/DesktopSurface.cs").read_text()
assert "BitmapImage" in desktop and "Shapes.Path" not in desktop, "Desktop ribbons must use the cached asset"
print("Cached wallpaper assets and publish wiring OK")
# Desktop composition must not regress into the Sections XAML root.
assert not (project / "MainWindow.Canvas.cs").exists(), "Obsolete packed desktop partial remains"
assert not {"MenuBar", "DockBorder", "DesktopCanvas", "DesktopClockCard", "DesktopWorkspaceCard", "WallpaperAccents"} & set(names), "Desktop UI must not be embedded in Sections"
startup = (project / "App.xaml.cs").read_text()
assert "new MainWindow(" not in startup and "DesktopEnvironment" in startup, "Startup must create the environment without Sections"
for layer in ["DesktopWindow", "TaskbarWindow", "MenuWindow", "QuickSettingsWindow"]:
    body = (project / "Desktop" / (layer + ".cs")).read_text()
    assert re.search(r"class\s+" + layer + r"\s*:\s*Window", body), f"{layer} must own a native Window"
closed = code[code.index("private void Window_Closed("):]
assert "SaveFinal(" not in closed and "DetachSnapshot(" in closed, "Closing Sections must not finalize the desktop session"
for file in [root / "appveyor.yml", root / ".github/workflows/build-windows.yml", root / "scripts/package.ps1", root / "scripts/build.ps1"]:
    body = file.read_text()
    assert "Nexus-Shell-1.4.2" in body and "Nexus-Shell-1.1.0" not in body and "Nexus-Shell-1.0.0" not in body, f"Stale artifact name: {file}"
print("Independent desktop ownership, Sections lifetime and CI versions OK")
# Desktop replacement cannot silently instantiate Explorer or common picker UI.
environment = (project / "Desktop/DesktopEnvironment.cs").read_text()
assert "ExplorerDesktopPresent()" in environment and "DesktopSessionMode.DesktopShell" in environment
for source in project.rglob("*.cs"):
    body = source.read_text()
    assert "Shell.Application" not in body, f"Explorer automation remains: {source}"
    assert not re.search(r"\bnew\s+(?:FileOpenPicker|FileSavePicker|FolderPicker)\b", body), f"Windows picker UI remains: {source}"
assert 'new("files", "Files", "nexus:files"' in (project / "Services/AppCatalog.cs").read_text()
assert "new ExclusiveTaskbarRegistration" in (project / "Desktop/TaskbarWindow.cs").read_text()
for layer in ["FilesWindow", "SwitcherWindow"]:
    assert re.search(r"class\s+" + layer + r"\s*:\s*Window", (project / "Desktop" / (layer + ".cs")).read_text())
for relative in ["src/Nexus.DesktopHost/Nexus.DesktopHost.csproj", "src/Nexus.DesktopHost/Program.cs", "src/Nexus.DesktopHost/WindowsDesktopSurfaces.cs", "docs/NEXUS-DESKTOP-MODE.md", "docs/TEST-DESKTOP-MODE.md", "scripts/restore-windows-desktop.ps1"]:
    assert (root / relative).is_file(), f"Missing desktop-mode deliverable: {relative}"
ET.parse(root / "src/Nexus.DesktopHost/Nexus.DesktopHost.csproj")
host = (root / "src/Nexus.DesktopHost/Program.cs").read_text()
assert "--host-token" in host and "EventWaitHandle" in host and "entireProcessTree: false" in host
assert "budget.IsUnresponsive" in host and "DesktopExitCode.RestoreWindows" in host
build = (root / "scripts/build.ps1").read_text()
assert "Nexus.DesktopHost.csproj" in build and "Restore-Windows-Desktop.bat" in build and "host runtime differs" in build
for relative in ["scripts/package-update.ps1", "scripts/apply-update.ps1"]:
    assert "Nexus.DesktopHost.*" in (root / relative).read_text(), f"Host must remain app-owned payload: {relative}"
assert "FolderBrowserDialog" not in (root / "scripts/apply-update.ps1").read_text()
print("Nexus desktop mode, Files/pickers, switcher, host recovery and build/update ownership OK")
resource_target = project_xml.find("Target[@Name='NexusPublishXamlResources']")
assert resource_target is not None and resource_target.attrib.get("AfterTargets") == "Publish"
resource_items = resource_target.find("ItemGroup/_NexusBuildResources").attrib["Include"]
assert "*.pri" in resource_items and "*.xbf" in resource_items
copy = resource_target.find("Copy[@SourceFiles='@(_NexusBuildResources)']")
assert "%(RecursiveDir)" in copy.attrib["DestinationFiles"]
for source in project.rglob("*.cs"):
    body = source.read_text()
    if "NativeMethods." in body and "namespace Nexus.Shell.Interop;" not in body:
        assert "using Nexus.Shell.Interop;" in body or "Interop.NativeMethods." in body, f"Missing interop import: {source}"
print("Interop imports OK")
if options.syntax:
    from tree_sitter import Language, Parser
    import tree_sitter_c_sharp
    syntax_parser = Parser(Language(tree_sitter_c_sharp.language()))
    for source in sorted([*project.rglob("*.cs"), *(root / "src/Nexus.DesktopHost").rglob("*.cs"), *(root / "tests").rglob("*.cs")]):
        tree = syntax_parser.parse(source.read_bytes())
        assert not tree.root_node.has_error, f"C# syntax error: {source}"
    print("C# syntax OK (tree-sitter; API/type resolution NOT checked)")
print(f"PASS: {len(required)} project files, {len(names)} named elements, {len(handlers)} handlers, resources, icon, JSON, and build references.")
print("C# / XAML compilation and Windows launch still required.")
