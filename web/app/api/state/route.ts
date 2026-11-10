import { env } from "cloudflare:workers";
import initialState from "@/private/initial-state.json";
import { validateState, encodeState, decodeState, readBounded } from "@/lib/state-codec";

export const dynamic = "force-dynamic";
const json = (data: unknown, status = 200) => Response.json(data, {status,headers:{"Cache-Control":"private, no-store","X-Content-Type-Options":"nosniff"}});
const identity = (request: Request) => request.headers.get("oai-authenticated-user-id");
function database() { if (!env.DB) throw new Error("云端存档暂时不可用，请保留本机草稿。"); return env.DB; }
type Row = { revision: number; payload: ArrayBuffer | number[]; receipts: string; updated_at: string };
const getRow = (owner: string) => database().prepare("SELECT revision,payload,receipts,updated_at FROM progress_state WHERE owner_id = ?").bind(owner).first<Row>();

export async function GET(request: Request) {
  const owner = identity(request); if(!owner) return json({error:"请先登录你的 ChatGPT 账号。"},401);
  try {
    const row = await getRow(owner);
    if(row && new URL(request.url).searchParams.get("revision") === String(row.revision))
      return json({userId:owner,revision:row.revision,updatedAt:row.updated_at,unchanged:true});
    return json({userId:owner,revision:row?.revision ?? 0,updatedAt:row?.updated_at ?? null,
      state:row ? await decodeState(row.payload) : validateState(initialState), initial:!row});
  } catch { return json({error:"暂时读不到云端存档，请稍后重试。"},503); }
}

export async function POST(request: Request) {
  const owner = identity(request); if(!owner) return json({error:"请先登录你的 ChatGPT 账号。"},401);
  if(request.headers.get("Origin") !== new URL(request.url).origin) return json({error:"请求来源不符。"},403);
  try {
    let stream = request.body; if(!stream) return json({error:"缺少存档。"},400);
    if(request.headers.get("Content-Encoding") === "gzip") stream = stream.pipeThrough(new DecompressionStream("gzip"));
    const body = JSON.parse(new TextDecoder().decode(await readBounded(stream)));
    if(!Number.isSafeInteger(body.revision) || body.revision < 0 || typeof body.mutationId !== "string" || !/^[a-zA-Z0-9-]{16,80}$/.test(body.mutationId))
      return json({error:"同步请求格式不正确。"},400);
    const current = await getRow(owner);
    const receipts: string[] = current ? JSON.parse(current.receipts) : [];
    if(receipts.includes(body.mutationId)) return json({revision:current!.revision,updatedAt:current!.updated_at,duplicate:true,
      ...(current!.revision !== body.revision+1 ? {state:await decodeState(current!.payload)} : {})});
    if((current?.revision ?? 0) !== body.revision) return json({error:"另一台设备已有更新。",revision:current?.revision ?? 0,
      state:current ? await decodeState(current.payload) : validateState(initialState)},409);
    const state = validateState(body.state), payload = await encodeState(state), updatedAt = new Date().toISOString();
    const nextReceipts = JSON.stringify(receipts.concat(body.mutationId).slice(-64));
    const result = current
      ? await database().prepare("UPDATE progress_state SET payload=?,revision=revision+1,receipts=?,updated_at=? WHERE owner_id=? AND revision=?")
        .bind(payload,nextReceipts,updatedAt,owner,body.revision).run()
      : await database().prepare("INSERT INTO progress_state(owner_id,revision,payload,receipts,updated_at) VALUES(?,1,?,?,?) ON CONFLICT(owner_id) DO NOTHING")
        .bind(owner,payload,nextReceipts,updatedAt).run();
    if(result.meta.changes !== 1) { const latest=await getRow(owner); return json({error:"另一台设备已有更新。",revision:latest?.revision ?? 0,
      state:latest ? await decodeState(latest.payload) : validateState(initialState)},409); }
    return json({revision:body.revision+1,updatedAt});
  } catch(error) { return json({error:error instanceof Error ? error.message : "同步暂时失败，本机草稿仍保留。"},400); }
}
