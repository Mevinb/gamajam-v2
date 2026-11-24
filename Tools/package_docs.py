#!/usr/bin/env python3
"""Prepare portable release documents; rewrite copies only and verify every packaged local Markdown link."""
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
from urllib.parse import quote, unquote, urlsplit, urlunsplit

GUIDES = {
    'Docs/RELEASE_README.md': 'README.md',
    **{f'Docs/{name}.md': f'{name}.md' for name in (
        'CONTROLS', 'KNOWN_ISSUES', 'THIRD_PARTY_ASSETS', 'TEST_REPORT',
        'AFTERLAKE_GUIDE', 'COMPLETE_GAME_GUIDE', 'HANDOFF', 'UPGRADE_TRACKER')},
}
CREDITS = {
    'Assets/Parasite/Resources/UI/OFL-Rajdhani.txt': 'Licenses/OFL-Rajdhani.txt',
    'Assets/Parasite/Audio/KENNEY_LICENSE.txt': 'Licenses/KENNEY_LICENSE.txt',
    'Assets/Parasite/Audio/AUDIO_CREDITS.txt': 'Licenses/AUDIO_CREDITS.txt',
    'Assets/Parasite/Art/Characters/CREDITS.md': 'Licenses/CHARACTER_CREDITS.md',
    'Assets/Parasite/Art/Characters/Animations/CREDITS.md': 'Licenses/ANIMATION_CREDITS.md',
}
LINK = re.compile(r'(!?\[[^\]\n]*\]\()(<[^>\n]+>|[^\s)]+)([^)]*\))')


def repository_reference(project):
    def git(*arguments):
        return subprocess.check_output(['git', '-C', str(project), *arguments], text=True).strip()
    remote = git('remote', 'get-url', 'origin')
    if remote.startswith('git@github.com:'):
        remote = 'https://github.com/' + remote.removeprefix('git@github.com:')
    elif remote.startswith('ssh://git@github.com/'):
        remote = 'https://github.com/' + remote.removeprefix('ssh://git@github.com/')
    if remote.endswith('.git'):
        remote = remote[:-4]
    if not remote.startswith(('https://', 'http://')):
        raise ValueError('Cannot create portable source links from repository URL: ' + remote)
    return remote.rstrip('/'), git('rev-parse', 'HEAD')


def prepare(project, build):
    project, build = project.resolve(), build.resolve()
    mapping = {project / source: Path(destination) for source, destination in (GUIDES | CREDITS).items()}
    for source in mapping:
        if not source.is_file() or source.stat().st_size == 0:
            raise ValueError('Missing or empty package document: ' + str(source))
    repository, revision = repository_reference(project)
    screenshots = project / 'Docs/Screenshots'
    if screenshots.is_dir():
        shutil.copytree(screenshots, build / 'Screenshots', dirs_exist_ok=True)

    def copied_document(source, destination):
        def rewrite(match):
            raw = match[2]
            target = raw[1:-1] if raw.startswith('<') and raw.endswith('>') else raw
            url = urlsplit(target)
            if url.scheme or url.netloc or not url.path:
                return match[0]
            source_target = Path(os.path.abspath(source.parent / unquote(url.path)))
            relative = source_target.relative_to(project)
            if not source_target.exists():
                raise ValueError(f'Broken source link in {source.relative_to(project)}: {target}')
            if source_target in mapping:
                packaged = mapping[source_target]
            elif relative.parts[:2] == ('Docs', 'Screenshots'):
                packaged = Path('Screenshots') / relative.relative_to('Docs/Screenshots')
            else:
                # Developer source files stay in the repository, rather than becoming dead ../Assets paths.
                kind = 'tree' if source_target.is_dir() else 'blob'
                updated = f'{repository}/{kind}/{revision}/{quote(relative.as_posix(), safe="/")}'
                updated = urlunsplit(('', '', updated, url.query, url.fragment))
                return match[1] + updated + match[3]
            local = Path(os.path.relpath(packaged, destination.parent)).as_posix()
            updated = urlunsplit(('', '', quote(local, safe='/.'), url.query, url.fragment))
            return match[1] + updated + match[3]

        content = LINK.sub(rewrite, source.read_text())
        if destination == Path('THIRD_PARTY_ASSETS.md'):
            content = content.replace(
                'Licence text: `Assets/Parasite/Audio/KENNEY_LICENSE.txt`.',
                'Licence text: [Licenses/KENNEY_LICENSE.txt](Licenses/KENNEY_LICENSE.txt).')
            content += ('\n## Credits included in this release\n\n'
                        'The complete [character and clothing attribution table](Licenses/CHARACTER_CREDITS.md), '
                        '[animation credits](Licenses/ANIMATION_CREDITS.md), '
                        'and [audio provenance](Licenses/AUDIO_CREDITS.txt) are included with this build. '
                        'See the [licence index](Licenses/README.md). `Assets/` paths above identify source-repository '
                        'files; the player stores its runtime assets in `Parasite_Data`.\n')
        output = build / destination
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(content)

    for source, destination in mapping.items():
        copied_document(source, destination)
    index = build / 'Licenses/README.md'
    index.write_text(
        '# PARASITE asset licences and credits\n\n'
        '- [Characters and clothing, including CC-BY attribution](CHARACTER_CREDITS.md)\n'
        '- [Humanoid animation sources and CMU acknowledgement](ANIMATION_CREDITS.md)\n'
        '- [Voice, ambience, music and sound provenance](AUDIO_CREDITS.txt)\n'
        '- [Rajdhani SIL Open Font License](OFL-Rajdhani.txt)\n'
        '- [Kenney CC0 notice](KENNEY_LICENSE.txt)\n\n'
        'The [asset provenance guide](../THIRD_PARTY_ASSETS.md) lists texture, model, foliage and skybox authors '
        'and source URLs. Keep these credits with the game when redistributing this player.\n')
    verified = 0
    for document in [build / destination for destination in mapping.values()] + [index]:
        for match in LINK.finditer(document.read_text()):
            target = match[2].strip('<>')
            url = urlsplit(target)
            if url.scheme or url.netloc or not url.path:
                continue
            resolved = (document.parent / unquote(url.path)).resolve()
            if not resolved.is_relative_to(build) or not resolved.is_file():
                raise ValueError(f'Broken packaged link in {document.relative_to(build)}: {target}')
            verified += 1
    print(f'Package documents: {len(GUIDES)} guides, {len(CREDITS)} licence/credit files; {verified} local Markdown links verified.')


if __name__ == '__main__':
    if len(sys.argv) != 3:
        raise SystemExit('Usage: package_docs.py <project directory> <existing build directory>')
    try:
        prepare(Path(sys.argv[1]), Path(sys.argv[2]))
    except (ValueError, OSError, subprocess.CalledProcessError) as error:
        raise SystemExit(str(error))
