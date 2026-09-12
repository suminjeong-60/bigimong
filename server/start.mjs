import { resolve } from "node:path";
import { createBackendServer } from "./http-server.js";

const port = Number(process.env.PORT ?? 4180);
const dbPath = resolve(process.env.BIGI_DB_PATH ?? "data/bigi-dragon.sqlite");
const backend = createBackendServer({
  dbPath,
  allowDevelopmentEndpoints: process.env.BIGI_DEV === "1"
});

await backend.listen(port, "0.0.0.0");
console.log(`Bigimong API listening on http://localhost:${port}`);

async function shutdown() {
  await backend.close();
  process.exit(0);
}

process.on("SIGINT", shutdown);
process.on("SIGTERM", shutdown);
