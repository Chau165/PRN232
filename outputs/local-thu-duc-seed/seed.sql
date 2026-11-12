-- Local demo data only. Execute inside a transaction against prn232_local.
-- Fictional addresses and approximate coordinates; legacy ward names match the app.
-- Re-running skips matching records. Conflicting existing records abort the transaction.
SELECT pg_advisory_xact_lock(232, 1105);

DO $$
DECLARE
    owner_id integer;
    type_id integer;
    property_id integer;
    existing_count integer;
    inserted_count integer;
    added_properties integer := 0;
    added_types integer := 0;
    added_rooms integer := 0;
    item record;
BEGIN
    SELECT u."UserId" INTO STRICT owner_id
    FROM "Users" u JOIN "Roles" r ON r."RoleId" = u."RoleId"
    WHERE u."Email" = 'admin.local@troviet.test'
      AND u."IsActive" AND r."RoleName" = 'Admin';

    FOR item IN SELECT * FROM (VALUES
        ('Phòng tiêu chuẩn', 2500000::numeric),
        ('Phòng có gác', 3200000::numeric),
        ('Studio cơ bản', 4200000::numeric),
        ('Studio có ban công', 5000000::numeric)
    ) AS v(name, price)
    LOOP
        SELECT count(*) INTO existing_count FROM "RoomTypes" WHERE "TypeName" = item.name;
        IF existing_count > 1 OR EXISTS (
            SELECT 1 FROM "RoomTypes" WHERE "TypeName" = item.name AND "Price" <> item.price
        ) THEN RAISE EXCEPTION 'Conflicting room type: %', item.name; END IF;
        IF existing_count = 0 THEN
            INSERT INTO "RoomTypes" ("TypeName", "Price") VALUES (item.name, item.price);
            added_types := added_types + 1;
        END IF;
    END LOOP;

    FOR item IN SELECT * FROM (VALUES
        ('Nhà trọ An Nhiên', '18 Đường Demo A', 'Linh Trung',
         'Khu trọ dành cho sinh viên, có chỗ để xe và khu giặt chung.', 10.8700::numeric, 106.8000::numeric),
        ('Khu trọ Bình Minh', '25 Đường Demo B', 'Linh Chiểu',
         'Phòng có gác, lối đi riêng, phù hợp sinh viên và người đi làm.', 10.8500::numeric, 106.7600::numeric),
        ('Nhà trọ Riverside', '36 Đường Demo C', 'Hiệp Bình Chánh',
         'Phòng studio có nội thất cơ bản, phù hợp người đi làm hoặc cặp đôi.', 10.8400::numeric, 106.7200::numeric)
    ) AS v(name, address, ward, description, latitude, longitude)
    LOOP
        SELECT count(*) INTO existing_count FROM "Properties" WHERE "PropertyName" = item.name;
        IF existing_count > 1 OR EXISTS (
            SELECT 1 FROM "Properties" WHERE "PropertyName" = item.name AND (
                "OwnerId" IS DISTINCT FROM owner_id OR "AddressText" IS DISTINCT FROM item.address
                OR "Ward" IS DISTINCT FROM item.ward OR "District" IS DISTINCT FROM 'Thủ Đức'
                OR "City" IS DISTINCT FROM 'TP. Hồ Chí Minh'
                OR "Description" IS DISTINCT FROM item.description
                OR "Latitude" IS DISTINCT FROM item.latitude OR "Longitude" IS DISTINCT FROM item.longitude
            )
        ) THEN RAISE EXCEPTION 'Conflicting property: %', item.name; END IF;
        IF existing_count = 0 THEN
            INSERT INTO "Properties" ("OwnerId", "PropertyName", "PrimaryImageUrl", "Description",
                "AddressText", "Ward", "District", "City", "Latitude", "Longitude")
            VALUES (owner_id, item.name, NULL, item.description, item.address, item.ward,
                'Thủ Đức', 'TP. Hồ Chí Minh', item.latitude, item.longitude);
            added_properties := added_properties + 1;
        END IF;
    END LOOP;

    FOR item IN SELECT * FROM (VALUES
        ('Nhà trọ An Nhiên', 'AN-A101', 'Phòng tiêu chuẩn', 18::numeric, 'Available'),
        ('Nhà trọ An Nhiên', 'AN-A102', 'Phòng tiêu chuẩn', 20::numeric, 'Available'),
        ('Nhà trọ An Nhiên', 'AN-A201', 'Phòng có gác', 25::numeric, 'Available'),
        ('Nhà trọ An Nhiên', 'AN-A202', 'Phòng có gác', 25::numeric, 'Maintenance'),
        ('Khu trọ Bình Minh', 'BM-B101', 'Phòng tiêu chuẩn', 20::numeric, 'Available'),
        ('Khu trọ Bình Minh', 'BM-B102', 'Phòng có gác', 24::numeric, 'Available'),
        ('Khu trọ Bình Minh', 'BM-B201', 'Phòng có gác', 26::numeric, 'Available'),
        ('Khu trọ Bình Minh', 'BM-B202', 'Studio cơ bản', 28::numeric, 'Maintenance'),
        ('Nhà trọ Riverside', 'RS-C101', 'Studio cơ bản', 28::numeric, 'Available'),
        ('Nhà trọ Riverside', 'RS-C102', 'Studio cơ bản', 30::numeric, 'Available'),
        ('Nhà trọ Riverside', 'RS-C201', 'Studio có ban công', 35::numeric, 'Available'),
        ('Nhà trọ Riverside', 'RS-C202', 'Studio có ban công', 35::numeric, 'Available')
    ) AS v(property_name, code, type_name, area, status)
    LOOP
        SELECT "PropertyId" INTO STRICT property_id FROM "Properties" WHERE "PropertyName" = item.property_name;
        SELECT "RoomTypeId" INTO STRICT type_id FROM "RoomTypes" WHERE "TypeName" = item.type_name;
        SELECT count(*) INTO existing_count FROM "Rooms" WHERE "RoomCode" = item.code;
        IF existing_count > 1 OR EXISTS (
            SELECT 1 FROM "Rooms" WHERE "RoomCode" = item.code AND (
                "PropertyId" IS DISTINCT FROM property_id OR "RoomTypeId" IS DISTINCT FROM type_id
                OR "AreaSqm" IS DISTINCT FROM item.area
            )
        ) THEN RAISE EXCEPTION 'Conflicting room: %', item.code; END IF;
        IF existing_count = 0 THEN
            INSERT INTO "Rooms" ("PropertyId", "RoomTypeId", "RoomCode", "AreaSqm", "Status")
            VALUES (property_id, type_id, item.code, item.area, item.status);
            added_rooms := added_rooms + 1;
        END IF;
        -- Existing room status is preserved: a booking/contract may have changed it.
    END LOOP;
    RAISE NOTICE 'Inserted properties=%, roomTypes=%, rooms=%', added_properties, added_types, added_rooms;
END $$;
