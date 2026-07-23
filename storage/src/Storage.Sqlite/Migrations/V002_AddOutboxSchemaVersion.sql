-- Copyright (c) Duende Software. All rights reserved.
-- See LICENSE in the project root for license information.

-- V002: Add dso_type_schema_version column to outbox_subscriber_queue.
--
-- This column carries the DSO schema version from the entities table through
-- to outbox events, enabling version-aware deserialization in outbox consumers.
-- Nullable because domain events (non-DSO payloads) do not have a DSO schema version.

ALTER TABLE outbox_subscriber_queue ADD COLUMN dso_type_schema_version INTEGER NULL;

CREATE INDEX IF NOT EXISTS outbox_subscriber_queue_pool_id_index
ON outbox_subscriber_queue (pool_id);

-- Version bump: only update if not already at version 2
INSERT OR IGNORE INTO __schema_info (key, value) VALUES ('SchemaVersion', '{"Version":2}');
UPDATE __schema_info SET value = '{"Version":2}' WHERE key = 'SchemaVersion' AND json_extract(value, '$.Version') < 2;
