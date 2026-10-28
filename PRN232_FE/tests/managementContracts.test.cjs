const { test } = require("node:test")
const assert = require("node:assert/strict")
const fs = require("node:fs")
const path = require("node:path")
const ts = require("typescript")

// Run the actual TypeScript date helpers used by the create-contract page.
function loadModule(file) {
  const module = { exports: {} }
  const { outputText } = ts.transpileModule(fs.readFileSync(file, "utf8"), {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2020 },
  })
  new Function("require", "module", "exports", outputText)(require, module, module.exports)
  return module.exports
}

const { addContractMonths, isContractDate, formatContractDate } =
  loadModule(path.resolve(__dirname, "../src/utils/managementContracts.ts"))

test("quick durations preserve calendar dates and clamp short months", () => {
  assert.equal(addContractMonths("2026-10-01", 6), "2027-04-01")
  assert.equal(addContractMonths("2026-10-01", 12), "2027-10-01")
  assert.equal(addContractMonths("2026-10-01", 24), "2028-10-01")
  assert.equal(addContractMonths("2024-02-29", 12), "2025-02-28")
  assert.equal(addContractMonths("2026-08-31", 6), "2027-02-28")
  assert.equal(addContractMonths("2026-02-30", 6), "")
})

test("contract dates must be real calendar days and display as dd/MM/yyyy", () => {
  assert.equal(isContractDate("2026-10-31"), true)
  assert.equal(isContractDate("2026-02-30"), false)
  assert.equal(isContractDate("31/10/2026"), false)
  assert.equal(formatContractDate("2026-10-31"), "31/10/2026")
})
