import { describe, expect, it } from "vitest"
import {
  areaSqmError,
  formatRoomFloor,
  maxOccupantsByArea,
  maxOccupantsError,
  monthlyRentError,
  propertyIdError,
  roomCodeError,
  roomDescriptionError,
  roomFloorError,
  roomTypeIdError,
  splitValidationErrors,
  validatePropertyRoomTypeForm,
  validateRoomForm,
} from "@/utils/roomValidation"
import {
  addressTextError,
  parseServiceRatePriceInput,
  propertyDescriptionError,
  propertyNameError,
  serviceIdError,
  serviceRatePriceError,
  validatePropertyForm,
} from "@/utils/propertyValidation"
import {
  equipmentCompensationValueError,
  equipmentNameError,
  equipmentQuantityError,
  equipmentRoomIdError,
  equipmentStatusError,
  equipmentSupplierError,
  formatEquipmentSummary,
  getVietnamTodayIsoDate,
  installDateError,
  validateEquipmentForm,
  warrantyExpiryError,
} from "@/utils/equipmentValidation"

describe("rebuild validation rules (C2 & C3)", () => {
  describe("roomValidation (C2)", () => {
    it("checks propertyId and roomTypeId", () => {
      expect(propertyIdError("")).toBe("Vui lòng chọn khu trọ.")
      expect(propertyIdError(0)).toBe("Vui lòng chọn khu trọ.")
      expect(propertyIdError(1)).toBeNull()

      expect(roomTypeIdError("")).toBe("Vui lòng chọn loại phòng.")
      expect(roomTypeIdError(0)).toBe("Vui lòng chọn loại phòng.")
      expect(roomTypeIdError(2)).toBeNull()
    })

    it("checks roomCode format and length", () => {
      expect(roomCodeError("   ")).toBe("Mã phòng không được để trống.")
      expect(roomCodeError("A".repeat(21))).toBe("Mã phòng tối đa 20 ký tự.")
      expect(roomCodeError("A".repeat(20))).toBeNull()
      expect(roomCodeError("A-101.2")).toBeNull()
      expect(roomCodeError("Phòng 101")).toBe(
        "Mã phòng chỉ gồm chữ không dấu, số, dấu gạch ngang hoặc dấu chấm (ví dụ: A-101).",
      )
      expect(roomCodeError("A_101")).toBe(
        "Mã phòng chỉ gồm chữ không dấu, số, dấu gạch ngang hoặc dấu chấm (ví dụ: A-101).",
      )
    })

    it("checks areaSqm boundaries (8 passes, 7.9 fails; 100 passes, 100.1 fails) and decimal places", () => {
      expect(areaSqmError("")).toBe("Diện tích phòng phải từ 8 đến 100 m².")
      expect(areaSqmError(7.9)).toBe("Diện tích phòng phải từ 8 đến 100 m².")
      expect(areaSqmError("7.9")).toBe("Diện tích phòng phải từ 8 đến 100 m².")
      expect(areaSqmError(8)).toBeNull()
      expect(areaSqmError("8")).toBeNull()
      expect(areaSqmError(24.5)).toBeNull()
      expect(areaSqmError(100)).toBeNull()
      expect(areaSqmError(100.1)).toBe("Diện tích phòng phải từ 8 đến 100 m².")
      expect(areaSqmError(24.55)).toBe(
        "Diện tích chỉ được có tối đa 1 chữ số thập phân.",
      )
    })

    it("checks monthlyRent boundaries (500.000 passes, 499.000 fails; 50.000.000 passes) and multiple of 1.000", () => {
      expect(monthlyRentError("")).toBe(
        "Giá thuê phải từ 500.000đ đến 50.000.000đ mỗi tháng.",
      )
      expect(monthlyRentError(499_000)).toBe(
        "Giá thuê phải từ 500.000đ đến 50.000.000đ mỗi tháng.",
      )
      expect(monthlyRentError("499000")).toBe(
        "Giá thuê phải từ 500.000đ đến 50.000.000đ mỗi tháng.",
      )
      expect(monthlyRentError(500_000)).toBeNull()
      expect(monthlyRentError("500000")).toBeNull()
      expect(monthlyRentError(50_000_000)).toBeNull()
      expect(monthlyRentError(50_001_000)).toBe(
        "Giá thuê phải từ 500.000đ đến 50.000.000đ mỗi tháng.",
      )
      expect(monthlyRentError(500_500)).toBe(
        "Giá thuê phải là bội số của 1.000đ.",
      )
    })

    it("validates room and property-room-type form maps and splits backend validationErrors", () => {
      expect(
        validateRoomForm({
          mode: "create",
          propertyId: "",
          roomTypeId: "",
          roomCode: "",
          areaSqm: "7.9",
        }),
      ).toEqual({
        propertyId: "Vui lòng chọn khu trọ.",
        roomTypeId: "Vui lòng chọn loại phòng.",
        roomCode: "Mã phòng không được để trống.",
        areaSqm: "Diện tích phòng phải từ 8 đến 100 m².",
      })

      expect(
        validatePropertyRoomTypeForm({
          mode: "create",
          roomTypeId: 1,
          monthlyRent: 499_000,
        }),
      ).toEqual({
        monthlyRent: "Giá thuê phải từ 500.000đ đến 50.000.000đ mỗi tháng.",
      })

      const split = splitValidationErrors(
        {
          areaSqm: ["Diện tích phòng phải từ 8 đến 100 m²."],
          other: ["Lỗi chung."],
        },
        ["areaSqm", "roomCode"] as const,
        "Fallback",
      )
      expect(split.fieldErrors).toEqual({
        areaSqm: "Diện tích phòng phải từ 8 đến 100 m².",
      })
      expect(split.generalError).toBe("Lỗi chung.")
    })

    it("checks C6 room fields: maxOccupants (1..10 & area/8), floor (0..50 & Tầng trệt), and description (<=1000)", () => {
      expect(maxOccupantsByArea(20)).toBe(2)
      expect(maxOccupantsByArea(95)).toBe(10)
      expect(maxOccupantsByArea("7.9")).toBeNull()

      expect(maxOccupantsError("", 20)).toBeNull()
      expect(maxOccupantsError(0, 20)).toBe(
        "Số người tối đa phải từ 1 đến 10.",
      )
      expect(maxOccupantsError(11, 100)).toBe(
        "Số người tối đa phải từ 1 đến 10.",
      )
      expect(maxOccupantsError(3, 20)).toBe(
        "Phòng 20 m² chỉ được ở tối đa 2 người (tối thiểu 8 m²/người).",
      )
      expect(maxOccupantsError(2, 20)).toBeNull()

      expect(roomFloorError("")).toBeNull()
      expect(roomFloorError(0)).toBeNull()
      expect(roomFloorError(50)).toBeNull()
      expect(roomFloorError(-1)).toBe("Tầng phải từ 0 (tầng trệt) đến 50.")
      expect(roomFloorError(51)).toBe("Tầng phải từ 0 (tầng trệt) đến 50.")
      expect(formatRoomFloor(0)).toBe("Tầng trệt")
      expect(formatRoomFloor(3)).toBe("Tầng 3")
      expect(formatRoomFloor(null)).toBeNull()

      expect(roomDescriptionError("")).toBeNull()
      expect(roomDescriptionError("a".repeat(1000))).toBeNull()
      expect(roomDescriptionError("a".repeat(1001))).toBe(
        "Mô tả phòng tối đa 1000 ký tự.",
      )
    })
  })

  describe("propertyValidation (C3)", () => {
    it("checks propertyName, addressText, and description", () => {
      expect(propertyNameError(" ")).toBe("Tên khu trọ không được để trống.")
      expect(propertyNameError("AB")).toBe(
        "Tên khu trọ phải từ 3 đến 150 ký tự.",
      )
      expect(propertyNameError("A".repeat(3))).toBeNull()
      expect(propertyNameError("A".repeat(150))).toBeNull()
      expect(propertyNameError("A".repeat(151))).toBe(
        "Tên khu trọ phải từ 3 đến 150 ký tự.",
      )

      expect(addressTextError("")).toBe(
        "Số nhà, tên đường không được để trống.",
      )
      expect(addressTextError("1234")).toBe(
        "Số nhà, tên đường phải từ 5 đến 255 ký tự.",
      )
      expect(addressTextError("12345")).toBeNull()
      expect(addressTextError("A".repeat(255))).toBeNull()
      expect(addressTextError("A".repeat(256))).toBe(
        "Số nhà, tên đường phải từ 5 đến 255 ký tự.",
      )

      expect(propertyDescriptionError("A".repeat(2000))).toBeNull()
      expect(propertyDescriptionError("A".repeat(2001))).toBe(
        "Mô tả tối đa 2000 ký tự.",
      )
    })

    it("checks serviceRate fields without hardcoding utility price ceilings", () => {
      expect(serviceIdError("")).toBe("Vui lòng chọn dịch vụ.")
      expect(serviceIdError(1)).toBeNull()

      expect(serviceRatePriceError("")).toBe("Đơn giá phải lớn hơn 0.")
      expect(serviceRatePriceError(0)).toBe("Đơn giá phải lớn hơn 0.")
      expect(serviceRatePriceError(-100)).toBe("Đơn giá phải lớn hơn 0.")
      expect(serviceRatePriceError("10.5")).toBe(
        "Đơn giá phải là số đồng nguyên.",
      )
      expect(parseServiceRatePriceInput("18.000")).toBe(18000)
      expect(serviceRatePriceError("18.000")).toBeNull()
      expect(serviceRatePriceError(5000)).toBeNull()
    })

    it("validates property form map", () => {
      expect(
        validatePropertyForm({
          propertyName: "AB",
          addressText: "123",
          description: "x".repeat(2001),
        }),
      ).toEqual({
        propertyName: "Tên khu trọ phải từ 3 đến 150 ký tự.",
        addressText: "Số nhà, tên đường phải từ 5 đến 255 ký tự.",
        description: "Mô tả tối đa 2000 ký tự.",
      })
    })
  })

  describe("equipmentValidation (C3)", () => {
    it("checks roomId, name, supplier, installDate, warrantyExpiry, and status", () => {
      expect(equipmentRoomIdError(0)).toBe("Vui lòng chọn phòng.")
      expect(equipmentRoomIdError(10)).toBeNull()

      expect(equipmentNameError("")).toBe(
        "Tên thiết bị là bắt buộc và không quá 200 ký tự.",
      )
      expect(equipmentNameError("A".repeat(201))).toBe(
        "Tên thiết bị là bắt buộc và không quá 200 ký tự.",
      )
      expect(equipmentNameError("Máy lạnh")).toBeNull()

      expect(equipmentSupplierError("")).toBe(
        "Nhà cung cấp là bắt buộc và không quá 200 ký tự.",
      )
      expect(equipmentSupplierError("B".repeat(201))).toBe(
        "Nhà cung cấp là bắt buộc và không quá 200 ký tự.",
      )
      expect(equipmentSupplierError("Daikin")).toBeNull()

      const today = getVietnamTodayIsoDate(new Date("2026-11-14T17:30:00Z"))
      expect(today).toBe("2026-11-15")
      expect(installDateError("2026-11-15", today)).toBeNull()
      expect(installDateError("2026-11-16", today)).toBe(
        "Ngày lắp đặt không được ở tương lai.",
      )

      expect(warrantyExpiryError("2026-11-15", "2026-11-14")).toBe(
        "Hạn bảo hành không được trước ngày lắp đặt.",
      )
      expect(warrantyExpiryError("2026-11-15", "2026-11-15")).toBeNull()

      expect(equipmentStatusError("Broken")).toBe(
        "Trạng thái thiết bị không được hỗ trợ.",
      )
      expect(equipmentStatusError("Operational")).toBeNull()

      expect(
        validateEquipmentForm(
          {
            mode: "create",
            roomId: 1,
            name: "Quạt",
            installDate: "2026-11-15",
            warrantyExpiry: "2027-11-15",
            supplier: "Senko",
            status: "Operational",
          },
          today,
        ),
      ).toEqual({})
    })

    it("checks C7 equipment quantity (1..20), compensationValue (0..50M, step 1000), and summary formatting", () => {
      expect(equipmentQuantityError("")).toBeNull()
      expect(equipmentQuantityError(1)).toBeNull()
      expect(equipmentQuantityError(20)).toBeNull()
      expect(equipmentQuantityError(0)).toBe("Số lượng phải từ 1 đến 20.")
      expect(equipmentQuantityError(21)).toBe("Số lượng phải từ 1 đến 20.")
      expect(equipmentQuantityError(1.5)).toBe("Số lượng phải từ 1 đến 20.")

      expect(equipmentCompensationValueError("")).toBeNull()
      expect(equipmentCompensationValueError(0)).toBeNull()
      expect(equipmentCompensationValueError(50_000_000)).toBeNull()
      expect(equipmentCompensationValueError(-1_000)).toBe(
        "Giá trị bồi thường phải từ 0đ đến 50.000.000đ.",
      )
      expect(equipmentCompensationValueError(50_001_000)).toBe(
        "Giá trị bồi thường phải từ 0đ đến 50.000.000đ.",
      )
      expect(equipmentCompensationValueError(8_500_500)).toBe(
        "Giá trị bồi thường phải là bội số của 1.000đ.",
      )

      expect(
        formatEquipmentSummary({
          name: "Máy lạnh",
          quantity: 2,
          compensationValue: 8_500_000,
        }),
      ).toBe("Máy lạnh × 2 — 8.500.000đ/cái")
      expect(formatEquipmentSummary({ name: "Máy lạnh API" })).toBe(
        "Máy lạnh API",
      )
    })
  })
})
