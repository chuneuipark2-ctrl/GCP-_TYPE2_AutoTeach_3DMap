/**
 * @deprecated check-assy-stp-export.mjs --assy Lower_Frame_assy 사용
 * node tools/check-lower-stp-export.mjs [stp경로]
 */
import { spawnSync } from 'child_process';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '..');
const script = path.join(__dirname, 'check-assy-stp-export.mjs');
const args = [script, '--assy', 'Lower_Frame_assy'];
const stpArg = process.argv[2];
if (stpArg) args.push(path.resolve(stpArg));

const r = spawnSync(process.execPath, args, { stdio: 'inherit', cwd: ROOT });
process.exit(r.status ?? 1);
