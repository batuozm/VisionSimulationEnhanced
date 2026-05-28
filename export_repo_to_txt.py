from pathlib import Path

# Name der Export-Datei
OUTPUT_FILE = "repo_export.txt"

# Ordner, die NICHT exportiert werden sollen
EXCLUDED_DIRS = {
    ".git",
    ".vs",
    ".vscode",
    "Library",
    "Temp",
    "Obj",
    "Build",
    "Builds",
    "Logs",
    "UserSettings",
    "MemoryCaptures",
}

# Dateien, die NICHT exportiert werden sollen
EXCLUDED_FILES = {
    OUTPUT_FILE,
    "export_repo_to_txt.py",
}

# Dateiendungen, die NICHT exportiert werden sollen
EXCLUDED_EXTENSIONS = {
    ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tga", ".psd",
    ".fbx", ".blend", ".obj", ".dae",
    ".mp4", ".mov", ".avi", ".wav", ".mp3", ".ogg",
    ".zip", ".rar", ".7z",
    ".dll", ".exe", ".apk",
    ".csproj", ".sln",
    ".unitypackage",
}

# Dateiendungen, die exportiert werden sollen
INCLUDED_EXTENSIONS = {
    ".cs",
    ".shader",
    ".hlsl",
    ".cginc",
    ".json",
    ".txt",
    ".md",
    ".xml",
    ".asmdef",
    ".asset",
    ".prefab",
    ".unity",
    ".mat",
    ".controller",
    ".inputactions",
}

# Maximale Dateigröße pro Datei, damit nichts Riesiges exportiert wird
MAX_FILE_SIZE_MB = 2


def should_skip_file(file_path: Path) -> bool:
    if file_path.name in EXCLUDED_FILES:
        return True

    if file_path.suffix.lower() in EXCLUDED_EXTENSIONS:
        return True

    if file_path.suffix.lower() not in INCLUDED_EXTENSIONS:
        return True

    file_size_mb = file_path.stat().st_size / (1024 * 1024)
    if file_size_mb > MAX_FILE_SIZE_MB:
        return True

    return False


def should_skip_dir(dir_path: Path) -> bool:
    return dir_path.name in EXCLUDED_DIRS


def export_repo():
    root = Path.cwd()
    output_path = root / OUTPUT_FILE

    files_to_export = []

    for path in root.rglob("*"):
        if path.is_dir():
            continue

        # Prüfen, ob ein übergeordneter Ordner ausgeschlossen ist
        if any(part in EXCLUDED_DIRS for part in path.parts):
            continue

        if should_skip_file(path):
            continue

        files_to_export.append(path)

    files_to_export.sort()

    with output_path.open("w", encoding="utf-8") as output:
        output.write("REPOSITORY EXPORT\n")
        output.write("=================\n\n")
        output.write(f"Root: {root}\n")
        output.write(f"Exported files: {len(files_to_export)}\n\n")

        for file_path in files_to_export:
            relative_path = file_path.relative_to(root)

            output.write("\n")
            output.write("=" * 100 + "\n")
            output.write(f"FILE: {relative_path}\n")
            output.write("=" * 100 + "\n\n")

            try:
                content = file_path.read_text(encoding="utf-8")
            except UnicodeDecodeError:
                try:
                    content = file_path.read_text(encoding="latin-1")
                except Exception as e:
                    output.write(f"[Could not read file: {e}]\n")
                    continue
            except Exception as e:
                output.write(f"[Could not read file: {e}]\n")
                continue

            output.write(content)
            output.write("\n")

    print(f"Export fertig: {output_path}")
    print(f"Exportierte Dateien: {len(files_to_export)}")


if __name__ == "__main__":
    export_repo()