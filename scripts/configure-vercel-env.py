"""Upload only Omnom's server secrets to the linked Vercel project via stdin."""
from pathlib import Path
import subprocess

values = {}
for line in Path('.env').read_text().splitlines():
    if '=' in line and not line.lstrip().startswith('#'):
        name, value = line.split('=', 1)
        values[name.strip()] = value.strip().strip('\"\'')

for name in ('OPENROUTER_API_KEY', 'APP_PASSCODE', 'Jwt__Secret'):
    if not values.get(name):
        raise SystemExit(f'Missing required setting: {name}')
    result = subprocess.run(['vercel', 'env', 'add', name, 'production,preview', '--sensitive', '--yes',
        '--scope', 'victorys-projects-c1cb4594'], input=values[name], text=True, capture_output=True)
    if result.returncode:
        raise SystemExit(f'Could not configure {name}; inspect Vercel environment settings.')
    print(f'Configured {name} as a server secret.')

for name, value in [('PORT', '8080'), ('REQUIRE_PASSCODE', 'true')]:
    subprocess.run(['vercel', 'env', 'add', name, 'production,preview', '--value', value,
        '--yes', '--scope', 'victorys-projects-c1cb4594'], check=True)
