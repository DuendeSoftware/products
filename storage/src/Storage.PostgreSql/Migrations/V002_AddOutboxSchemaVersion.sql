-- Copyright (c) Duende Software. All rights reserved.
-- See LICENSE in the project root for license information.

-- V002: Add dso_type_schema_version column to outbox_subscriber_queue.
--
-- This column carries the DSO schema version from the entities table through
-- to outbox events, enabling version-aware deserialization in outbox consumers.
-- Nullable because domain events (non-DSO payloads) do not have a DSO schema version.
--
-- [[schemaname]] is replaced at runtime with the schema name.

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = '[[schemaname]]'
          AND table_name = 'outbox_subscriber_queue'
          AND column_name = 'dso_type_schema_version'
    ) THEN
        ALTER TABLE [[schemaname]].outbox_subscriber_queue
            ADD COLUMN dso_type_schema_version integer NULL;
    END IF;

    CREATE INDEX IF NOT EXISTS outbox_subscriber_queue_pool_id_index
        ON [[schemaname]].outbox_subscriber_queue (pool_id);

    COMMENT ON SCHEMA [[schemaname]] IS '{"Version":2}';
END
$$;
