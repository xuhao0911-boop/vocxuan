import { Engine } from "./voc-core.mjs";

export const MAX_JSON_BYTES = 8 * 1024 * 1024;
const object = (value: unknown): value is Record<string, unknown> => !!value && typeof value === "object" && !Array.isArray(value);

export function validateState(value: unknown) {
  if (!object(value) || !object(value.Review) || !Array.isArray(value.Imported) || value.Imported.length > 50000)
    throw new Error("学习存档格式不正确。");
  if (value.Version !== undefined && (!Number.isInteger(value.Version) || Number(value.Version) > 6))
    throw new Error("此存档版本暂不支持，请保留原文件。");
  const engine = new Engine([], structuredClone(value));
  const normalized = JSON.parse(engine.serialize());
  if (new TextEncoder().encode(JSON.stringify(normalized)).length > MAX_JSON_BYTES)
    throw new Error("存档超过 8 MB，请先导出备份并缩小本轮数量。");
  return normalized;
}

export async function readBounded(stream: ReadableStream<Uint8Array>, limit = MAX_JSON_BYTES): Promise<Uint8Array> {
  const reader = stream.getReader(); const chunks: Uint8Array[] = []; let size = 0;
  try {
    while (true) { const {value,done} = await reader.read(); if(done) break; size += value.length;
      if (size > limit) { await reader.cancel(); throw new Error("存档超过大小限制。"); } chunks.push(value); }
  } finally { reader.releaseLock(); }
  const result = new Uint8Array(size); let offset = 0;
  for(const chunk of chunks) { result.set(chunk,offset); offset += chunk.length; } return result;
}

export async function encodeState(state: unknown): Promise<ArrayBuffer> {
  const bytes = new TextEncoder().encode(JSON.stringify(state));
  const compressed = await new Response(new Blob([bytes]).stream().pipeThrough(new CompressionStream("gzip"))).arrayBuffer();
  if (compressed.byteLength > 1800000) throw new Error("压缩存档过大，请先导出备份。");
  return compressed;
}

export async function decodeState(payload: ArrayBuffer | number[]): Promise<unknown> {
  const bytes = payload instanceof ArrayBuffer ? new Uint8Array(payload) : new Uint8Array(payload);
  const stream = new Blob([bytes]).stream().pipeThrough(new DecompressionStream("gzip"));
  return JSON.parse(new TextDecoder().decode(await readBounded(stream)));
}
