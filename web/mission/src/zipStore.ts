/** Uncompressed ZIP (store method). No extra dependency. */

const CRC_TABLE = (() => {
  const t = new Uint32Array(256);
  for (let n = 0; n < 256; n++) {
    let c = n;
    for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    t[n] = c >>> 0;
  }
  return t;
})();

function crc32(data: Uint8Array): number {
  let c = 0xffffffff;
  for (let i = 0; i < data.length; i++) {
    c = CRC_TABLE[(c ^ data[i]) & 0xff] ^ (c >>> 8);
  }
  return (c ^ 0xffffffff) >>> 0;
}

function u16(n: number): Uint8Array {
  const b = new Uint8Array(2);
  new DataView(b.buffer).setUint16(0, n, true);
  return b;
}

function u32(n: number): Uint8Array {
  const b = new Uint8Array(4);
  new DataView(b.buffer).setUint32(0, n >>> 0, true);
  return b;
}

function concat(parts: Uint8Array[]): Uint8Array {
  const len = parts.reduce((n, p) => n + p.length, 0);
  const out = new Uint8Array(len);
  let o = 0;
  for (const p of parts) {
    out.set(p, o);
    o += p.length;
  }
  return out;
}

const enc = new TextEncoder();

export function zipStore(
  files: { path: string; text: string }[]
): Blob {
  if (files.length > 2048) throw new Error("ZIP 文件数量超过限制");
  const names = new Set<string>();
  const locals: Uint8Array[] = [];
  const centrals: Uint8Array[] = [];
  let offset = 0;
  for (const file of files) {
    safePath(file.path);
    if (names.has(file.path)) throw new Error("ZIP 路径重复");
    names.add(file.path);
    const name = enc.encode(file.path.replace(/\\/g, "/"));
    const data = enc.encode(file.text);
    const crc = crc32(data);
    const local = concat([
      u32(0x04034b50),
      u16(20),
      u16(0x800),
      u16(0),
      u16(0),
      u16(0),
      u32(crc),
      u32(data.length),
      u32(data.length),
      u16(name.length),
      u16(0),
      name,
      data,
    ]);
    const central = concat([
      u32(0x02014b50),
      u16(20),
      u16(20),
      u16(0x800),
      u16(0),
      u16(0),
      u16(0),
      u32(crc),
      u32(data.length),
      u32(data.length),
      u16(name.length),
      u16(0),
      u16(0),
      u16(0),
      u16(0),
      u32(0),
      u32(offset),
      name,
    ]);
    locals.push(local);
    centrals.push(central);
    offset += local.length;
  }
  const central = concat(centrals);
  const end = concat([
    u32(0x06054b50),
    u16(0),
    u16(0),
    u16(files.length),
    u16(files.length),
    u32(central.length),
    u32(offset),
    u16(0),
  ]);
  const bytes = concat([...locals, central, end]);
  const copy = new Uint8Array(bytes.byteLength);
  copy.set(bytes);
  return new Blob([copy], { type: "application/zip" });
}

function safePath(name: string) {
  if (!name || name.length > 4096 || name.startsWith("/") || /[\\:\x00-\x1f]/.test(name) || name.split("/").some(part => part === ".." || part === ".")) throw new Error("ZIP 路径不安全");
}

async function inflateRaw(data: Uint8Array, expected: number): Promise<Uint8Array> {
  const copy = new Uint8Array(data.byteLength);
  copy.set(data);
  const ds = new DecompressionStream("deflate-raw");
  const copyBuf = new ArrayBuffer(copy.byteLength);
  new Uint8Array(copyBuf).set(copy);
  const stream = new Blob([copyBuf]).stream().pipeThrough(ds);
  const reader = stream.getReader();
  const parts: Uint8Array[] = [];
  let total = 0;
  while (true) {
    const { done, value } = await reader.read();
    if (done) break;
    total += value.byteLength;
    if (total > expected) { await reader.cancel(); throw new Error("ZIP 解压大小超过声明"); }
    parts.push(value);
  }
  return concat(parts);
}

/** Read JSON/text files out of a zip (store or deflate). */
export async function unzipTextFiles(
  buf: ArrayBuffer
): Promise<{ name: string; text: string }[]> {
  const bytes = new Uint8Array(buf);
  const dv = new DataView(buf);
  const dec = new TextDecoder();
  const out: { name: string; text: string }[] = [];
  const requireZip = (valid: boolean) => { if (!valid) throw new Error("ZIP 结构、大小或 CRC 校验失败"); };
  requireZip(bytes.length >= 22 && bytes.length <= 64 * 1024 * 1024);
  let end = bytes.length - 22;
  while (end >= Math.max(0, bytes.length - 65557) && !(dv.getUint32(end, true) === 0x06054b50 && end + 22 + dv.getUint16(end + 20, true) === bytes.length)) end--;
  requireZip(end >= 0);
  const count = dv.getUint16(end + 10, true);
  const centralSize = dv.getUint32(end + 12, true);
  let cursor = dv.getUint32(end + 16, true);
  requireZip(dv.getUint16(end + 4, true) === 0 && dv.getUint16(end + 6, true) === 0 && dv.getUint16(end + 8, true) === count && count <= 2048 && cursor + centralSize === end);
  const centralStart = cursor;
  const names = new Set<string>();
  let total = 0;
  for (let entry = 0; entry < count; entry++) {
    requireZip(cursor + 46 <= end && dv.getUint32(cursor, true) === 0x02014b50);
    const flags = dv.getUint16(cursor + 8, true);
    const method = dv.getUint16(cursor + 10, true);
    const crc = dv.getUint32(cursor + 16, true);
    const compressed = dv.getUint32(cursor + 20, true);
    const size = dv.getUint32(cursor + 24, true);
    const nameLength = dv.getUint16(cursor + 28, true);
    const extraLength = dv.getUint16(cursor + 30, true);
    const commentLength = dv.getUint16(cursor + 32, true);
    const local = dv.getUint32(cursor + 42, true);
    requireZip(cursor + 46 + nameLength + extraLength + commentLength <= end && (flags & ~0x808) === 0 && (method === 0 || method === 8) && dv.getUint16(cursor + 34, true) === 0);
    const name = dec.decode(bytes.subarray(cursor + 46, cursor + 46 + nameLength));
    safePath(name);
    requireZip(!names.has(name));
    names.add(name);
    total += size;
    requireZip(size <= 16 * 1024 * 1024 && total <= 64 * 1024 * 1024 && local + 30 <= centralStart && dv.getUint32(local, true) === 0x04034b50);
    const localNameLength = dv.getUint16(local + 26, true);
    const start = local + 30 + localNameLength + dv.getUint16(local + 28, true);
    requireZip(start + compressed <= centralStart && dv.getUint16(local + 6, true) === flags && dv.getUint16(local + 8, true) === method && dec.decode(bytes.subarray(local + 30, local + 30 + localNameLength)) === name);
    if (!(flags & 8)) requireZip(dv.getUint32(local + 14, true) === crc && dv.getUint32(local + 18, true) === compressed && dv.getUint32(local + 22, true) === size);
    const packed = bytes.subarray(start, start + compressed);
    const raw = method === 8 ? await inflateRaw(packed, size) : packed;
    requireZip(raw.length === size && crc32(raw) === crc);
    if (/\.(json|txt|md|pchtxt)$/i.test(name)) out.push({ name, text: dec.decode(raw) });
    cursor += 46 + nameLength + extraLength + commentLength;
  }
  requireZip(cursor === end);
  return out;
}
