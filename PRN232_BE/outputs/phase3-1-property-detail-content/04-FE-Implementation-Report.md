# Phase 3.1 — FE/BE implementation report

## Implemented

- Backend public detail DTO now includes `roomAmenities` entries with amenity ID, catalog name, and count of rentable rooms carrying that amenity.
- Backend derives entries from this property’s rentable rooms, excludes blocked/unrentable rooms, groups in SQL, and reads the base detail plus summary in a repeatable-read transaction.
- FE restores the **Giới thiệu**, **Tiện ích**, and **Chi phí sinh hoạt** sections on `PropertyDetailPage` while preserving the existing `DetailSection` presentation pattern.
- FE renders actual API room-amenity names and coverage counts. Known amenity names receive existing-style icons; unknown names retain their actual text with a generic icon.
- Missing introduction and cost data display an accessible “đang được cập nhật” status. No Phase 2-era sample description, universal amenities, or invented price/unit was reintroduced.
- FE supports the old API response during rollout by treating the new field as optional.
- BE API/PostgreSQL tests and FE page tests cover property scoping, rentable-room counts, empty amenities, unavailable content states, and sample/mock leakage.

## Changed code files

- `PRN232_BE/PRN232.Application/DTOs/PublicPropertyListingDto.cs`
- `PRN232_BE/PRN232.Infrastructure/Repositories/PublicPropertyListingRepository.cs`
- `PRN232_BE/PRN232.Application.Tests/PublicPropertyListingApiTests.cs`
- `PRN232_BE/PRN232.Application.Tests/PublicPropertyListingPostgreSqlTests.cs`
- `PRN232_FE/src/api/publicBrowsingApi.ts`
- `PRN232_FE/src/pages/PropertyDetailPage/PropertyDetailPage.tsx`
- `PRN232_FE/tests/publicPropertyPages.test.tsx`

## Not implemented pending schema/content approval

Persisted introduction content, explicit property-level amenity assignments, and structured public utility rates require owner decisions and database work. No migrations or live data updates were run. Management CRUD was not changed.

## Runtime boundary

FE build/type checks and automated tests passed as reported in `05-Test-Matrix-And-UAT.md`. Browser UAT and FE-to-new-BE live HTTP verification remain unverified because starting temporary local servers was rejected by the execution/tool policy; the existing API process was left untouched.
