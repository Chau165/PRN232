import { describe, expect, it } from "vitest"
import {
  canTerminateContractWithSettlement,
  formatForfeitedDepositReason,
  requiresDepositSettlement,
  validateDepositSettlementConfirmation,
} from "@/utils/depositSettlementValidation"
import {
  evaluateMoveOutDepositWarning,
  getLatestNoticeDateToKeepDeposit,
  validatePlannedMoveOutDate,
} from "@/utils/moveOutNoticeValidation"
import {
  MAX_IMAGE_EVIDENCE_BYTES,
  MAX_PDF_EVIDENCE_BYTES,
  validateContractEvidenceFile,
  validateEvidenceBatchCount,
  validateHideEvidenceReason,
} from "@/utils/contractEvidenceValidation"
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
import {
  bookingMoveInDateError,
  bookingOccupantCountError,
  formatBookingMoveInSummary,
} from "@/utils/bookingDisplay"
import {
  canChargeForHandoverItem,
  handoverChargeAmountError,
  handoverConditionError,
  handoverDateError,
  splitHandoverValidationErrors,
} from "@/utils/handoverValidation"

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
      expect(
        formatEquipmentSummary({
          name: "Bàn học",
          quantity: 1,
          compensationValue: 0,
        }),
      ).toBe("Bàn học × 1 — Chưa định giá")
      expect(formatEquipmentSummary({ name: "Máy lạnh API" })).toBe(
        "Máy lạnh API",
      )
    })

    it("validates booking moveInDate, occupantCount and move-in summary (C9)", () => {
      const today = "2026-11-15"
      expect(bookingMoveInDateError("", today)).toBe(
        "Vui lòng chọn ngày dọn vào.",
      )
      expect(bookingMoveInDateError("2026-11-14", today)).toBe(
        "Ngày dọn vào không được ở quá khứ.",
      )
      expect(bookingMoveInDateError("2026-11-15", today)).toBeNull()
      expect(bookingMoveInDateError("2026-11-29", today)).toBeNull()
      expect(bookingMoveInDateError("2026-11-30", today)).toBe(
        "Ngày dọn vào tối đa 14 ngày kể từ hôm nay.",
      )

      expect(bookingOccupantCountError(0)).toBe("Số người ở phải từ 1 đến 10.")
      expect(bookingOccupantCountError(11)).toBe("Số người ở phải từ 1 đến 10.")
      expect(
        bookingOccupantCountError(4, { roomCode: "A101", maxOccupants: 3 }),
      ).toBe("Phòng A101 chỉ được ở tối đa 3 người.")
      expect(
        bookingOccupantCountError(3, { roomCode: "A101", maxOccupants: 3 }),
      ).toBeNull()

      expect(
        formatBookingMoveInSummary({
          moveInDate: "2026-11-20",
          occupantCount: 2,
        }),
      ).toBe("Dọn vào 20/11/2026 · 2 người")
      expect(formatBookingMoveInSummary({})).toBeNull()
    })

    it("validates equipment handover records and maps items[i].field errors (C14)", () => {
      const today = "2026-11-15"
      expect(handoverDateError("2026-11-16", today)).toBe(
        "Ngày bàn giao không được ở tương lai.",
      )
      expect(handoverDateError("2026-11-15", today)).toBeNull()
      expect(handoverConditionError("Invalid")).toBe(
        "Tình trạng phải là Tốt, Hao mòn, Hư hỏng hoặc Mất.",
      )

      expect(canChargeForHandoverItem("MoveIn", "Damaged", null)).toBe(false)
      expect(canChargeForHandoverItem("MoveOut", "Good", "Good")).toBe(false)
      expect(canChargeForHandoverItem("MoveOut", "Worn", "Good")).toBe(false)
      expect(canChargeForHandoverItem("MoveOut", "Damaged", "Damaged")).toBe(false)
      expect(canChargeForHandoverItem("MoveOut", "Damaged", "Good")).toBe(true)

      expect(
        handoverChargeAmountError({
          kind: "MoveIn",
          equipmentName: "Máy lạnh",
          quantity: 1,
          compensationValue: 8_500_000,
          condition: "Damaged",
          chargeAmount: 100_000,
        }),
      ).toBe("Biên bản nhận phòng không tính tiền bồi thường.")

      expect(
        handoverChargeAmountError({
          kind: "MoveOut",
          equipmentName: "Máy lạnh",
          quantity: 1,
          compensationValue: 8_500_000,
          condition: "Worn",
          moveInCondition: "Good",
          chargeAmount: 100_000,
        }),
      ).toBe(
        "Máy lạnh: chỉ tính tiền khi thiết bị hư hỏng hoặc mất (hao mòn tự nhiên không phải bồi thường).",
      )

      expect(
        handoverChargeAmountError({
          kind: "MoveOut",
          equipmentName: "Máy lạnh",
          quantity: 1,
          compensationValue: 8_500_000,
          condition: "Damaged",
          moveInCondition: "Damaged",
          chargeAmount: 100_000,
        }),
      ).toBe(
        "Máy lạnh đã hư hỏng hoặc mất từ lúc nhận phòng nên không tính tiền khách.",
      )

      expect(
        handoverChargeAmountError({
          kind: "MoveOut",
          equipmentName: "Bàn học",
          quantity: 2,
          compensationValue: 500_000,
          condition: "Damaged",
          moveInCondition: "Good",
          chargeAmount: 1_200_000,
        }),
      ).toBe("Bàn học: tiền bồi thường tối đa 1.000.000đ (2 × 500.000đ).")

      const split = splitHandoverValidationErrors(
        {
          handoverDate: ["Ngày bàn giao không được ở tương lai."],
          "items[0].chargeAmount": ["Tiền bồi thường phải là bội số của 1.000đ."],
        },
        "Dữ liệu không hợp lệ.",
      )
      expect(split.fieldErrors).toEqual({
        handoverDate: "Ngày bàn giao không được ở tương lai.",
        "items[0].chargeAmount": "Tiền bồi thường phải là bội số của 1.000đ.",
      })
      expect(split.generalError).toBe("")
    })

    it("validates contract evidence files, count limits, and hide reasons (C15)", () => {
      expect(
        validateContractEvidenceFile({
          name: "anh-nhan-phong.jpg",
          type: "image/jpeg",
          size: 1_500_000,
        }),
      ).toBeNull()

      expect(
        validateContractEvidenceFile({
          name: "hop-dong-ky.pdf",
          type: "application/pdf",
          size: 8 * 1024 * 1024,
        }),
      ).toBeNull()

      expect(
        validateContractEvidenceFile({
          name: "tai-lieu.docx",
          type: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
          size: 100_000,
        }),
      ).toBe("Chỉ nhận ảnh JPG, PNG, WEBP hoặc file PDF hợp lệ.")

      expect(
        validateContractEvidenceFile({
          name: "anh-qua-nang.png",
          type: "image/png",
          size: MAX_IMAGE_EVIDENCE_BYTES + 1,
        }),
      ).toBe("Ảnh tối đa 5 MB, PDF tối đa 10 MB.")

      expect(
        validateContractEvidenceFile({
          name: "pdf-qua-nang.pdf",
          type: "application/pdf",
          size: MAX_PDF_EVIDENCE_BYTES + 1,
        }),
      ).toBe("Ảnh tối đa 5 MB, PDF tối đa 10 MB.")

      expect(validateEvidenceBatchCount(19, 1)).toBeNull()
      expect(validateEvidenceBatchCount(19, 2)).toBe(
        "Mỗi loại minh chứng tối đa 20 file.",
      )

      expect(validateHideEvidenceReason("   ")).toBe(
        "Vui lòng nhập lý do ẩn minh chứng.",
      )
      expect(validateHideEvidenceReason("a".repeat(501))).toBe(
        "Lý do ẩn minh chứng không được vượt quá 500 ký tự.",
      )
      expect(validateHideEvidenceReason("Ảnh tải nhầm hợp đồng")).toBeNull()
    })

    it("validates planned move-out date and computes 30-day deposit forfeiture warning (C16)", () => {
      expect(getLatestNoticeDateToKeepDeposit("2026-11-30")).toBe("2026-10-31")

      expect(
        validatePlannedMoveOutDate({
          plannedMoveOutDate: "2026-11-10",
          endDate: "2027-11-01",
          today: "2026-11-15",
        }),
      ).toBe("Ngày dự kiến trả phòng không được ở quá khứ.")

      expect(
        validatePlannedMoveOutDate({
          plannedMoveOutDate: "2027-11-05",
          endDate: "2027-11-01",
          today: "2026-11-15",
        }),
      ).toBe(
        "Ngày dự kiến trả phòng không được sau ngày kết thúc hợp đồng. Muốn ở lâu hơn hãy gia hạn hợp đồng.",
      )

      const earlyUnder30 = evaluateMoveOutDepositWarning({
        plannedMoveOutDate: "2026-11-30",
        endDate: "2027-11-01",
        today: "2026-11-15",
      })
      expect(earlyUnder30).toEqual({
        forfeitsDeposit: true,
        noticeDays: 15,
        latestNoticeDate: "2026-10-31",
        warningMessage:
          "Bạn trả phòng trước hạn hợp đồng mà báo trước dưới 30 ngày nên sẽ mất phần tiền cọc còn lại khi quyết toán. Báo muộn nhất ngày 31/10/2026 để giữ cọc.",
      })

      const earlyOver30 = evaluateMoveOutDepositWarning({
        plannedMoveOutDate: "2026-12-20",
        endDate: "2027-11-01",
        today: "2026-11-15",
      })
      expect(earlyOver30).toEqual({
        forfeitsDeposit: false,
        noticeDays: 35,
        latestNoticeDate: "2026-11-20",
        warningMessage: null,
      })

      const exactEndDate = evaluateMoveOutDepositWarning({
        plannedMoveOutDate: "2026-11-20",
        endDate: "2026-11-20",
        today: "2026-11-15",
      })
      expect(exactEndDate?.forfeitsDeposit).toBe(false)
      expect(exactEndDate?.warningMessage).toBeNull()
    })

    it("validates deposit settlement rules and checkout eligibility (C17)", () => {
      expect(requiresDepositSettlement({ securityDepositAmount: 0 })).toBe(false)
      expect(requiresDepositSettlement({ securityDepositAmount: 3_000_000 })).toBe(true)

      // Hợp đồng cũ không có cọc đảm bảo -> cho phép trả phòng ngay
      expect(
        canTerminateContractWithSettlement({ securityDepositAmount: 0 }, false),
      ).toBe(true)

      // Hợp đồng có cọc đảm bảo -> chỉ bật nút Trả phòng khi đã quyết toán
      expect(
        canTerminateContractWithSettlement(
          { securityDepositAmount: 3_000_000 },
          false,
        ),
      ).toBe(false)
      expect(
        canTerminateContractWithSettlement(
          { securityDepositAmount: 3_000_000 },
          true,
        ),
      ).toBe(true)

      expect(
        formatForfeitedDepositReason({
          earlyWithoutNotice: true,
          noticeDays: 10,
          forfeitedAmount: 1_800_000,
        }),
      ).toBe("trả trước hạn, báo trước 10 ngày < 30")

      expect(
        validateDepositSettlementConfirmation({
          outstandingDebt: 500_000,
          outstandingDebtAcknowledged: false,
        }),
      ).toBe(
        "Khách còn nợ hóa đơn 500.000\u00a0₫. Nên thu hết trước khi quyết toán; nếu khách không trả, hãy xác nhận để giữ lại cọc bù nợ.",
      )

      expect(
        validateDepositSettlementConfirmation({
          outstandingDebt: 500_000,
          outstandingDebtAcknowledged: true,
        }),
      ).toBeNull()
    })
  })
})
