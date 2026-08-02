import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const stylesheetUrl = new URL("../../PianoMapper.Web/wwwroot/css/app.css", import.meta.url);

test("page layout does not cap the available browser width", async () => {
    const stylesheet = await readFile(stylesheetUrl, "utf8");
    const mainRule = stylesheet.match(/main\s*{(?<declarations>[^}]*)}/)?.groups?.declarations;

    assert.ok(mainRule);
    assert.match(mainRule, /max-width:\s*none;/);
});
