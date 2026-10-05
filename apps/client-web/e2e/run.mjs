// Corre las pruebas e2e de client-web.
//
// En desarrollo el código compartido vive en apps/_shared, fuera de la app, y desde ahí no se
// resuelven react ni lucide-react (el Dockerfile lo copia DENTRO de la app por esa razón). Para
// poder construir y servir la app en local se hace lo mismo: se copia _shared a ./_shared y, al
// terminar, SIEMPRE se borra. Dejarlo ahí sería peligroso: el despliegue empaqueta el árbol
// completo y una copia vieja de _shared dentro de la app pisaría a la real en la imagen.
import { cpSync, existsSync, rmSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const appDir = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const source = resolve(appDir, '..', '_shared');
const target = resolve(appDir, '_shared');

if (existsSync(target)) {
  console.error('Ya existe apps/client-web/_shared; bórralo antes de correr las pruebas (ver comentario en e2e/run.mjs).');
  process.exit(1);
}

let status = 1;
try {
  cpSync(source, target, { recursive: true });
  // Con shell:true los argumentos pasan por cmd.exe: sin comillas, un filtro como "a|b" se leería como tubería.
  const quoted = process.argv.slice(2).map(arg => `"${arg.replace(/"/g, '\\"')}"`);
  const result = spawnSync('npx', ['playwright', 'test', ...quoted], {
    cwd: appDir,
    stdio: 'inherit',
    shell: true,
  });
  status = result.status ?? 1;
} finally {
  rmSync(target, { recursive: true, force: true });
}
process.exit(status);
