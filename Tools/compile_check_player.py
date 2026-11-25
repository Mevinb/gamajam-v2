#!/usr/bin/env python3
"""Compile current runtime sources against a validated cached Linux-Mono player API. Never starts Unity."""
import argparse, json, os, re, shlex, subprocess, sys
from pathlib import Path

class InvalidCache(RuntimeError): pass

def require(ok, message):
    if not ok: raise InvalidCache(message)

def json_file(path):
    try: return json.loads(path.read_text())
    except (OSError, ValueError) as error: raise InvalidCache(f"Unreadable cache metadata {path}: {error}") from error

def version_at(project):
    path = project / "ProjectSettings/ProjectVersion.txt"
    require(path.is_file(), "Missing ProjectSettings/ProjectVersion.txt")
    match = re.search(r"^m_EditorVersion: (\S+)$", path.read_text(), re.M)
    require(match, "Project Unity version is missing")
    return match.group(1)

def parse_option(line, prefix):
    try: words = shlex.split(line)
    except ValueError as error: raise InvalidCache("Malformed player response-file option") from error
    require(len(words) == 1 and words[0].startswith(prefix), "Malformed player response-file option")
    return words[0]

def validate_cache(project, editor_data, rsp):
    prefix = rsp.parent.name.removesuffix(".dag")
    metadata = project / "Library/Bee" / (prefix + "-inputdata.json")
    data = json_file(metadata)
    config = data.get("BeeBuildProgramCommon.Data.ConfigurationData", {})
    compile_data = data.get("ScriptCompilationBuildProgram.Data.ScriptCompilationData", {})
    require(config.get("UnityVersion") == version_at(project), "Cached player Unity version differs from project")
    require(Path(config.get("EditorContentsPath", "")).resolve() == editor_data.resolve(), "Cached player editor installation differs")
    require(compile_data.get("BuildTarget") == "StandaloneLinux64", "Cached target is not Linux standalone")
    engine = editor_data / "PlaybackEngines/LinuxStandaloneSupport/Variations/mono/Managed"
    require((engine / "UnityEngine.CoreModule.dll").is_file(), "Installed Linux-Mono player modules are missing")
    refs, defines = [], []
    for line in rsp.read_text().splitlines():
        if line.startswith("-r:"): refs.append(parse_option(line, "-r:"))
        elif line.startswith("-define:"): defines.append(parse_option(line, "-define:"))
    symbols = {symbol for item in defines for symbol in re.split(r"[;,]", item[len("-define:"):])}
    require({"UNITY_STANDALONE_LINUX", "ENABLE_MONO"} <= symbols and "ENABLE_IL2CPP" not in symbols,
        "Cached compile is not the Linux Mono target")
    require(not any(s == "UNITY_EDITOR" or s.startswith("UNITY_EDITOR_") for s in symbols), "Editor defines in player cache")
    paths = []
    for item in refs:
        path = Path(item[3:]); path = path if path.is_absolute() else project / path
        require(path.is_file(), "Missing player reference: " + str(path))
        path = path.resolve(); paths.append(path)
        require(not path.name.startswith("UnityEditor") and not path.is_relative_to((editor_data / "Managed/UnityEngine").resolve()),
            "Editor reference in player cache: " + str(path))
    require((engine / "UnityEngine.CoreModule.dll").resolve() in paths, "Player engine reference is missing")
    require(any(p.name.endswith(".ref.dll") and p.is_relative_to((project / "Library/Bee/artifacts").resolve()) for p in paths),
        "Player package reference assemblies are missing")
    dependencies = {project / "ProjectSettings/ProjectVersion.txt", project / "Packages/manifest.json", project / "Packages/packages-lock.json"}
    for folder in [project / "Assets", project / "Packages"]:
        for suffix in ["*.asmdef", "*.asmref"]: dependencies.update(folder.rglob(suffix))
    package_roots = {row["Name"]: project / row["ResolvedPath"] for row in config.get("Packages", [])}
    for assembly in compile_data.get("Assemblies", []):
        asmdef = assembly.get("Asmdef")
        if asmdef:
            path = Path(asmdef)
            if not path.is_absolute():
                # Bee records PackageManager's virtual Packages/name path; resolve its frozen physical cache root.
                parts = path.parts
                if len(parts) > 2 and parts[0] == "Packages" and not (project / path).is_file() and parts[1] in package_roots:
                    path = package_roots[parts[1]].joinpath(*parts[2:])
                else: path = project / path
            dependencies.add(path)
    stamp = metadata.stat().st_mtime_ns
    for path in sorted(dependencies):
        require(path.is_file(), "Missing cache dependency: " + str(path))
        require(path.stat().st_mtime_ns <= stamp, "Player cache is stale after dependency change: " + str(path))
    return refs + defines, metadata

def choose_cache(project, editor_data):
    candidates = sorted((project / "Library/Bee/artifacts").glob("*P.dag/Parasite.Runtime.rsp"), key=lambda p: p.stat().st_mtime_ns, reverse=True)
    failures = []
    for rsp in candidates:
        try:
            options, metadata = validate_cache(project, editor_data, rsp)
            return rsp, options, metadata
        except (InvalidCache, OSError, KeyError) as error: failures.append(str(rsp.relative_to(project)) + ": " + str(error))
    detail = "; ".join(failures) if failures else "No Linux player response file exists"
    raise InvalidCache(detail + ". Run a successful Linux Mono Unity build to refresh the player API cache; no Editor fallback is used.")

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, required=True); parser.add_argument("--editor-data", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args(); project = args.project.resolve(); editor = args.editor_data.resolve()
    try:
        rsp, options, metadata = choose_cache(project, editor)
        # Current source enumeration is independent of the historical source list in Bee's response file.
        result = subprocess.run(["rg", "--files", "--hidden", "--no-ignore", str(project / "Assets/Parasite/Scripts"), "-g", "*.cs"], capture_output=True, text=True)
        require(result.returncode == 0 and result.stdout.strip(), "Current runtime sources could not be enumerated")
        sources = sorted(result.stdout.splitlines()); args.out.parent.mkdir(parents=True, exist_ok=True)
        command = [str(editor / "NetCoreRuntime/dotnet"), str(editor / "DotNetSdkRoslyn/csc.dll"),
            "-nologo", "-noconfig", "-nostdlib", "-target:library", "-langversion:9.0", "-nowarn:1701,1702,0414,0649,0169"]
        command += options + ["-out:" + str(args.out.resolve())] + sources
        print("PLAYER: references " + str(rsp.relative_to(project)), flush=True)
        completed = subprocess.run(command, cwd=project)
        ok = completed.returncode == 0 and args.out.is_file()
        print("PLAYER: " + (f"OK ({len(sources)} files; Linux Mono API)" if ok else "FAILED"))
        return 0 if ok else 1
    except (InvalidCache, OSError) as error:
        print("PLAYER: FAILED - " + str(error), file=sys.stderr); return 1

if __name__ == "__main__": sys.exit(main())
