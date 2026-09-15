#!/usr/bin/env python3
"""Verify source documentation, NuGet payloads, and a real isolated package consumer."""
import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[1]
EXCLUDED = {'bin', 'obj', '.git', '.vs', 'artifacts', 'TestResults', '__pycache__'}


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def verify_source():
    files = [p for p in ROOT.rglob('*') if p.is_file() and not EXCLUDED.intersection(p.relative_to(ROOT).parts)]
    require([p.relative_to(ROOT).as_posix() for p in files if p.suffix.lower() == '.md'] == ['README.md'],
            'Keep exactly one authored Markdown file: README.md')
    readme = (ROOT / 'README.md').read_text(encoding='utf-8')
    anchors = set(re.findall(r'<a id="([^"]+)"', readme))
    for target in re.findall(r'\]\((#[^)]+)\)', readme):
        require(target[1:] in anchors, f'Missing README anchor: {target}')
    snippets = re.findall(r'<!-- source: ([^\n]+) -->\n```[^\n]*\n(.*?)\n```', readme, re.S)
    require(len(snippets) >= 6, 'README must contain the complete tested implementation example')
    for name, content in snippets:
        require((ROOT / name).read_text(encoding='utf-8').rstrip() == content, f'Stale README example: {name}')
    for p in files:
        if p.suffix == '.csproj':
            project = ET.parse(p).getroot()
            tfms = project.findall('.//TargetFramework')
            expected = 'netstandard2.0' if p.stem == 'Causalia.Analyzers' else 'net10.0'
            require(len(tfms) == 1 and tfms[0].text == expected and not project.findall('.//TargetFrameworks'),
                    f'Unexpected target framework: {p}')
            lock = json.loads(p.with_name('packages.lock.json').read_text())
            require(all('net8' not in key.lower() for key in lock['dependencies']), f'Obsolete lock target: {p}')
            for reference in project.findall('.//ProjectReference'):
                require((p.parent / reference.attrib['Include']).exists(), f'Missing project reference: {p}')
    print('Source verified: .NET 10 only, one README, complete examples match compiled source.', flush=True)


def verify_packages(directory):
    packages = sorted(directory.glob('*.nupkg'))
    require(len(packages) == 13, f'Expected 13 packages, got {len(packages)}')
    require(len(list(directory.glob('*.snupkg'))) == 12, 'Expected 12 symbol packages')
    ids = []
    for path in packages:
        with zipfile.ZipFile(path) as package:
            entries = package.namelist()
            manifest = ET.fromstring(package.read(next(n for n in entries if n.endswith('.nuspec'))))
            metadata = manifest.find('{*}metadata')
            name = metadata.find('{*}id').text
            version = metadata.find('{*}version').text
            ids.append((name, version))
            require(metadata.find('{*}license').text == 'MIT', f'Missing MIT metadata: {name}')
            require(package.read('README.md') == (ROOT / 'README.md').read_bytes(), f'Outdated package README: {name}')
            require([n for n in entries if n.lower().endswith('.md')] == ['README.md'], f'Extra packaged Markdown: {name}')
            if name == 'Causalia.Analyzers':
                require('analyzers/dotnet/Causalia.Analyzers.dll' in entries, 'Analyzer not packaged in compiler folder')
                require(not any(n.startswith('lib/') for n in entries), 'Analyzer must not be a runtime dependency')
            else:
                require(f'lib/net10.0/{name}.dll' in entries, f'Missing .NET 10 assembly: {name}')
                require(f'lib/net10.0/{name}.xml' in entries, f'Missing XML API documentation: {name}')
                require(not any(n.startswith('lib/') and not n.startswith('lib/net10.0/') for n in entries),
                        f'Unexpected runtime framework: {name}')
    print('Packages verified: 13 NuGet packages, 12 symbol packages, one README per package.', flush=True)
    return ids


def verify_consumer(directory, ids):
    dotnet = os.environ.get('CAUSALIA_DOTNET', 'dotnet')
    with tempfile.TemporaryDirectory(prefix='causalia-consumer-') as temporary:
        work = Path(temporary)
        (work / 'global.json').write_bytes((ROOT / 'global.json').read_bytes())
        (work / 'Directory.Build.props').write_text('<Project />')
        (work / 'Directory.Build.targets').write_text('<Project />')
        config = ET.Element('configuration')
        sources = ET.SubElement(config, 'packageSources')
        ET.SubElement(sources, 'clear')
        ET.SubElement(sources, 'add', key='local', value=str(directory.resolve()))
        ET.SubElement(sources, 'add', key='nuget.org', value='https://api.nuget.org/v3/index.json')
        mapping = ET.SubElement(config, 'packageSourceMapping')
        ET.SubElement(mapping, 'clear')
        local = ET.SubElement(mapping, 'packageSource', key='local')
        for pattern in ('Causalia', 'Causalia.*'):
            ET.SubElement(local, 'package', pattern=pattern)
        ET.SubElement(ET.SubElement(mapping, 'packageSource', key='nuget.org'), 'package', pattern='*')
        ET.ElementTree(config).write(work / 'NuGet.Config', encoding='unicode')
        references = '\n'.join(f'<PackageReference Include="{name}" Version="{version}" />' for name, version in ids)
        (work / 'Consumer.csproj').write_text('''<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType>
<ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors>
<NuGetAudit>true</NuGetAudit><NuGetAuditMode>all</NuGetAuditMode></PropertyGroup>
<ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App" />''' + references + '</ItemGroup></Project>')
        (work / 'Program.cs').write_text('''using Causalia;
using Causalia.Storage;
using Causalia.Storage.Faults;
using Causalia.Storage.Exceptions;
using Causalia.Visualization;
using System.Threading;
var result = await Simulation.RunAsync(new SimulationOptions { Seed = 42 }, async context =>
{
    var database = context.CreateStorageDatabase("orders");
    var client = database.CreateClient(options: new SimulationStorageClientOptions
    {
        Faults = new StorageFaultPlan().FailAfterCommit(1)
    });
    var transaction = client.BeginTransaction();
    transaction.Write("order", new byte[] { 1 }, expectedVersion: 0);
    try
    {
        await transaction.CommitAsync(context.CancellationToken);
        throw new InvalidOperationException("Expected lost acknowledgement.");
    }
    catch (SimulationStorageAmbiguousCommitException)
    {
        var actual = await database.CreateClient().ReadAsync("order", context.CancellationToken);
        if (!actual.Exists || actual.Version != 1) throw new InvalidOperationException("Durability broken.");
    }
    await Task.Delay(TimeSpan.FromMinutes(10), context.TimeProvider, context.CancellationToken);
}, CancellationToken.None);
if (result.VirtualElapsed != TimeSpan.FromMinutes(10)) throw new InvalidOperationException("Virtual time broken.");
Console.WriteLine("Packaged consumer passed: durability and virtual time.");
''')
        env = dict(os.environ, NUGET_PACKAGES=str(work / 'packages'))
        def run(args, failure=False):
            completed = subprocess.run([dotnet, *args], cwd=work, env=env, text=True,
                                       stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=240)
            if failure:
                require(completed.returncode != 0 and 'error CAU1001' in completed.stdout,
                        'Packaged analyzer did not reject wall-clock access:\n' + completed.stdout)
            else:
                require(completed.returncode == 0, completed.stdout)
                print(completed.stdout[-1600:], flush=True)
        run(['restore', '--configfile', 'NuGet.Config', '-m:1', '/nr:false'])
        build = ['build', '-c', 'Release', '--no-restore', '-m:1', '/nr:false', '-p:UseSharedCompilation=false']
        run(build)
        run([str(work / 'bin/Release/net10.0/Consumer.dll')])
        (work / 'DeliberateEscape.cs').write_text('''using Causalia;
[DeterministicSimulation]
public static class DeliberateEscape
{
    public static DateTime ReadClock() => DateTime.UtcNow;
}
''')
        run(build, failure=True)
        print('Packaged analyzer verified: CAU1001 rejected nondeterministic clock access.', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--packages', type=Path)
    parser.add_argument('--consumer', action='store_true')
    args = parser.parse_args()
    verify_source()
    if args.packages:
        package_ids = verify_packages(args.packages)
        if args.consumer:
            verify_consumer(args.packages, package_ids)
