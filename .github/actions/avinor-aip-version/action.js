import path from "node:path";
import { promisify } from "node:util";
import { createWriteStream } from "node:fs";
import * as fs from "node:fs/promises";
import { URL } from "node:url";
import { pipeline } from "node:stream";

import fetch from "node-fetch";
import { Cookie } from "tough-cookie";

const streamPipeline = promisify(pipeline);

async function action() {
  const langCode = "no";
  const docFolderPath = path.join("doc", langCode);
  await fs.mkdir(docFolderPath, { recursive: true });
  let aipUrl = new URL(`https://ais.avinor.no/${encodeURI(langCode)}/AIP`);
  let aipResponse = await fetch(aipUrl, { redirect: "manual" });
  const aipSetCookies = aipResponse.headers.raw()["set-cookie"] || [];
  const aipCookies = aipSetCookies.map((c) => Cookie.parse(c));
  const aipLocation = aipResponse.headers.get("location");
  if (aipLocation) aipUrl = new URL(aipLocation, aipUrl);
  aipResponse = await fetch(aipUrl, {
    headers: aipCookies.map((c) => ["cookie", c?.cookieString() || ""]),
    redirect: "follow",
  });
  aipUrl = new URL(aipResponse.url, aipUrl);
  const aipPath = decodeURI(aipUrl.pathname);
  const aipFileName = aipPath.substring(aipPath.lastIndexOf("/") + 1);
  const aipFilePath = path.join(docFolderPath, aipFileName);
  const aipFileStream = createWriteStream(aipFilePath);
  if (aipResponse.body) await streamPipeline(aipResponse.body, aipFileStream);
}

action();
