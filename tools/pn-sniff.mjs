import { chromium } from "playwright";

const url = process.argv[2] || "https://l.postnord.com/VtFFznBKUZwi";
const browser = await chromium.launch();
const page = await browser.newPage();

page.on("request", (req) => {
  const u = req.url();
  if (u.includes("postnord") && (req.resourceType() === "xhr" || req.resourceType() === "fetch"))
    console.log("REQ", req.method(), u);
});

page.on("response", async (res) => {
  const u = res.url();
  if (!u.includes("postnord")) return;
  const ct = (res.headers()["content-type"] || "").split(";")[0];
  if (ct.includes("json") || u.includes("api") || u.includes("graphql")) {
    try {
      const body = (await res.text()).slice(0, 1200);
      console.log("RES", res.status(), ct, u, "\n", body, "\n====");
    } catch (e) {
      console.log("RES", res.status(), ct, u, "read err", e.message);
    }
  }
});

const resp = await page.goto(url, { waitUntil: "networkidle", timeout: 60000 });
console.log("CHAIN", resp?.request().redirectedFrom()?.url(), "->", resp?.url(), "status", resp?.status());
console.log("TEXT:", (await page.locator("body").innerText()).slice(0, 500));
await browser.close();
