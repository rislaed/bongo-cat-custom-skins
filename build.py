#!/usr/bin/env python3
import os
import sys
import re
import time
import shutil
import subprocess
import zipfile
from pathlib import Path

PROJECT_DIR = Path(__file__).parent.resolve()
SOLUTION_FILE = PROJECT_DIR / "BongoCatCustomSkins.sln"
OUTPUT_ZIP = PROJECT_DIR / "BongoCatCustomSkins.zip"
LOADER_DLL = PROJECT_DIR / "DoorstopLoader.dll"
CONFIG_INI = PROJECT_DIR / "doorstop_config.ini"
PLUGIN_DLL = PROJECT_DIR / "Mods" / "BongoCatCustomSkins.dll"
CECIL_DLL = PROJECT_DIR / "Mono.Cecil.dll"

def detect_game_directory(user_specified=None):
	if user_specified:
		p = Path(user_specified).expanduser().resolve()
		if p.exists() and (p / "BongoCat.exe").exists():
			return p
		elif p.exists():
			return p

	env_dir = os.environ.get("BONGO_CAT_DIR") or os.environ.get("GAME_DIR")
	if env_dir:
		p = Path(env_dir).expanduser().resolve()
		if p.exists():
			return p

	candidates = [
		Path("E:/SteamLibrary/steamapps/common/BongoCat"),
		Path("D:/SteamLibrary/steamapps/common/BongoCat"),
		Path("C:/SteamLibrary/steamapps/common/BongoCat"),
		Path(os.environ.get("ProgramFiles(x86)", "C:/Program Files (x86)")) / "Steam/steamapps/common/BongoCat",
		Path(os.environ.get("ProgramFiles", "C:/Program Files")) / "Steam/steamapps/common/BongoCat",
		Path.home() / ".steam/steam/steamapps/common/BongoCat",
		Path.home() / ".local/share/Steam/steamapps/common/BongoCat",
	]

	for c in candidates:
		if c.exists() and ((c / "BongoCat.exe").exists() or (c / "BongoCat_Data").exists()):
			return c.resolve()

	for c in candidates:
		if c.exists():
			return c.resolve()

	return None

def find_build_tool():
	tool = shutil.which("msbuild")
	if tool:
		return [tool]
	
	tool = shutil.which("dotnet")
	if tool:
		return [tool, "build"]

	if sys.platform == "win32":
		vswhere = Path(os.environ.get("ProgramFiles(x86)", "C:/Program Files (x86)")) / "Microsoft Visual Studio" / "Installer" / "vswhere.exe"
		if vswhere.exists():
			try:
				res = subprocess.check_output([
					str(vswhere), "-latest", "-requires", "Microsoft.Component.MSBuild",
					"-find", "MSBuild/**/Bin/MSBuild.exe"
				], text=True).strip().splitlines()
				if res and Path(res[0]).exists():
					return [res[0]]
			except Exception:
				pass
		
		candidates = [
			Path(os.environ.get("SystemRoot", "C:/Windows")) / "Microsoft.NET" / "Framework64" / "v4.0.30319" / "MSBuild.exe",
			Path(os.environ.get("SystemRoot", "C:/Windows")) / "Microsoft.NET" / "Framework" / "v4.0.30319" / "MSBuild.exe"
		]
		for cand in candidates:
			if cand.exists():
				return [str(cand)]
	elif sys.platform.startswith("linux") or sys.platform == "darwin":
		for name in ["msbuild", "xbuild"]:
			t = shutil.which(name)
			if t:
				return [t]

	return None

def stop_game_process():
	if sys.platform == "win32":
		try:
			subprocess.run(
				["powershell", "-NoProfile", "-Command", "Get-Process -Name 'BongoCat' -ErrorAction SilentlyContinue | Stop-Process -Force"],
				check=False, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL
			)
			time.sleep(0.5)
		except Exception:
			pass

def safe_copy(src, dst, retries=5, delay=0.5):
	for i in range(retries):
		try:
			shutil.copy2(src, dst)
			return
		except OSError:
			if i == retries - 1:
				raise
			time.sleep(delay)

def strip_attributes():
	if sys.platform == "win32":
		ps_cmd = f"""
		$bytes = [System.IO.File]::ReadAllBytes('{CECIL_DLL}');
		[System.Reflection.Assembly]::Load($bytes) | Out-Null;
		$rp = New-Object Mono.Cecil.ReaderParameters -Property @{{ ReadWrite = $true }};
		foreach ($p in @('{LOADER_DLL}', '{PLUGIN_DLL}')) {{
			$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($p, $rp);
			$asm.CustomAttributes.Clear();
			$asm.Write();
			$asm.Dispose();
		}}
		"""
		res = subprocess.run(["powershell", "-NoProfile", "-Command", ps_cmd])
		if res.returncode != 0:
			sys.exit("Failed to strip attributes via PowerShell.")
	else:
		pwsh = shutil.which("pwsh")
		if pwsh:
			ps_cmd = f"""
			$bytes = [System.IO.File]::ReadAllBytes('{CECIL_DLL}');
			[System.Reflection.Assembly]::Load($bytes) | Out-Null;
			$rp = New-Object Mono.Cecil.ReaderParameters -Property @{{ ReadWrite = $true }};
			foreach ($p in @('{LOADER_DLL}', '{PLUGIN_DLL}')) {{
				$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($p, $rp);
				$asm.CustomAttributes.Clear();
				$asm.Write();
				$asm.Dispose();
			}}
			"""
			subprocess.run([pwsh, "-NoProfile", "-Command", ps_cmd], check=True)
		else:
			print("Notice: pwsh not found. Skipping attribute stripping.")

def package_zip(include_skins=True):
	if OUTPUT_ZIP.exists():
		for _ in range(5):
			try:
				OUTPUT_ZIP.unlink()
				break
			except OSError:
				time.sleep(0.3)
		
	with zipfile.ZipFile(OUTPUT_ZIP, "w", zipfile.ZIP_DEFLATED) as zf:
		zf.write(LOADER_DLL, LOADER_DLL.name)
		zf.write(CONFIG_INI, CONFIG_INI.name)
		zf.write(PLUGIN_DLL, f"Mods/{PLUGIN_DLL.name}")
		if include_skins:
			zinfo = zipfile.ZipInfo("Mods/CustomSkins/")
			zinfo.external_attr = 0o755 << 16 | 0x10
			zf.writestr(zinfo, "")

			src_custom = PROJECT_DIR / "Mods" / "CustomSkins"
			if src_custom.exists():
				for file_path in src_custom.rglob("*"):
					if file_path.is_file():
						rel_path = file_path.relative_to(PROJECT_DIR)
						zf.write(file_path, rel_path.as_posix())
				
	print(f"Package created: {OUTPUT_ZIP.name} ({OUTPUT_ZIP.stat().st_size} bytes)")

def deploy(game_dir, include_skins=True):
	game_path = Path(game_dir).resolve()
	if not game_path.exists():
		sys.exit(f"Target game directory does not exist: {game_path}")
	
	print("Stopping running Bongo Cat instances before deployment...")
	stop_game_process()

	print(f"Deploying to {game_path}...")
	safe_copy(LOADER_DLL, game_path / LOADER_DLL.name)
	
	target_config = game_path / CONFIG_INI.name
	if target_config.exists():
		content = target_config.read_text(encoding="utf-8")
		if re.search(r"target_assembly\s*=", content, re.IGNORECASE):
			content = re.sub(r"target_assembly\s*=.*", "target_assembly = DoorstopLoader.dll", content, flags=re.IGNORECASE)
		elif re.search(r"targetAssembly\s*=", content, re.IGNORECASE):
			content = re.sub(r"targetAssembly\s*=.*", "targetAssembly=DoorstopLoader.dll", content, flags=re.IGNORECASE)
		else:
			content = CONFIG_INI.read_text(encoding="utf-8")
		target_config.write_text(content, encoding="utf-8")
	else:
		safe_copy(CONFIG_INI, target_config)
	
	target_mods = game_path / "Mods"
	target_mods.mkdir(parents=True, exist_ok=True)
	safe_copy(PLUGIN_DLL, target_mods / PLUGIN_DLL.name)

	if include_skins:
		target_custom = target_mods / "CustomSkins"
		target_custom.mkdir(parents=True, exist_ok=True)
		src_custom = PROJECT_DIR / "Mods" / "CustomSkins"
		if src_custom.exists():
			shutil.copytree(src_custom, target_custom, dirs_exist_ok=True)
			
	print("Deployment finished successfully.")

def main():
	import argparse
	parser = argparse.ArgumentParser(description="Universal Build and Deploy Tool for Bongo Cat Custom Skins")
	parser.add_argument("--game-dir", "-g", help="Path to Bongo Cat installation folder")
	parser.add_argument("--no-deploy", action="store_true", help="Compile and package zip only, do not deploy to game directory")
	parser.add_argument("--skins", dest="include_skins", action="store_true", default=True, help="Include CustomSkins directory in archive and deployment (default: True)")
	parser.add_argument("--no-skins", dest="include_skins", action="store_false", help="Do not include CustomSkins directory in archive or deployment")
	args = parser.parse_args()

	game_dir = detect_game_directory(args.game_dir)
	if not game_dir:
		sys.exit("Error: Could not locate Bongo Cat installation folder. Specify it with --game-dir <path>.")

	print(f"Resolved Bongo Cat directory: {game_dir}")

	tool = find_build_tool()
	if not tool:
		sys.exit("Error: No build tool (msbuild or dotnet) found in PATH or standard locations.")
	
	build_props = [
		f"/p:GameDir={game_dir}",
		"/p:Configuration=Release",
		"/v:minimal",
		"/nologo"
	]
	cmd = tool + [str(SOLUTION_FILE)] + build_props
	print(f"Executing build: {' '.join(cmd)}")
	res = subprocess.run(cmd, cwd=str(PROJECT_DIR))
	if res.returncode != 0:
		sys.exit(f"Build failed with exit code {res.returncode}")
	
	print("Stripping assembly attributes via Mono.Cecil...")
	strip_attributes()

	print("Packaging release zip archive...")
	package_zip(include_skins=args.include_skins)

	if not args.no_deploy:
		deploy(game_dir, include_skins=args.include_skins)

if __name__ == "__main__":
	main()
