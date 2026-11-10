# VocXuan Sites

Owner-private vocabulary study app with cloud snapshots, spaced review, daily study records, and full JSON backup/import. The browser study core reuses VocXuan v1.6. The server module in lib/voc-core.mjs is the same domain core exported for validation.

The Site is private to the owner. The root page requires dispatch-owned ChatGPT authentication; API records use the trusted site-scoped authenticated user ID. Browser-supplied identity headers are stripped by the platform. Private initial desktop progress is imported only by server code, never copied into public assets. The first authenticated session persists it to D1.

Cloud writes use compressed snapshots, compare-and-swap revisions, and the latest 64 mutation receipts to make retries idempotent. Stale or simultaneous writers receive a conflict, retain a local draft, and explicitly choose a version. No silent last-writer overwrite is used. Drafts can be downloaded from the backup dialog. Local cache is scoped to Site and authenticated user. Reads poll only while visible; errors back off and drafts remain local.

The D1 schema and migration are under db/ and drizzle/. Hosted migrations are applied by Sites publication. Keep applied migration SQL and metadata immutable. Local preview data and hosted data are separate.

Development and production commands follow the bundled Sites lifecycle. Use the Sites plugin helpers for installation, builds, source synchronization, packaging, and private deployment. Never put source repository tokens into files or shell arguments. No external paid service, SMS service, custom domain, or model API is required.

A native APK or desktop EXE does not participate in this Site's synchronization. Use the same Site URL on both devices. JSON imports replace the hosted snapshot after explicit confirmation and keep a local backup. The existing original application files are preserved.
