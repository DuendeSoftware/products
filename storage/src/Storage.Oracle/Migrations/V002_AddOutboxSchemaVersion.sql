-- Copyright (c) Duende Software. All rights reserved.
-- See LICENSE in the project root for license information.

-- V002: Add dso_type_schema_version column to OUTBOX_SUBSCRIBER_QUEUE.
--
-- This column carries the DSO schema version from the entities table through
-- to outbox events, enabling version-aware deserialization in outbox consumers.
-- Nullable because domain events (non-DSO payloads) do not have a DSO schema version.
--
-- [[schema]] is replaced at runtime with the quoted, schema-qualifying prefix.
--
-- Wrapped in a PL/SQL block that ignores ORA-01430 (column already exists)
-- so the script is idempotent.

BEGIN
    EXECUTE IMMEDIATE q'[ALTER TABLE [[schema]]OUTBOX_SUBSCRIBER_QUEUE ADD (dso_type_schema_version NUMBER(10) NULL)]';
EXCEPTION WHEN OTHERS THEN IF SQLCODE NOT IN (-1430) THEN RAISE; END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE q'[CREATE INDEX [[schema]]IX_OUTBOX_POOL_ID ON [[schema]]OUTBOX_SUBSCRIBER_QUEUE (pool_id)]';
EXCEPTION WHEN OTHERS THEN IF SQLCODE NOT IN (-955, -1408) THEN RAISE; END IF;
END;
/

MERGE INTO [[schema]]"__SCHEMA_INFO" d
USING (SELECT 2 AS version FROM dual) s
ON (d.version = s.version)
WHEN NOT MATCHED THEN INSERT (version) VALUES (2)
/
