-- Copyright (c) Duende Software. All rights reserved.
-- See LICENSE in the project root for license information.

-- V002: Add dso_type_schema_version column to outbox_subscriber_queue.
--
-- This column carries the DSO schema version from the entities table through
-- to outbox events, enabling version-aware deserialization in outbox consumers.
-- Nullable because domain events (non-DSO payloads) do not have a DSO schema version.
--
-- Uses [[schemaname]] as a placeholder replaced at runtime.

DECLARE @current_version INT = ISNULL((
    SELECT CAST(JSON_VALUE(CAST(value AS NVARCHAR(MAX)), '$.Version') AS INT)
    FROM sys.extended_properties ep
    WHERE ep.class = 3
      AND ep.name = N'SchemaVersion'
      AND ep.major_id = SCHEMA_ID('[[schemaname]]')
), 0);

IF @current_version < 2
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = '[[schemaname]]'
          AND TABLE_NAME = 'outbox_subscriber_queue'
          AND COLUMN_NAME = 'dso_type_schema_version'
    )
    BEGIN
        ALTER TABLE [[[schemaname]]].[outbox_subscriber_queue]
            ADD dso_type_schema_version INT NULL;
    END

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = 'IX_[[schemaname]]_outbox_subscriber_queue_pool_id'
          AND object_id = OBJECT_ID('[[[schemaname]]].[outbox_subscriber_queue]')
    )
    BEGIN
        CREATE INDEX IX_[[schemaname]]_outbox_subscriber_queue_pool_id
            ON [[[schemaname]]].[outbox_subscriber_queue] (pool_id);
    END

    -- Version bump: property already exists from V001, use sp_updateextendedproperty
    EXEC sys.sp_updateextendedproperty
        @name = N'SchemaVersion',
        @value = N'{"Version":2}',
        @level0type = N'SCHEMA',
        @level0name = N'[[schemaname]]';
END
