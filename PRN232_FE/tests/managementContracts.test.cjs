const { test, beforeEach } = require("node:test")
const assert = require("node:assert/strict")
const fs = require("node:fs")
const path = require("node:path")
const ts = require("typescript")

// Run the actual TypeScript utilities with isolated browser storage.
const modules = new Map()
function loadModule(file) {
  if (modules.has(file)) return modules.get(file).exports
  const module = { exports: {} }
  modules.set(file, module)
  const { outputText } = ts.transpileModule(fs.readFileSync(file, "utf8"), {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2020 },
  })
  const localRequire = (name) => name.startsWith("@/")
    ? loadModule(path.resolve(__dirname, "../src", `${name.slice(2)}.ts`))
    : require(name)
  new Function("require", "module", "exports", outputText)(localRequire, module, module.exports)
  return module.exports
}

const storage = new Map()
global.window = { localStorage: {
  getItem: (key) => storage.get(key) ?? null,
  setItem: (key, value) => storage.set(key, value),
} }
const { addContractMonths, createBookingContract, getCreatedContracts } = loadModule(path.resolve(__dirname, "../src/utils/managementContracts.ts"))
const { getManagementScope } = loadModule(path.resolve(__dirname, "../src/utils/managementScope.ts"))
const { managedBookings } = loadModule(path.resolve(__dirname, "../src/data/management.ts"))
const manager = { id: "manager-1", name: "Manager", role: "MANAGER", assignedPropertyIds: ["happy-home"] }

beforeEach(() => storage.clear())

test("quick durations preserve calendar dates and clamp short months", () => {
  assert.equal(addContractMonths("2026-10-01", 6), "2027-04-01")
  assert.equal(addContractMonths("2026-10-01", 12), "2027-10-01")
  assert.equal(addContractMonths("2026-10-01", 24), "2028-10-01")
  assert.equal(addContractMonths("2024-02-29", 12), "2025-02-28")
  assert.equal(addContractMonths("2026-08-31", 6), "2027-02-28")
  assert.equal(addContractMonths("2026-02-30", 6), "")
})

test("confirmed booking creates a persisted contract and becomes converted", () => {
  const contract = createBookingContract(manager, "BK-2026-0021", "2027-10-01")
  assert.equal(contract.tenantName, "Minh Khang")
  assert.equal(contract.tenantPhone, "0912 555 678")
  assert.equal(contract.startDate, "01/10/2026")
  assert.equal(contract.endDate, "01/10/2027")
  assert.equal(contract.monthlyRent, "3.400.000đ")
  assert.deepEqual(contract.companions, [])
  assert.equal(getCreatedContracts()[0].id, contract.id)
  assert.equal(getManagementScope(manager).contracts[0].id, contract.id)
  assert.equal(getManagementScope(manager).bookings[0].status, "Converted to Rental")
  assert.throws(() => createBookingContract(manager, "BK-2026-0021", "2028-10-01"), /Chỉ có thể/)
  assert.equal(getCreatedContracts().length, 1)
})

test("non-confirmed, unknown, and inaccessible bookings cannot create contracts", () => {
  const admin = { ...manager, role: "ADMIN" }
  assert.throws(() => createBookingContract(admin, "BK-2026-0018", "2027-10-01"), /Chỉ có thể/)
  assert.throws(() => createBookingContract(admin, "BK-2026-0025", "2027-10-01"), /Chỉ có thể/)
  assert.throws(() => createBookingContract(manager, "unknown", "2027-10-01"), /không có quyền/)
  assert.throws(() => createBookingContract({ ...manager, assignedPropertyIds: [] }, "BK-2026-0021", "2027-10-01"), /không có quyền/)
  assert.throws(() => createBookingContract({ ...manager, role: "CUSTOMER" }, "BK-2026-0021", "2027-10-01"), /không có quyền/)
  assert.equal(getCreatedContracts().length, 0)
})

test("end date must exist, be valid and be after the move-in date", () => {
  for (const date of ["", "2026-02-30", "2026-09-30", "2026-10-01"]) {
    assert.throws(() => createBookingContract(manager, "BK-2026-0021", date), /Ngày kết thúc/)
  }
  assert.equal(getCreatedContracts().length, 0)
})

test("all companions are copied including an optional missing phone", () => {
  const booking = managedBookings.find((item) => item.id === "BK-2026-0021")
  const original = booking.companions
  try {
    booking.companions = [{ name: "Người A" }, { name: "Người B", phone: "0901234567" }]
    const contract = createBookingContract(manager, booking.id, "2027-10-01")
    assert.deepEqual(contract.companions, booking.companions)
    assert.notEqual(contract.companions, booking.companions)
  } finally {
    booking.companions = original
  }
})

test("failed storage does not convert the booking or report a saved contract", () => {
  const original = window.localStorage.setItem
  try {
    window.localStorage.setItem = () => { throw new Error("Storage full") }
    assert.throws(() => createBookingContract(manager, "BK-2026-0021", "2027-10-01"), /Storage full/)
  } finally {
    window.localStorage.setItem = original
  }
  assert.equal(getCreatedContracts().length, 0)
  assert.equal(getManagementScope(manager).bookings[0].status, "Confirmed")
})
